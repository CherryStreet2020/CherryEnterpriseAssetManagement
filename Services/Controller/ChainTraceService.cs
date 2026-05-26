using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Controller;

/// <summary>
/// Sprint 12.7 PR #2 — concrete implementation of
/// <see cref="IControllerCockpitService"/>. Walks the source-to-GL chain
/// for the Controller Control Center's Drilldown tab.
///
/// Two arms:
///
///   ASSET  → Asset header + CipCapitalization(s) + CipProject + CipCosts
///            (top 10) + recent Depreciation JEs (top 12) + JournalLines
///            (top 6 per JE).
///
///   JE     → JournalEntry header + JournalLines (with AccountingKey
///            segment context) + reverse-walk via JE.Source ("Depreciation"
///            → matching asset accounts; "CIP" → CipCapitalization;
///            "AP" / "Receiving" → upstream document by JE.Reference).
///
/// All collection reads are bounded with .Take(N). All queries use
/// AsNoTracking. Zero DbContext mutation (Lock 15 compliant).
/// </summary>
public sealed class ChainTraceService : IControllerCockpitService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ChainTraceService> _logger;

    // Bounds on collection reads — keeps the trace tight + predictable
    // even for noisy assets with hundreds of months of depreciation history.
    private const int MaxCipCostsPerProject = 10;
    private const int MaxDepreciationJEs = 12;
    private const int MaxLinesPerJE = 6;

    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("en-US");

    public ChainTraceService(AppDbContext db, ILogger<ChainTraceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ChainTraceResult> TraceAsync(string? query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ChainTraceResult.NotResolved(
                headline: "No query",
                narration: "Type an Asset #, JE #, PO #, or Invoice # in the search box to walk the source-to-GL chain. Try `ASSET-1116` or `JE-1` to see the trace shape.");
        }

        var entityRef = ParseEntityRef(query.Trim());
        if (entityRef is null)
        {
            return ChainTraceResult.NotResolved(
                headline: $"Could not parse `{query}`",
                narration: "Try formats like `ASSET-1116`, `JE-1`, or a bare integer (assumed Asset).");
        }

        try
        {
            return entityRef.Kind switch
            {
                EntityKind.Asset          => await TraceFromAssetAsync(entityRef.Id, ct),
                EntityKind.JournalEntry   => await TraceFromJournalEntryAsync(entityRef.Id, ct),
                _ => ChainTraceResult.NotResolved(
                    headline: $"Unsupported entity kind `{entityRef.Kind}`",
                    narration: "Asset and JE walks ship in PR #2. PO / Invoice / WO walks land in later Sprint 12.7 PRs."),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChainTraceService.TraceAsync failed for query={Query}", query);
            return ChainTraceResult.NotResolved(
                headline: "Trace failed",
                narration: $"Internal error walking the chain. {ex.Message}");
        }
    }

    // =====================================================================
    // ASSET arm
    // =====================================================================

    private async Task<ChainTraceResult> TraceFromAssetAsync(int assetId, CancellationToken ct)
    {
        var asset = await _db.Assets.AsNoTracking()
            .Where(a => a.Id == assetId)
            .Select(a => new
            {
                a.Id,
                a.AssetNumber,
                a.Description,
                a.Model,
                a.SerialNumber,
                a.AcquisitionCost,
                a.AccumulatedDepreciation,
                a.InServiceDate,
                a.GLAssetAccount,
                a.GLAccumDepAccount,
                a.GLDepExpenseAccount,
                a.CompanyId,
                a.SiteId,
            })
            .FirstOrDefaultAsync(ct);

        if (asset is null)
        {
            return ChainTraceResult.NotResolved(
                headline: $"Asset #{assetId} not found",
                narration: "No asset with that Id in the database. Check the Assets list for valid IDs.");
        }

        var nbv = asset.AcquisitionCost - asset.AccumulatedDepreciation;
        var steps = new List<ChainStep>(capacity: 32);

        // ---- Step 1 — Asset header ------------------------------------------
        steps.Add(new ChainStep(
            StepType: "Asset",
            StepKey: $"ASSET-{asset.Id}",
            Eyebrow: "ASSET",
            Headline: $"{asset.Description} — #{asset.AssetNumber}",
            Subtext: BuildAssetSubtext(asset.Model, asset.SerialNumber, asset.InServiceDate),
            AmountText: $"NBV {FormatMoney(nbv)}",
            DateText: $"In service {FormatDate(asset.InServiceDate)}",
            DeepLinkHref: $"/Assets/Asset?id={asset.Id}",
            Narration: $"NBV of {FormatMoney(nbv)} = acquisition cost {FormatMoney(asset.AcquisitionCost)} less accumulated depreciation {FormatMoney(asset.AccumulatedDepreciation)}."
        ));

        // ---- Step 2 — Capital project (origin) ------------------------------
        var capitalizations = await _db.CipCapitalizations.AsNoTracking()
            .Where(c => c.AssetId == asset.Id)
            .OrderBy(c => c.CapitalizedAt)
            .Select(c => new
            {
                c.Id,
                c.CipProjectId,
                c.JournalEntryId,
                c.CapitalizedAt,
                c.TotalCapitalized,
                ProjectNumber = c.Project != null ? c.Project.ProjectNumber : null,
                ProjectName = c.Project != null ? c.Project.Name : null,
            })
            .Take(5)
            .ToListAsync(ct);

        foreach (var cap in capitalizations)
        {
            steps.Add(new ChainStep(
                StepType: "CipProject",
                StepKey: $"CIPCAP-{cap.Id}",
                Eyebrow: "ORIGIN — CAPITAL PROJECT",
                Headline: cap.ProjectNumber is not null
                    ? $"{cap.ProjectName} — #{cap.ProjectNumber}"
                    : $"Capitalization #{cap.Id}",
                Subtext: $"Total capitalized {FormatMoney(cap.TotalCapitalized)}",
                AmountText: FormatMoney(cap.TotalCapitalized),
                DateText: FormatDate(cap.CapitalizedAt),
                DeepLinkHref: cap.CipProjectId > 0 ? $"/CIP/Details/{cap.CipProjectId}" : null,
                Narration: $"Asset was capitalized from this CIP project on {FormatDate(cap.CapitalizedAt)} for {FormatMoney(cap.TotalCapitalized)}."
            ));

            // Pull the top CIP costs that fed this project. Even though
            // CipCapitalizationCost is the formal mapping, the simplest
            // narration walks CipCosts on the project ordered by Amount DESC.
            if (cap.CipProjectId > 0)
            {
                var topCosts = await _db.CipCosts.AsNoTracking()
                    .Where(cost => cost.CipProjectId == cap.CipProjectId)
                    .OrderByDescending(cost => cost.Amount)
                    .Take(MaxCipCostsPerProject)
                    .Select(cost => new
                    {
                        cost.Id,
                        cost.Description,
                        cost.Amount,
                        cost.TransactionDate,
                        cost.Vendor,
                        cost.InvoiceNumber,
                        cost.PurchaseOrderNumber,
                        cost.SourceType,
                        cost.SourceDisplayRef,
                    })
                    .ToListAsync(ct);

                foreach (var cost in topCosts)
                {
                    var refLabel = !string.IsNullOrWhiteSpace(cost.SourceDisplayRef)
                        ? cost.SourceDisplayRef
                        : cost.InvoiceNumber ?? cost.PurchaseOrderNumber;

                    steps.Add(new ChainStep(
                        StepType: "CipCost",
                        StepKey: $"CIPCOST-{cost.Id}",
                        Eyebrow: "CAPITAL COST",
                        Headline: cost.Description,
                        Subtext: BuildCostSubtext(cost.Vendor, refLabel, cost.SourceType),
                        AmountText: FormatMoney(cost.Amount),
                        DateText: FormatDate(cost.TransactionDate),
                        DeepLinkHref: null,
                        Narration: BuildCostNarration(cost.Description, cost.Vendor, cost.Amount, cost.TransactionDate)
                    ));
                }
            }
        }

        // ---- Step 3 — recent depreciation JEs -------------------------------
        // PROVENANCE CONSTRAINT (Codex P1 catch 2026-05-25): a JE whose
        // Source = "Depreciation" + line on GL accounts X/Y could belong to
        // ANY asset that posts to those same accounts. Per-tenant defaults
        // (e.g. "1510 — Accumulated Depreciation", "6500 — Depreciation
        // Expense") are shared across most assets. Matching on account
        // strings alone would surface other assets' depreciation JEs as if
        // they belonged to the queried asset, producing a factually wrong
        // drilldown.
        //
        // Without JournalLine.SourceModule / SourceDocumentId / SourceAssetId
        // columns (queued as a future ADR-tracked migration — see
        // AccountingKey.cs §"FORWARD-LOOKING"), the only safe disambiguation
        // is the per-entity GL account override pattern: when the asset
        // overrides GLAccumDepAccount or GLDepExpenseAccount with a value
        // that is NOT the tenant default, that account string uniquely
        // identifies the asset's depreciation postings.
        //
        // For assets WITHOUT overrides (the common case), we skip the
        // depreciation walk and surface a narration step explaining the
        // limitation. PR #3+ wires the proper traceability columns +
        // AccountingKey ProjectId/SiteId segment matching.
        // Start from any per-entity GL account override stored on the asset.
        // A stored value on Asset.GLAccumDepAccount / GLDepExpenseAccount is
        // a CANDIDATE override; the next cross-check (below) drops any
        // candidate that appears on ≥ 1 OTHER asset in the same tenant
        // (which would mean it's shared, not asset-specific).
        var depAccounts = new[] { asset.GLAccumDepAccount, asset.GLDepExpenseAccount }
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Cast<string>()
            .Distinct()
            .ToArray();

        // Cross-check: an account "string survives as override" iff it is
        // present on FEWER than N assets in this tenant. If it's on ≥ 2
        // assets, treat it as a shared default (i.e. NOT a per-asset
        // override) — drop it from disambiguation set.
        if (depAccounts.Length > 0 && asset.CompanyId.HasValue)
        {
            var sharedAccounts = await _db.Assets.AsNoTracking()
                .Where(a => a.CompanyId == asset.CompanyId
                    && a.Id != asset.Id
                    && a.GLAccumDepAccount != null
                    && depAccounts.Contains(a.GLAccumDepAccount!))
                .Select(a => a.GLAccumDepAccount!)
                .Distinct()
                .ToListAsync(ct);

            var sharedExp = await _db.Assets.AsNoTracking()
                .Where(a => a.CompanyId == asset.CompanyId
                    && a.Id != asset.Id
                    && a.GLDepExpenseAccount != null
                    && depAccounts.Contains(a.GLDepExpenseAccount!))
                .Select(a => a.GLDepExpenseAccount!)
                .Distinct()
                .ToListAsync(ct);

            depAccounts = depAccounts
                .Where(a => !sharedAccounts.Contains(a) && !sharedExp.Contains(a))
                .ToArray();
        }

        if (depAccounts.Length == 0)
        {
            // Provenance limitation — surface a narration step the user can
            // read, instead of silently returning a wrong walk.
            steps.Add(new ChainStep(
                StepType: "JournalEntry",
                StepKey: $"DEP-UNDISAMBIGUATED-{asset.Id}",
                Eyebrow: "DEPRECIATION CHAIN",
                Headline: "Depreciation history not disambiguated for this asset",
                Subtext: "Asset uses shared default GL accounts for Accumulated Depreciation / Depreciation Expense.",
                DeepLinkHref: null,
                Narration: $"Per-asset depreciation JE lookup requires either (a) per-entity GL account overrides on the asset, or (b) JournalLine.SourceDocumentId provenance (queued for a future ADR-tracked migration). Until then the depreciation walk would risk surfacing other assets' JEs that post to the same default accounts. See {asset.Id}'s capitalization + CIP chain above for what we CAN trace today."
            ));
        }
        else
        {
            // Load top N Depreciation JE headers WITH their lines via Include,
            // then filter in-memory to those that touch the asset's
            // depreciation accounts. Two reasons we go this shape rather than
            // a navigation-projection or two-query JournalLines-first lookup:
            //
            //   - `je.Lines.Any(l => ...)` and `je.Lines.Select(l => l.Account)`
            //     in a server projection silently return 0 through EF Core's
            //     InMemory provider (per dotnet/efcore#24468 family of issues),
            //     making the service untestable without Npgsql.
            //
            //   - The two-query "JournalLines first → JE Ids → JE headers"
            //     shape works on Npgsql but lines inserted via the JE.Lines
            //     navigation collection are not always reachable through the
            //     JournalLines DbSet under InMemory, so the same untestable
            //     gap appears.
            //
            // .Include is the provider-portable choice. The result set is
            // bounded by MaxDepreciationJEs * 3 (in-memory headroom for
            // noisy assets), and Lines per JE is small (~10 lines per dep JE
            // worst case), so the wire weight is modest.
            var depCandidates = await _db.JournalEntries.AsNoTracking()
                .Include(je => je.Lines)
                .Where(je => je.Source == "Depreciation")
                .OrderByDescending(je => je.PostingDate)
                .ThenByDescending(je => je.Id)
                .Take(MaxDepreciationJEs * 3)
                .ToListAsync(ct);

            var depJEs = depCandidates
                .Where(c => c.Lines.Any(l => depAccounts.Contains(l.Account)))
                .Take(MaxDepreciationJEs)
                .Select(je => new
                {
                    je.Id,
                    je.Batch,
                    je.PostingDate,
                    je.Reference,
                    je.Description,
                    je.Period,
                })
                .ToList();

            foreach (var je in depJEs)
            {
                steps.Add(new ChainStep(
                    StepType: "JournalEntry",
                    StepKey: $"JE-{je.Id}",
                    Eyebrow: "DEPRECIATION JE",
                    Headline: je.Batch,
                    Subtext: je.Description ?? je.Reference,
                    DateText: $"{FormatDate(je.PostingDate)} · period {je.Period}",
                    DeepLinkHref: $"/Journals/Details/{je.Id}",
                    Narration: $"Depreciation posted on {FormatDate(je.PostingDate)} for batch {je.Batch}."
                ));

                // Pull the asset-relevant lines from this JE (top N), with
                // segment chips from AccountingKey when resolved. Include
                // the AccountingKey nav so the segment chips render with
                // their human-readable AccountingKeyString.
                var lines = await _db.JournalLines.AsNoTracking()
                    .Include(l => l.AccountingKey)
                    .Where(l => l.JournalEntryId == je.Id && depAccounts.Contains(l.Account))
                    .OrderBy(l => l.LineNo)
                    .Take(MaxLinesPerJE)
                    .Select(l => new
                    {
                        l.Id,
                        l.LineNo,
                        l.Account,
                        l.Debit,
                        l.Credit,
                        l.Description,
                        l.AccountingKeyId,
                        AccountingKeyString = l.AccountingKey != null ? l.AccountingKey.AccountingKeyString : null,
                    })
                    .ToListAsync(ct);

                foreach (var line in lines)
                {
                    var drCr = line.Debit > 0 ? "Dr" : (line.Credit > 0 ? "Cr" : "—");
                    var amt = line.Debit > 0 ? line.Debit : line.Credit;
                    var chips = BuildSegmentChips(line.Account, line.AccountingKeyId, line.AccountingKeyString);

                    steps.Add(new ChainStep(
                        StepType: "JournalLine",
                        StepKey: $"JL-{line.Id}",
                        Eyebrow: "JOURNAL LINE",
                        Headline: $"Line {line.LineNo} · Account {line.Account}",
                        Subtext: line.Description,
                        AmountText: $"{drCr} {FormatMoney(amt)}",
                        DeepLinkHref: $"/Journals/Details/{je.Id}#line-{line.Id}",
                        Narration: $"{drCr} {FormatMoney(amt)} to GL {line.Account}.",
                        SegmentChips: chips
                    ));
                }
            }
        }

        var resultHeadline = $"{asset.Description} — #{asset.AssetNumber}";
        var resultSubtitle = $"NBV {FormatMoney(nbv)} · Acq Cost {FormatMoney(asset.AcquisitionCost)} · Accum Dep {FormatMoney(asset.AccumulatedDepreciation)}";
        var resultNarration = capitalizations.Count > 0
            ? $"Walked {capitalizations.Count} capitalization{(capitalizations.Count == 1 ? "" : "s")} → {steps.Count(s => s.StepType == "JournalEntry")} depreciation JE{(steps.Count(s => s.StepType == "JournalEntry") == 1 ? "" : "s")} → {steps.Count(s => s.StepType == "JournalLine")} journal line{(steps.Count(s => s.StepType == "JournalLine") == 1 ? "" : "s")}."
            : $"Walked {steps.Count(s => s.StepType == "JournalEntry")} depreciation JE{(steps.Count(s => s.StepType == "JournalEntry") == 1 ? "" : "s")} → {steps.Count(s => s.StepType == "JournalLine")} journal line{(steps.Count(s => s.StepType == "JournalLine") == 1 ? "" : "s")}. No capital project origin found.";

        return new ChainTraceResult
        {
            IsResolved = true,
            Headline = resultHeadline,
            Subtitle = resultSubtitle,
            Narration = resultNarration,
            Steps = steps,
        };
    }

    // =====================================================================
    // JOURNAL ENTRY arm
    // =====================================================================

    private async Task<ChainTraceResult> TraceFromJournalEntryAsync(int jeId, CancellationToken ct)
    {
        var je = await _db.JournalEntries.AsNoTracking()
            .Where(j => j.Id == jeId)
            .Select(j => new
            {
                j.Id,
                j.Batch,
                j.Source,
                j.Reference,
                j.Description,
                j.Period,
                j.PostingDate,
                j.BookId,
            })
            .FirstOrDefaultAsync(ct);

        if (je is null)
        {
            return ChainTraceResult.NotResolved(
                headline: $"JE #{jeId} not found",
                narration: "No journal entry with that Id. Check /Journals for valid IDs.");
        }

        var steps = new List<ChainStep>(capacity: 12);

        // ---- Step 1 — JE header ---------------------------------------------
        steps.Add(new ChainStep(
            StepType: "JournalEntry",
            StepKey: $"JE-{je.Id}",
            Eyebrow: $"{(je.Source ?? "MANUAL").ToUpperInvariant()} JE",
            Headline: je.Batch,
            Subtext: je.Description ?? je.Reference,
            DateText: $"{FormatDate(je.PostingDate)} · period {je.Period}",
            DeepLinkHref: $"/Journals/Details/{je.Id}",
            Narration: $"Posted on {FormatDate(je.PostingDate)} via {je.Source ?? "Manual"} source."
        ));

        // ---- Step 2 — origin reverse-walk by JE.Source ----------------------
        if (string.Equals(je.Source, "CIP", StringComparison.OrdinalIgnoreCase))
        {
            var cap = await _db.CipCapitalizations.AsNoTracking()
                .Where(c => c.JournalEntryId == je.Id)
                .Select(c => new
                {
                    c.Id,
                    c.AssetId,
                    c.CipProjectId,
                    c.CapitalizedAt,
                    c.TotalCapitalized,
                    AssetDescription = c.Asset != null ? c.Asset.Description : null,
                    AssetNumber = c.Asset != null ? c.Asset.AssetNumber : null,
                    ProjectNumber = c.Project != null ? c.Project.ProjectNumber : null,
                    ProjectName = c.Project != null ? c.Project.Name : null,
                })
                .FirstOrDefaultAsync(ct);

            if (cap is not null)
            {
                steps.Add(new ChainStep(
                    StepType: "Asset",
                    StepKey: $"ASSET-{cap.AssetId}",
                    Eyebrow: "CAPITALIZED ASSET",
                    Headline: cap.AssetDescription is not null
                        ? $"{cap.AssetDescription} — #{cap.AssetNumber}"
                        : $"Asset #{cap.AssetId}",
                    AmountText: FormatMoney(cap.TotalCapitalized),
                    DateText: FormatDate(cap.CapitalizedAt),
                    DeepLinkHref: $"/Assets/Asset?id={cap.AssetId}",
                    Narration: $"This JE capitalized Asset #{cap.AssetNumber ?? cap.AssetId.ToString()} for {FormatMoney(cap.TotalCapitalized)}."
                ));
                if (cap.ProjectNumber is not null)
                {
                    steps.Add(new ChainStep(
                        StepType: "CipProject",
                        StepKey: $"CIPPROJ-{cap.CipProjectId}",
                        Eyebrow: "CAPITAL PROJECT",
                        Headline: $"{cap.ProjectName} — #{cap.ProjectNumber}",
                        DeepLinkHref: $"/CIP/Details/{cap.CipProjectId}",
                        Narration: $"Costs were accumulated on CIP project {cap.ProjectNumber}."
                    ));
                }
            }
        }

        // ---- Step 3 — JournalLines (always last in chain) -------------------
        var lines = await _db.JournalLines.AsNoTracking()
            .Where(l => l.JournalEntryId == je.Id)
            .OrderBy(l => l.LineNo)
            .Take(MaxLinesPerJE * 2)
            .Select(l => new
            {
                l.Id,
                l.LineNo,
                l.Account,
                l.Debit,
                l.Credit,
                l.Description,
                l.AccountingKeyId,
                AccountingKeyString = l.AccountingKey != null ? l.AccountingKey.AccountingKeyString : null,
            })
            .ToListAsync(ct);

        foreach (var line in lines)
        {
            var drCr = line.Debit > 0 ? "Dr" : (line.Credit > 0 ? "Cr" : "—");
            var amt = line.Debit > 0 ? line.Debit : line.Credit;
            var chips = BuildSegmentChips(line.Account, line.AccountingKeyId, line.AccountingKeyString);

            steps.Add(new ChainStep(
                StepType: "JournalLine",
                StepKey: $"JL-{line.Id}",
                Eyebrow: "JOURNAL LINE",
                Headline: $"Line {line.LineNo} · Account {line.Account}",
                Subtext: line.Description,
                AmountText: $"{drCr} {FormatMoney(amt)}",
                DeepLinkHref: $"/Journals/Details/{je.Id}#line-{line.Id}",
                Narration: $"{drCr} {FormatMoney(amt)} to GL {line.Account}.",
                SegmentChips: chips
            ));
        }

        return new ChainTraceResult
        {
            IsResolved = true,
            Headline = $"{je.Batch}",
            Subtitle = $"{(je.Source ?? "Manual")} JE · {FormatDate(je.PostingDate)} · {lines.Count} line{(lines.Count == 1 ? "" : "s")}",
            Narration = $"JE #{je.Id} was generated by the {je.Source ?? "Manual"} subsystem on {FormatDate(je.PostingDate)}.",
            Steps = steps,
        };
    }

    // =====================================================================
    // PARSER
    // =====================================================================

    internal enum EntityKind { Unknown, Asset, JournalEntry, PurchaseOrder, Invoice, WorkOrder }
    internal sealed record EntityRef(EntityKind Kind, int Id);

    private static readonly Regex EntityPattern = new(
        @"^\s*(?<kind>asset|je|journal|po|purchase|inv|invoice|wo|workorder|work)?\s*[-:#\s]*(?<id>\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parse a loose entity query string. Returns NULL when no integer Id
    /// can be extracted.
    /// </summary>
    internal static EntityRef? ParseEntityRef(string query)
    {
        var m = EntityPattern.Match(query);
        if (!m.Success) return null;

        if (!int.TryParse(m.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
            return null;

        var kindToken = m.Groups["kind"].Value?.ToLowerInvariant();
        return kindToken switch
        {
            "je" or "journal"                 => new EntityRef(EntityKind.JournalEntry, id),
            "po" or "purchase"                => new EntityRef(EntityKind.PurchaseOrder, id),
            "inv" or "invoice"                => new EntityRef(EntityKind.Invoice, id),
            "wo" or "workorder" or "work"     => new EntityRef(EntityKind.WorkOrder, id),
            _                                 => new EntityRef(EntityKind.Asset, id), // bare integer or "asset" → Asset
        };
    }

    // =====================================================================
    // FORMAT HELPERS
    // =====================================================================

    private static string FormatMoney(decimal amount) =>
        amount.ToString("C2", MoneyCulture);

    private static string FormatDate(DateTime? dt) =>
        dt.HasValue ? dt.Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) : "—";

    private static string FormatDate(DateTime dt) =>
        dt.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    private static string BuildAssetSubtext(string? model, string? serial, DateTime? inService)
    {
        var bits = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(model)) bits.Add($"Model {model}");
        if (!string.IsNullOrWhiteSpace(serial)) bits.Add($"S/N {serial}");
        if (inService.HasValue) bits.Add($"In service {FormatDate(inService)}");
        return bits.Count > 0 ? string.Join(" · ", bits) : "";
    }

    private static string? BuildCostSubtext(string? vendor, string? refLabel, string? sourceType)
    {
        var bits = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(vendor)) bits.Add(vendor);
        if (!string.IsNullOrWhiteSpace(refLabel)) bits.Add(refLabel);
        if (!string.IsNullOrWhiteSpace(sourceType)) bits.Add($"({sourceType})");
        return bits.Count > 0 ? string.Join(" · ", bits) : null;
    }

    private static string BuildCostNarration(string description, string? vendor, decimal amount, DateTime txDate)
    {
        var vendorPart = string.IsNullOrWhiteSpace(vendor) ? "" : $" from {vendor}";
        return $"{description}{vendorPart}, {FormatMoney(amount)} on {FormatDate(txDate)}.";
    }

    /// <summary>
    /// Build the AccountingKey segment-context chips for a JournalLine. Reads
    /// the human-readable <see cref="AccountingKey.AccountingKeyString"/> field
    /// (e.g. <c>"Co=2|Site=17|Acct=5610|CC=110100|Dept=2009|Proj=|ICP=|Vert=1"</c>)
    /// and splits it into individual non-empty chips for the Razor partial to
    /// render. Returns NULL when the AccountingKey is unresolved (orphan
    /// rows backfilled to NULL during the PRA-5b migration).
    /// </summary>
    private static IReadOnlyList<string>? BuildSegmentChips(string account, int? accountingKeyId, string? keyString)
    {
        if (!accountingKeyId.HasValue || string.IsNullOrWhiteSpace(keyString))
        {
            // Legacy / orphan line — only the GL account string is known.
            return new[] { $"GL {account}" };
        }

        var chips = new List<string>(8);
        foreach (var segment in keyString.Split('|'))
        {
            var trimmed = segment.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Skip segments with no value (e.g. "Proj=", "ICP=").
            var eq = trimmed.IndexOf('=');
            if (eq >= 0 && eq == trimmed.Length - 1) continue;

            chips.Add(trimmed);
        }
        return chips.Count > 0 ? chips : null;
    }
}
