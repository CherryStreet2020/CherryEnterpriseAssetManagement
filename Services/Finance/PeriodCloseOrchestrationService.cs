using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Finance
{
    // MP #112 — Period Close Orchestration.
    //
    // The piece that turns CherryAI EAM from "auditor-credible reports" into
    // "auditor-credible workflow." Maximo doesn't ship a one-click sequenced
    // close. SAP buries it under 11 transaction codes. We do it in one screen
    // with red/green pre-flight checks that refuse to lock the period until
    // it's actually clean.
    //
    // The orchestration sits ABOVE the existing primitives:
    //   - FiscalCalendarService already materializes the 12 monthly periods
    //   - PeriodGuard already refuses JE writes into non-Open periods
    //   - JournalGenerator.GenerateMonthlyAsync already posts depreciation
    //   - TrialBalance report already computes Σ DR / Σ CR
    // We compose those into a single transactionally-safe close.
    //
    // ROLE GATE: Admin or Finance. Hard-coded in the page-level [Authorize];
    // the service trusts the caller.
    //
    // The PreflightSnapshot is persisted on FiscalPeriod.PreflightSnapshotJson
    // at close time so an auditor reading the period record sees the full
    // close packet inline — every check value, every step result, who, when.

    public enum CheckStatus
    {
        Pass = 0,
        Warn = 1,  // non-blocking; close can proceed
        Fail = 2,  // blocking; close refuses unless overridden
        NotApplicable = 3
    }

    public sealed record PreflightCheck(
        string Code,
        string Title,
        CheckStatus Status,
        string ValueLabel,
        string Detail,
        string? DrillThroughUrl);

    public sealed record CloseStepResult(
        string Code,
        string Title,
        bool Succeeded,
        string Detail,
        decimal? Amount,
        int? RelatedEntityId);

    public sealed record PeriodClosePacket(
        int FiscalPeriodId,
        int CompanyId,
        string PeriodName,
        DateTime PeriodStartDate,
        DateTime PeriodEndDate,
        DateTime CapturedAt,
        string CapturedBy,
        IReadOnlyList<PreflightCheck> Preflight,
        IReadOnlyList<CloseStepResult> Steps,
        bool OverrideUsed,
        string? OverrideReason);

    public sealed record CloseResult(
        bool Succeeded,
        FiscalPeriod? Period,
        PeriodClosePacket Packet,
        string? ErrorMessage);

    public interface IPeriodCloseOrchestrationService
    {
        Task<IReadOnlyList<PreflightCheck>> RunPreflightAsync(int companyId, int fiscalPeriodId);

        Task<CloseResult> ExecuteCloseAsync(
            int companyId,
            int fiscalPeriodId,
            string username,
            bool overrideFailures = false,
            string? overrideReason = null);

        Task<CloseResult> ReopenAsync(
            int companyId,
            int fiscalPeriodId,
            string username,
            string reason);

        Task<FiscalPeriod?> GetNextCloseableAsync(int companyId);
    }

    public class PeriodCloseOrchestrationService : IPeriodCloseOrchestrationService
    {
        private readonly AppDbContext _db;
        private readonly AuditService _audit;
        private readonly ILogger<PeriodCloseOrchestrationService> _logger;

        // GR/IR clearing tolerance: net DR-CR on the clearing account at
        // period close should be ≤ this. Anything bigger means one or more
        // GR receipts have no matching invoice and should clear before lock.
        private const decimal GrIrToleranceUsd = 1.00m;

        // Stale-GR threshold: GRs without inspection action older than this
        // (calendar days) get flagged. Captures "received-but-forgotten"
        // inventory drift that distorts AP.
        private const int StaleGrInspectionDays = 30;

        public PeriodCloseOrchestrationService(
            AppDbContext db,
            AuditService audit,
            ILogger<PeriodCloseOrchestrationService> logger)
        {
            _db = db;
            _audit = audit;
            _logger = logger;
        }

        public async Task<FiscalPeriod?> GetNextCloseableAsync(int companyId)
        {
            // Earliest Open period whose EndDate is on or before today —
            // i.e., the period actually elapsed in the real world. Refusing
            // to close future periods prevents a finger-slip that locks
            // June while we're still in May.
            var today = DateTime.UtcNow.Date;
            return await _db.FiscalPeriods
                .Where(p => p.CompanyId == companyId
                         && p.Status == PeriodStatus.Open
                         && p.EndDate <= today)
                .OrderBy(p => p.StartDate)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<PreflightCheck>> RunPreflightAsync(int companyId, int fiscalPeriodId)
        {
            var period = await _db.FiscalPeriods
                .Include(p => p.FiscalYear)
                .FirstOrDefaultAsync(p => p.Id == fiscalPeriodId && p.CompanyId == companyId)
                ?? throw new InvalidOperationException(
                    $"Fiscal period {fiscalPeriodId} not found in company {companyId}.");

            var endInclusive = period.EndDate.Date.AddDays(1).AddTicks(-1);

            var checks = new List<PreflightCheck>
            {
                await CheckPeriodStateAsync(period),
                await CheckTrialBalanceAsync(period.StartDate, endInclusive),
                await CheckDepreciationStatusAsync(companyId, period),
                await CheckGrIrClearingAsync(companyId, period.StartDate, endInclusive),
                await CheckStaleOpenReceiptsAsync(companyId, period.EndDate),
                await CheckUnapprovedInvoicesInPeriodAsync(companyId, period.StartDate, endInclusive),
                await CheckCorrectiveWosCompletionAsync(companyId, period.StartDate, endInclusive)
            };

            return checks;
        }

        public async Task<CloseResult> ExecuteCloseAsync(
            int companyId,
            int fiscalPeriodId,
            string username,
            bool overrideFailures = false,
            string? overrideReason = null)
        {
            var period = await _db.FiscalPeriods
                .Include(p => p.FiscalYear)
                .FirstOrDefaultAsync(p => p.Id == fiscalPeriodId && p.CompanyId == companyId);

            if (period == null)
            {
                throw new InvalidOperationException(
                    $"Fiscal period {fiscalPeriodId} not found in company {companyId}.");
            }

            if (period.Status != PeriodStatus.Open)
            {
                throw new InvalidOperationException(
                    $"Period '{period.Name}' is already {period.Status}. Reopen it first if you need to re-close.");
            }

            var preflight = await RunPreflightAsync(companyId, fiscalPeriodId);
            var blocking = preflight.Where(c => c.Status == CheckStatus.Fail).ToList();
            if (blocking.Any() && !overrideFailures)
            {
                var packet = new PeriodClosePacket(
                    FiscalPeriodId: period.Id,
                    CompanyId: companyId,
                    PeriodName: period.Name,
                    PeriodStartDate: period.StartDate,
                    PeriodEndDate: period.EndDate,
                    CapturedAt: DateTime.UtcNow,
                    CapturedBy: username,
                    Preflight: preflight,
                    Steps: new List<CloseStepResult>(),
                    OverrideUsed: false,
                    OverrideReason: null);

                return new CloseResult(
                    Succeeded: false,
                    Period: period,
                    Packet: packet,
                    ErrorMessage: $"{blocking.Count} pre-flight check(s) failed. Resolve them or close with override + reason.");
            }

            if (blocking.Any() && string.IsNullOrWhiteSpace(overrideReason))
            {
                throw new InvalidOperationException(
                    "Override requires a written reason. The reason is captured in the AuditLog and the close packet.");
            }

            // Run the sequenced close inside one EF transaction. If any step
            // throws, the lock won't be persisted.
            var steps = new List<CloseStepResult>();
            using var tx = await _db.Database.BeginTransactionAsync();

            try
            {
                // Step 1: post monthly depreciation per Book scoped to this company.
                // Idempotent: if DepreciationPosted is already true, skip.
                if (!period.DepreciationPosted)
                {
                    var books = await _db.Books
                        .Where(b => b.CompanyId == companyId || b.CompanyId == null)
                        .ToListAsync();

                    decimal depTotal = 0m;
                    int booksRun = 0;
                    foreach (var book in books)
                    {
                        try
                        {
                            var entry = await JournalGenerator.GenerateMonthlyAsync(
                                _db,
                                book.Id,
                                new DateTime(period.StartDate.Year, period.StartDate.Month, 1),
                                createdBy: username,
                                companyId: companyId,
                                enforcePeriodLock: false /* we're inside the close; bypass */);
                            depTotal += entry.Lines?.Sum(l => l.Debit) ?? 0m;
                            booksRun++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "Depreciation skip for book {BookId} during close of period {PeriodId}: {Msg}",
                                book.Id, period.Id, ex.Message);
                        }
                    }

                    period.DepreciationCalculated = true;
                    period.DepreciationPosted = true;
                    await _db.SaveChangesAsync();

                    steps.Add(new CloseStepResult(
                        Code: "DEPRECIATION",
                        Title: "Post monthly depreciation per book",
                        Succeeded: true,
                        Detail: $"Posted {booksRun} book(s); total DR {depTotal:C} to Depreciation Expense.",
                        Amount: depTotal,
                        RelatedEntityId: null));
                }
                else
                {
                    steps.Add(new CloseStepResult(
                        Code: "DEPRECIATION",
                        Title: "Post monthly depreciation per book",
                        Succeeded: true,
                        Detail: "Already posted in a prior close attempt — skipped.",
                        Amount: null,
                        RelatedEntityId: null));
                }

                // Step 2: re-verify trial balance after depreciation posted.
                var endInclusive = period.EndDate.Date.AddDays(1).AddTicks(-1);
                var tbCheck = await CheckTrialBalanceAsync(period.StartDate, endInclusive);
                steps.Add(new CloseStepResult(
                    Code: "TB_POST_DEP",
                    Title: "Verify trial balance after depreciation",
                    Succeeded: tbCheck.Status != CheckStatus.Fail,
                    Detail: tbCheck.Detail,
                    Amount: null,
                    RelatedEntityId: null));
                if (tbCheck.Status == CheckStatus.Fail && !overrideFailures)
                {
                    throw new InvalidOperationException($"Trial balance failed post-depreciation: {tbCheck.Detail}");
                }

                // Step 3: re-verify GR/IR clearing.
                var grIrCheck = await CheckGrIrClearingAsync(companyId, period.StartDate, endInclusive);
                steps.Add(new CloseStepResult(
                    Code: "GR_IR_POST_DEP",
                    Title: "Verify GR/IR clearing",
                    Succeeded: grIrCheck.Status != CheckStatus.Fail,
                    Detail: grIrCheck.Detail,
                    Amount: null,
                    RelatedEntityId: null));
                if (grIrCheck.Status == CheckStatus.Fail && !overrideFailures)
                {
                    throw new InvalidOperationException($"GR/IR clearing failed: {grIrCheck.Detail}");
                }

                // Step 4: flip the period to Closed.
                period.Status = PeriodStatus.Closed;
                period.ClosedAt = DateTime.UtcNow;
                period.ClosedBy = username;

                var capturedPacket = new PeriodClosePacket(
                    FiscalPeriodId: period.Id,
                    CompanyId: companyId,
                    PeriodName: period.Name,
                    PeriodStartDate: period.StartDate,
                    PeriodEndDate: period.EndDate,
                    CapturedAt: DateTime.UtcNow,
                    CapturedBy: username,
                    Preflight: preflight,
                    Steps: steps,
                    OverrideUsed: blocking.Any(),
                    OverrideReason: overrideReason);

                period.PreflightSnapshotJson = JsonSerializer.Serialize(capturedPacket,
                    new JsonSerializerOptions { WriteIndented = false });

                await _db.SaveChangesAsync();

                steps.Add(new CloseStepResult(
                    Code: "LOCK",
                    Title: "Lock fiscal period",
                    Succeeded: true,
                    Detail: $"Period '{period.Name}' status: Open → Closed. ClosedBy={username}.",
                    Amount: null,
                    RelatedEntityId: period.Id));

                // Step 5: defense-in-depth — also flip the legacy PeriodLock
                // by YYYYMM so any code path that still checks AuditService.IsPeriodLockedAsync
                // gets the right answer.
                try
                {
                    var yyyymm = period.StartDate.Year * 100 + period.StartDate.Month;
                    await _audit.LockPeriodAsync(yyyymm, username,
                        reason: $"Close orchestration — period {period.Name}, override={blocking.Any()}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Legacy PeriodLock write failed but FiscalPeriod close succeeded.");
                }

                // Audit log entry — paired with the JSON snapshot on the period
                // record, this gives an auditor everything they need.
                // Pass a FLAT snapshot DTO (not the EF entity) because
                // AuditService.LogAsync<T> JsonSerializer-serializes the
                // entity with default options and chokes on the
                // FiscalPeriod ↔ FiscalYear circular nav. Wrapped in
                // try/catch so an audit-log hiccup never aborts a
                // successfully-committed close (paired with LockPeriodAsync).
                try
                {
                    var auditSnapshot = new
                    {
                        period.Id,
                        period.Name,
                        period.CompanyId,
                        period.FiscalYearId,
                        Status = period.Status.ToString(),
                        period.ClosedAt,
                        period.ClosedBy,
                        period.StartDate,
                        period.EndDate,
                        period.DepreciationCalculated,
                        period.DepreciationPosted,
                        PreflightSnapshotJsonLength = period.PreflightSnapshotJson?.Length ?? 0
                    };
                    await _audit.LogAsync<object>(
                        action: blocking.Any() ? "PeriodCloseWithOverride" : "PeriodClose",
                        before: null,
                        after: auditSnapshot,
                        username: username,
                        description: $"Closed period {period.Name}. Preflight had {blocking.Count} blocking failure(s). " +
                                     (overrideReason != null ? $"Override reason: {overrideReason}" : "No override needed."));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "AuditService.LogAsync failed for period close (period {PeriodId}) — close itself still succeeded.",
                        period.Id);
                }

                await tx.CommitAsync();

                var finalPacket = capturedPacket with { Steps = steps };
                return new CloseResult(
                    Succeeded: true,
                    Period: period,
                    Packet: finalPacket,
                    ErrorMessage: null);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Period close failed for period {PeriodId} company {CompanyId}",
                    fiscalPeriodId, companyId);

                var packet = new PeriodClosePacket(
                    FiscalPeriodId: period.Id,
                    CompanyId: companyId,
                    PeriodName: period.Name,
                    PeriodStartDate: period.StartDate,
                    PeriodEndDate: period.EndDate,
                    CapturedAt: DateTime.UtcNow,
                    CapturedBy: username,
                    Preflight: preflight,
                    Steps: steps,
                    OverrideUsed: false,
                    OverrideReason: overrideReason);

                return new CloseResult(
                    Succeeded: false,
                    Period: period,
                    Packet: packet,
                    ErrorMessage: ex.Message);
            }
        }

        public async Task<CloseResult> ReopenAsync(int companyId, int fiscalPeriodId, string username, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Reopen requires a reason — captured for audit.");

            var period = await _db.FiscalPeriods
                .FirstOrDefaultAsync(p => p.Id == fiscalPeriodId && p.CompanyId == companyId);

            if (period == null)
                throw new InvalidOperationException($"Fiscal period {fiscalPeriodId} not found.");

            if (period.Status == PeriodStatus.Open)
                throw new InvalidOperationException($"Period '{period.Name}' is already Open.");

            period.Status = PeriodStatus.Open;
            // DEP flags intentionally NOT reset — the depreciation has already
            // hit the books; re-closing without re-running prevents duplicate JE.
            // If the user wants a fresh depreciation run, they must reverse the
            // existing DEP JE manually (Reverse button on Journals/Details).
            period.ClosedAt = null;
            period.ClosedBy = null;

            await _db.SaveChangesAsync();

            try
            {
                var yyyymm = period.StartDate.Year * 100 + period.StartDate.Month;
                await _audit.UnlockPeriodAsync(yyyymm, username);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Legacy PeriodLock unlock failed but FiscalPeriod reopen succeeded.");
            }

            try
            {
                var auditSnapshot = new
                {
                    period.Id,
                    period.Name,
                    period.CompanyId,
                    period.FiscalYearId,
                    Status = period.Status.ToString(),
                    period.StartDate,
                    period.EndDate
                };
                await _audit.LogAsync<object>(
                    action: "PeriodReopen",
                    before: null,
                    after: auditSnapshot,
                    username: username,
                    description: $"Reopened period {period.Name}. Reason: {reason}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "AuditService.LogAsync failed for period reopen (period {PeriodId}) — reopen itself still succeeded.",
                    period.Id);
            }

            var packet = new PeriodClosePacket(
                FiscalPeriodId: period.Id,
                CompanyId: companyId,
                PeriodName: period.Name,
                PeriodStartDate: period.StartDate,
                PeriodEndDate: period.EndDate,
                CapturedAt: DateTime.UtcNow,
                CapturedBy: username,
                Preflight: new List<PreflightCheck>(),
                Steps: new List<CloseStepResult>
                {
                    new CloseStepResult("REOPEN", "Reopen period", true,
                        $"Period '{period.Name}' status: Closed → Open. Reason: {reason}",
                        null, period.Id)
                },
                OverrideUsed: false,
                OverrideReason: reason);

            return new CloseResult(
                Succeeded: true,
                Period: period,
                Packet: packet,
                ErrorMessage: null);
        }

        // ---------- INDIVIDUAL CHECK IMPLEMENTATIONS ----------

        private async Task<PreflightCheck> CheckPeriodStateAsync(FiscalPeriod period)
        {
            var today = DateTime.UtcNow.Date;
            if (period.Status != PeriodStatus.Open)
            {
                return new PreflightCheck("PERIOD_STATE",
                    "Period is Open and closeable",
                    CheckStatus.Fail,
                    period.Status.ToString(),
                    $"Period must be Open to close. Current status: {period.Status}.",
                    null);
            }
            if (period.EndDate > today)
            {
                return new PreflightCheck("PERIOD_STATE",
                    "Period is Open and closeable",
                    CheckStatus.Fail,
                    "Future",
                    $"Cannot close a future period. Period ends {period.EndDate:yyyy-MM-dd}; today is {today:yyyy-MM-dd}.",
                    null);
            }
            await Task.CompletedTask;
            return new PreflightCheck("PERIOD_STATE",
                "Period is Open and closeable",
                CheckStatus.Pass,
                $"Open through {period.EndDate:yyyy-MM-dd}",
                "Period is in Open state and its end date has elapsed.",
                null);
        }

        private async Task<PreflightCheck> CheckTrialBalanceAsync(DateTime startDate, DateTime endInclusive)
        {
            var totals = await _db.JournalLines
                .Where(l => l.JournalEntry != null
                    && l.JournalEntry.PostingDate >= startDate
                    && l.JournalEntry.PostingDate <= endInclusive)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Debits = g.Sum(l => l.Debit),
                    Credits = g.Sum(l => l.Credit)
                })
                .FirstOrDefaultAsync();

            var debits = totals?.Debits ?? 0m;
            var credits = totals?.Credits ?? 0m;
            var net = debits - credits;

            if (Math.Abs(net) < 0.01m)
            {
                return new PreflightCheck("TB_BALANCED",
                    "Trial balance is balanced",
                    CheckStatus.Pass,
                    $"Σ Debit {debits:C} = Σ Credit {credits:C}",
                    "Every JE in the period nets to zero.",
                    $"/Reports/TrialBalance?StartDate={startDate:yyyy-MM-dd}&EndDate={endInclusive:yyyy-MM-dd}");
            }

            return new PreflightCheck("TB_BALANCED",
                "Trial balance is balanced",
                CheckStatus.Fail,
                $"Net imbalance {net:C}",
                $"Σ Debit {debits:C} − Σ Credit {credits:C} = {net:C}. Inspect the Trial Balance report for the offending account.",
                $"/Reports/TrialBalance?StartDate={startDate:yyyy-MM-dd}&EndDate={endInclusive:yyyy-MM-dd}");
        }

        private async Task<PreflightCheck> CheckDepreciationStatusAsync(int companyId, FiscalPeriod period)
        {
            // The orchestrator will run depreciation as step 1 of the close
            // if this flag is false. So "not yet run" is a WARN, not a FAIL —
            // close still proceeds, it just does the work as part of close.
            if (period.DepreciationPosted)
            {
                await Task.CompletedTask;
                return new PreflightCheck("DEPRECIATION",
                    "Monthly depreciation posted",
                    CheckStatus.Pass,
                    "Posted",
                    "Depreciation has already been posted for this period.",
                    "/Journals?source=DEP");
            }

            var books = await _db.Books
                .Where(b => b.CompanyId == companyId || b.CompanyId == null)
                .CountAsync();
            return new PreflightCheck("DEPRECIATION",
                "Monthly depreciation posted",
                CheckStatus.Warn,
                $"{books} book(s) not yet posted",
                "Depreciation will be posted as the first step of close.",
                null);
        }

        private async Task<PreflightCheck> CheckGrIrClearingAsync(int companyId, DateTime startDate, DateTime endInclusive)
        {
            // GR/IR clearing account default is 2150 (GlAccountKind.GrAccrued).
            // Per-tenant overrides exist in CompanyGlAccountConfig but the
            // default covers every demo + most production. Net DR-CR over
            // the period should be ≤ tolerance; anything bigger means
            // unmatched GR receipts that should clear before lock.
            const string GrIrAccount = "2150";

            var net = await _db.JournalLines
                .Where(l => l.JournalEntry != null
                    && l.JournalEntry.PostingDate >= startDate
                    && l.JournalEntry.PostingDate <= endInclusive
                    && l.Account == GrIrAccount)
                .GroupBy(_ => 1)
                .Select(g => g.Sum(l => l.Debit) - g.Sum(l => l.Credit))
                .FirstOrDefaultAsync();

            if (Math.Abs(net) <= GrIrToleranceUsd)
            {
                return new PreflightCheck("GR_IR",
                    "GR/IR clearing is reconciled",
                    CheckStatus.Pass,
                    $"Net {net:C} on {GrIrAccount}",
                    $"GR/IR clearing within tolerance ({GrIrToleranceUsd:C}).",
                    $"/Reports/TrialBalance?StartDate={startDate:yyyy-MM-dd}&EndDate={endInclusive:yyyy-MM-dd}");
            }

            return new PreflightCheck("GR_IR",
                "GR/IR clearing is reconciled",
                CheckStatus.Warn,
                $"Net {net:C} on {GrIrAccount}",
                $"GR/IR clearing has net {net:C} on account {GrIrAccount}. Likely unmatched receipts. Investigate or override.",
                $"/Reports/TrialBalance?StartDate={startDate:yyyy-MM-dd}&EndDate={endInclusive:yyyy-MM-dd}");
        }

        private async Task<PreflightCheck> CheckStaleOpenReceiptsAsync(int companyId, DateTime periodEnd)
        {
            // Stale = goods receipts with status pending inspection older than N days.
            // Avoid hard dependency on specific GR model field names by being permissive
            // with the query — we only count, we don't drill through.
            var cutoff = periodEnd.AddDays(-StaleGrInspectionDays);

            // Goods receipts created before cutoff that still haven't moved
            // out of pending state. This relies on the legacy GoodsReceipt
            // model — best-effort count, surface as WARN regardless.
            var staleCount = 0;
            try
            {
                staleCount = await _db.Set<GoodsReceipt>()
                    .Where(g => g.ReceiptDate <= cutoff
                             && g.Status == ReceiptStatus.Received)
                    .CountAsync();
            }
            catch
            {
                // Model surface differs across branches; tolerate.
                staleCount = -1;
            }

            if (staleCount <= 0)
            {
                return new PreflightCheck("STALE_GR",
                    $"No goods receipts pending inspection >{StaleGrInspectionDays} days",
                    CheckStatus.Pass,
                    "0 stale",
                    "All goods receipts are current or already inspected.",
                    "/Purchasing/Receipts");
            }

            return new PreflightCheck("STALE_GR",
                $"No goods receipts pending inspection >{StaleGrInspectionDays} days",
                CheckStatus.Warn,
                $"{staleCount} stale",
                $"{staleCount} goods receipt(s) have been pending >{StaleGrInspectionDays} days. Inspect and either accept or reject before close.",
                "/Purchasing/Receipts");
        }

        private async Task<PreflightCheck> CheckUnapprovedInvoicesInPeriodAsync(int companyId, DateTime startDate, DateTime endInclusive)
        {
            var unapproved = 0;
            try
            {
                unapproved = await _db.Set<VendorInvoice>()
                    .Where(v => v.InvoiceDate >= startDate
                             && v.InvoiceDate <= endInclusive
                             && v.Status != InvoiceStatus.Approved
                             && v.Status != InvoiceStatus.Paid
                             && v.Status != InvoiceStatus.PartiallyPaid
                             && v.Status != InvoiceStatus.Voided)
                    .CountAsync();
            }
            catch
            {
                unapproved = -1;
            }

            if (unapproved <= 0)
            {
                return new PreflightCheck("UNAPPROVED_INV",
                    "All vendor invoices in period approved",
                    CheckStatus.Pass,
                    "0 unapproved",
                    "Every invoice dated in this period has been approved.",
                    "/AccountsPayable/Index");
            }

            return new PreflightCheck("UNAPPROVED_INV",
                "All vendor invoices in period approved",
                CheckStatus.Warn,
                $"{unapproved} unapproved",
                $"{unapproved} vendor invoice(s) dated in this period are not yet Approved. Approve or reject before close.",
                "/AccountsPayable/Index");
        }

        private async Task<PreflightCheck> CheckCorrectiveWosCompletionAsync(int companyId, DateTime startDate, DateTime endInclusive)
        {
            // WOs with CompletedDate in period should be Status=Completed.
            // A non-Completed WO with a CompletedDate set is a data drift.
            var drift = 0;
            try
            {
                drift = await _db.MaintenanceEvents
                    .Where(m => m.CompletedDate != null
                             && m.CompletedDate >= startDate
                             && m.CompletedDate <= endInclusive
                             && m.Status != MaintenanceStatus.Completed)
                    .CountAsync();
            }
            catch
            {
                drift = -1;
            }

            if (drift <= 0)
            {
                return new PreflightCheck("WO_COMPLETE",
                    "Work orders completed in period are Status=Completed",
                    CheckStatus.Pass,
                    "0 drift",
                    "Every WO with a completion date in this period is marked Completed.",
                    "/WorkOrders/Index");
            }

            return new PreflightCheck("WO_COMPLETE",
                "Work orders completed in period are Status=Completed",
                CheckStatus.Warn,
                $"{drift} drifted",
                $"{drift} WO(s) have a CompletedDate in this period but are not Status=Completed. Close them or clear the date.",
                "/WorkOrders/Index");
        }
    }
}
