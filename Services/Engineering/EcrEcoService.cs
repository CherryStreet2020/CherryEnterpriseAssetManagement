// Sprint 14.3 PR-1 (2026-05-27) — EcrEcoService impl.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Engineering;

public sealed class EcrEcoService : IEcrEcoService
{
    private readonly AppDbContext _db;
    private readonly IDocumentService _docSvc;
    private readonly ILogger<EcrEcoService> _logger;

    public EcrEcoService(AppDbContext db, IDocumentService docSvc, ILogger<EcrEcoService> logger)
    {
        _db = db;
        _docSvc = docSvc;
        _logger = logger;
    }

    // ---------- ECR -----------------------------------------------------

    public async Task<Result<EngineeringChangeRequest>> CreateEcrAsync(
        int companyId,
        string ecrNumber,
        string title,
        string? description,
        ChangeReason changeReason,
        ChangeUrgency urgency,
        bool affectsForm,
        bool affectsFit,
        bool affectsFunction,
        bool affectsSafety,
        bool affectsCustomers,
        bool affectsRegulatory,
        int? linkedItemId,
        int? linkedDocumentId,
        int? linkedProductionOrderId,
        int? linkedCustomerId,
        string requestedBy,
        CancellationToken ct)
    {
        if (companyId <= 0) return Result.Failure<EngineeringChangeRequest>("CompanyId must be > 0.");
        if (string.IsNullOrWhiteSpace(ecrNumber)) return Result.Failure<EngineeringChangeRequest>("EcrNumber is required.");
        if (string.IsNullOrWhiteSpace(title)) return Result.Failure<EngineeringChangeRequest>("Title is required.");
        if (string.IsNullOrWhiteSpace(requestedBy)) return Result.Failure<EngineeringChangeRequest>("RequestedBy is required.");

        // Normalize EcrNumber per the DMS-style normalization rule (PR #366
        // Codex P1 lesson): AppDbContext uppercases on SaveChanges, so the
        // service-side uniqueness check uses the same normalized form.
        var normalizedNumber = ecrNumber.Trim().ToUpperInvariant();

        var existing = await _db.EngineeringChangeRequests.AsNoTracking()
            .FirstOrDefaultAsync(e => e.CompanyId == companyId && e.EcrNumber == normalizedNumber, ct);
        if (existing != null)
        {
            return Result.Failure<EngineeringChangeRequest>(
                $"ECR {normalizedNumber} already exists for CompanyId {companyId} (Id={existing.Id}). Pick a different EcrNumber.");
        }

        // Tenant validation on linked references — refuse cross-tenant links.
        if (linkedItemId.HasValue)
        {
            var item = await _db.Items.AsNoTracking()
                .Where(i => i.Id == linkedItemId.Value)
                .Select(i => new { i.Id, i.CompanyId })
                .FirstOrDefaultAsync(ct);
            if (item == null) return Result.Failure<EngineeringChangeRequest>($"LinkedItem {linkedItemId} not found.");
            if (item.CompanyId.HasValue && item.CompanyId.Value != companyId)
            {
                return Result.Failure<EngineeringChangeRequest>(
                    $"Cross-tenant link refused: LinkedItem {linkedItemId} belongs to CompanyId {item.CompanyId.Value} but ECR is for CompanyId {companyId}.");
            }
        }
        if (linkedDocumentId.HasValue)
        {
            var doc = await _db.Documents.AsNoTracking()
                .Where(d => d.Id == linkedDocumentId.Value)
                .Select(d => new { d.Id, d.CompanyId })
                .FirstOrDefaultAsync(ct);
            if (doc == null) return Result.Failure<EngineeringChangeRequest>($"LinkedDocument {linkedDocumentId} not found.");
            if (doc.CompanyId != companyId)
            {
                return Result.Failure<EngineeringChangeRequest>(
                    $"Cross-tenant link refused: LinkedDocument {linkedDocumentId} belongs to CompanyId {doc.CompanyId} but ECR is for CompanyId {companyId}.");
            }
        }
        if (linkedProductionOrderId.HasValue)
        {
            var pro = await _db.ProductionOrders.AsNoTracking()
                .Where(p => p.Id == linkedProductionOrderId.Value)
                .Select(p => new { p.Id, p.CompanyId })
                .FirstOrDefaultAsync(ct);
            if (pro == null) return Result.Failure<EngineeringChangeRequest>($"LinkedProductionOrder {linkedProductionOrderId} not found.");
            if (pro.CompanyId != companyId)
            {
                return Result.Failure<EngineeringChangeRequest>(
                    $"Cross-tenant link refused: LinkedProductionOrder {linkedProductionOrderId} belongs to CompanyId {pro.CompanyId} but ECR is for CompanyId {companyId}.");
            }
        }
        // Codex P2 fix (PR #367): validate LinkedCustomer tenant ownership.
        // Customer rows are company-scoped; without this check an ECR for
        // tenant 1 could attach a customer from tenant 2, leaking into the
        // downstream customer-impact/notice workflow when AffectsCustomers=true.
        // Customer.CompanyId is non-nullable int (not nullable like Item),
        // so a direct equality check is sufficient.
        if (linkedCustomerId.HasValue)
        {
            var cust = await _db.Customers.AsNoTracking()
                .Where(c => c.Id == linkedCustomerId.Value)
                .Select(c => new { c.Id, c.CompanyId })
                .FirstOrDefaultAsync(ct);
            if (cust == null) return Result.Failure<EngineeringChangeRequest>($"LinkedCustomer {linkedCustomerId} not found.");
            if (cust.CompanyId != companyId)
            {
                return Result.Failure<EngineeringChangeRequest>(
                    $"Cross-tenant link refused: LinkedCustomer {linkedCustomerId} belongs to CompanyId {cust.CompanyId} but ECR is for CompanyId {companyId}.");
            }
        }

        var now = DateTime.UtcNow;
        var ecr = new EngineeringChangeRequest
        {
            CompanyId = companyId,
            EcrNumber = normalizedNumber,
            Title = title,
            Description = description,
            ChangeReason = changeReason,
            Urgency = urgency,
            Status = EcrStatus.Draft,
            AffectsForm = affectsForm,
            AffectsFit = affectsFit,
            AffectsFunction = affectsFunction,
            AffectsSafety = affectsSafety,
            AffectsCustomers = affectsCustomers,
            AffectsRegulatory = affectsRegulatory,
            LinkedItemId = linkedItemId,
            LinkedDocumentId = linkedDocumentId,
            LinkedProductionOrderId = linkedProductionOrderId,
            LinkedCustomerId = linkedCustomerId,
            RequestedBy = requestedBy,
            RequestedAtUtc = now,
            CreatedAt = now,
            CreatedBy = requestedBy,
        };
        _db.EngineeringChangeRequests.Add(ecr);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "EcrEcoService.CreateEcrAsync: created ECR {EcrId} '{Number}' Reason={Reason} Urgency={Urgency} F/F/F={F}/{Fit}/{Fn} by {By}.",
            ecr.Id, ecr.EcrNumber, ecr.ChangeReason, ecr.Urgency, affectsForm, affectsFit, affectsFunction, requestedBy);

        return Result.Success(ecr);
    }

    public async Task<Result<EngineeringChangeRequest>> SubmitEcrAsync(int ecrId, string submittedBy, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(submittedBy)) return Result.Failure<EngineeringChangeRequest>("SubmittedBy is required.");

        var ecr = await _db.EngineeringChangeRequests.FirstOrDefaultAsync(e => e.Id == ecrId, ct);
        if (ecr == null) return Result.Failure<EngineeringChangeRequest>($"ECR {ecrId} not found.");
        if (ecr.Status != EcrStatus.Draft)
        {
            return Result.Failure<EngineeringChangeRequest>(
                $"ECR {ecrId} is in status {ecr.Status}; only Draft ECRs can be submitted.");
        }

        ecr.Status = EcrStatus.Submitted;
        ecr.SubmittedAtUtc = DateTime.UtcNow;
        ecr.UpdatedAt = DateTime.UtcNow;
        ecr.UpdatedBy = submittedBy;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("EcrEcoService.SubmitEcrAsync: ECR {EcrId} submitted by {By}.", ecrId, submittedBy);
        return Result.Success(ecr);
    }

    public async Task<Result<EcrApprovalResult>> ApproveEcrAndCreateEcoAsync(
        int ecrId,
        string ecoNumber,
        string ecoTitle,
        EcoEffectivityType effectivityType,
        DateTime? effectiveFromUtc,
        string? effectivitySerialFrom,
        string? effectivitySerialTo,
        string? effectivityLotFrom,
        string? effectivityLotTo,
        int? effectivityProductionOrderId,
        string approvedBy,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ecoNumber)) return Result.Failure<EcrApprovalResult>("EcoNumber is required.");
        if (string.IsNullOrWhiteSpace(ecoTitle)) return Result.Failure<EcrApprovalResult>("EcoTitle is required.");
        if (string.IsNullOrWhiteSpace(approvedBy)) return Result.Failure<EcrApprovalResult>("ApprovedBy is required.");

        var ecr = await _db.EngineeringChangeRequests.FirstOrDefaultAsync(e => e.Id == ecrId, ct);
        if (ecr == null) return Result.Failure<EcrApprovalResult>($"ECR {ecrId} not found.");
        if (ecr.Status != EcrStatus.Submitted && ecr.Status != EcrStatus.UnderReview)
        {
            return Result.Failure<EcrApprovalResult>(
                $"ECR {ecrId} is in status {ecr.Status}; only Submitted or UnderReview ECRs can be approved.");
        }

        var normalizedEcoNumber = ecoNumber.Trim().ToUpperInvariant();
        var existing = await _db.EngineeringChangeOrders.AsNoTracking()
            .FirstOrDefaultAsync(e => e.CompanyId == ecr.CompanyId && e.EcoNumber == normalizedEcoNumber, ct);
        if (existing != null)
        {
            return Result.Failure<EcrApprovalResult>(
                $"ECO {normalizedEcoNumber} already exists for CompanyId {ecr.CompanyId} (Id={existing.Id}). Pick a different EcoNumber.");
        }

        var now = DateTime.UtcNow;
        var eco = new EngineeringChangeOrder
        {
            CompanyId = ecr.CompanyId,
            LocationId = ecr.LocationId,
            EcoNumber = normalizedEcoNumber,
            Title = ecoTitle,
            Description = ecr.Description,
            SourceEcr = ecr, // EF nav-property pattern for atomic FK fixup
            Urgency = ecr.Urgency,
            Status = EcoStatus.Draft,
            EffectivityType = effectivityType,
            EffectiveFromUtc = effectiveFromUtc,
            EffectivitySerialFrom = effectivitySerialFrom,
            EffectivitySerialTo = effectivitySerialTo,
            EffectivityLotFrom = effectivityLotFrom,
            EffectivityLotTo = effectivityLotTo,
            EffectivityProductionOrderId = effectivityProductionOrderId,
            RequiresFaiRetrigger = ecr.AffectsForm || ecr.AffectsFit || ecr.AffectsFunction,
            RequiresCustomerNotice = ecr.AffectsCustomers,
            RequiresRegulatoryNotice = ecr.AffectsRegulatory,
            CreatedAt = now,
            CreatedBy = approvedBy,
        };
        _db.EngineeringChangeOrders.Add(eco);

        ecr.Status = EcrStatus.Approved;
        ecr.DecidedAtUtc = now;
        ecr.DecidedBy = approvedBy;
        ecr.ResultingEco = eco; // EF nav-property fixup pattern (PR-FS-6 lesson)
        ecr.UpdatedAt = now;
        ecr.UpdatedBy = approvedBy;

        // Single SaveChangesAsync — atomic ECR approval + ECO creation.
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "EcrEcoService.ApproveEcrAndCreateEcoAsync: ECR {EcrId} approved by {By} → ECO {EcoId} '{EcoNumber}' (Urgency={Urg}, EffectivityType={Eff}, RequiresFAI={Fai}, RequiresCustomerNotice={Cust}).",
            ecrId, approvedBy, eco.Id, eco.EcoNumber, eco.Urgency, eco.EffectivityType, eco.RequiresFaiRetrigger, eco.RequiresCustomerNotice);

        return Result.Success(new EcrApprovalResult(ecr, eco));
    }

    public async Task<Result<EngineeringChangeRequest>> RejectEcrAsync(int ecrId, string rejectionReason, string rejectedBy, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason)) return Result.Failure<EngineeringChangeRequest>("RejectionReason is required.");
        if (string.IsNullOrWhiteSpace(rejectedBy)) return Result.Failure<EngineeringChangeRequest>("RejectedBy is required.");

        var ecr = await _db.EngineeringChangeRequests.FirstOrDefaultAsync(e => e.Id == ecrId, ct);
        if (ecr == null) return Result.Failure<EngineeringChangeRequest>($"ECR {ecrId} not found.");
        if (ecr.Status != EcrStatus.Submitted && ecr.Status != EcrStatus.UnderReview)
        {
            return Result.Failure<EngineeringChangeRequest>(
                $"ECR {ecrId} is in status {ecr.Status}; only Submitted or UnderReview ECRs can be rejected.");
        }

        var now = DateTime.UtcNow;
        ecr.Status = EcrStatus.Rejected;
        ecr.RejectionReason = rejectionReason;
        ecr.DecidedAtUtc = now;
        ecr.DecidedBy = rejectedBy;
        ecr.UpdatedAt = now;
        ecr.UpdatedBy = rejectedBy;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("EcrEcoService.RejectEcrAsync: ECR {EcrId} rejected by {By}. Reason: {Reason}", ecrId, rejectedBy, rejectionReason);
        return Result.Success(ecr);
    }

    // ---------- ECO -----------------------------------------------------

    public async Task<Result<EcoLineItem>> AddEcoLineItemAsync(
        int ecoId,
        int? affectedItemId,
        int? affectedDocumentId,
        int? affectedDocumentVersionId,
        int? newDocumentVersionId,
        string? changeDescription,
        string? beforeValue,
        string? afterValue,
        EcoLineItemDisposition disposition,
        string? notes,
        string createdBy,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(createdBy)) return Result.Failure<EcoLineItem>("CreatedBy is required.");

        var eco = await _db.EngineeringChangeOrders.FirstOrDefaultAsync(e => e.Id == ecoId, ct);
        if (eco == null) return Result.Failure<EcoLineItem>($"ECO {ecoId} not found.");
        if (eco.Status != EcoStatus.Draft && eco.Status != EcoStatus.InApproval)
        {
            return Result.Failure<EcoLineItem>(
                $"ECO {ecoId} is in status {eco.Status}; line items can only be added while Draft or InApproval.");
        }

        // Tenant validation on each affected FK.
        if (affectedItemId.HasValue)
        {
            var item = await _db.Items.AsNoTracking().Where(i => i.Id == affectedItemId.Value).Select(i => new { i.Id, i.CompanyId }).FirstOrDefaultAsync(ct);
            if (item == null) return Result.Failure<EcoLineItem>($"AffectedItem {affectedItemId} not found.");
            if (item.CompanyId.HasValue && item.CompanyId.Value != eco.CompanyId)
                return Result.Failure<EcoLineItem>($"Cross-tenant: AffectedItem {affectedItemId} CompanyId={item.CompanyId.Value} vs ECO CompanyId={eco.CompanyId}.");
        }
        if (affectedDocumentId.HasValue)
        {
            var doc = await _db.Documents.AsNoTracking().Where(d => d.Id == affectedDocumentId.Value).Select(d => new { d.Id, d.CompanyId }).FirstOrDefaultAsync(ct);
            if (doc == null) return Result.Failure<EcoLineItem>($"AffectedDocument {affectedDocumentId} not found.");
            if (doc.CompanyId != eco.CompanyId)
                return Result.Failure<EcoLineItem>($"Cross-tenant: AffectedDocument {affectedDocumentId} CompanyId={doc.CompanyId} vs ECO CompanyId={eco.CompanyId}.");
        }
        // Codex P1 fix (PR #367): validate DocumentVersion tenants by walking
        // to their parent Document's CompanyId. Without this, an ECO in
        // tenant 1 can attach a version from tenant 2 via either FK and the
        // downstream ReleaseEcoAsync flow would supersede the foreign-tenant
        // version through IDocumentService — a tenant-leak P0 in disguise.
        if (affectedDocumentVersionId.HasValue)
        {
            var ver = await _db.DocumentVersions.AsNoTracking()
                .Where(v => v.Id == affectedDocumentVersionId.Value)
                .Select(v => new { v.Id, v.CompanyId, v.DocumentId })
                .FirstOrDefaultAsync(ct);
            if (ver == null) return Result.Failure<EcoLineItem>($"AffectedDocumentVersion {affectedDocumentVersionId} not found.");
            if (ver.CompanyId != eco.CompanyId)
                return Result.Failure<EcoLineItem>($"Cross-tenant: AffectedDocumentVersion {affectedDocumentVersionId} CompanyId={ver.CompanyId} vs ECO CompanyId={eco.CompanyId}.");
            // Also enforce that, when AffectedDocumentId is also supplied,
            // the version belongs to that exact document (catches an attacker
            // supplying a same-tenant doc id + a foreign version id).
            if (affectedDocumentId.HasValue && ver.DocumentId != affectedDocumentId.Value)
                return Result.Failure<EcoLineItem>($"AffectedDocumentVersion {affectedDocumentVersionId} belongs to Document {ver.DocumentId}, not the supplied AffectedDocument {affectedDocumentId}.");
        }
        if (newDocumentVersionId.HasValue)
        {
            var newVer = await _db.DocumentVersions.AsNoTracking()
                .Where(v => v.Id == newDocumentVersionId.Value)
                .Select(v => new { v.Id, v.CompanyId, v.DocumentId })
                .FirstOrDefaultAsync(ct);
            if (newVer == null) return Result.Failure<EcoLineItem>($"NewDocumentVersion {newDocumentVersionId} not found.");
            if (newVer.CompanyId != eco.CompanyId)
                return Result.Failure<EcoLineItem>($"Cross-tenant: NewDocumentVersion {newDocumentVersionId} CompanyId={newVer.CompanyId} vs ECO CompanyId={eco.CompanyId}.");
            if (affectedDocumentId.HasValue && newVer.DocumentId != affectedDocumentId.Value)
                return Result.Failure<EcoLineItem>($"NewDocumentVersion {newDocumentVersionId} belongs to Document {newVer.DocumentId}, not the supplied AffectedDocument {affectedDocumentId}.");
        }

        // Auto-advance Sequence to MAX+10.
        var maxSequence = await _db.EcoLineItems.Where(l => l.EcoId == ecoId).MaxAsync(l => (int?)l.Sequence, ct) ?? 0;
        var nextSequence = maxSequence + 10;

        var line = new EcoLineItem
        {
            EcoId = ecoId,
            CompanyId = eco.CompanyId,
            LocationId = eco.LocationId,
            Sequence = nextSequence,
            AffectedItemId = affectedItemId,
            AffectedDocumentId = affectedDocumentId,
            AffectedDocumentVersionId = affectedDocumentVersionId,
            NewDocumentVersionId = newDocumentVersionId,
            ChangeDescription = changeDescription,
            BeforeValue = beforeValue,
            AfterValue = afterValue,
            Disposition = disposition,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
        _db.EcoLineItems.Add(line);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "EcrEcoService.AddEcoLineItemAsync: line {LineId} on ECO {EcoId} Seq={Seq} AffectedItem={AItem} AffectedDoc={ADoc} NewDocVer={NewVer} Disposition={Disp} by {By}.",
            line.Id, ecoId, nextSequence, affectedItemId, affectedDocumentId, newDocumentVersionId, disposition, createdBy);

        return Result.Success(line);
    }

    public async Task<Result<EcoApproval>> AddEcoApprovalStageAsync(
        int ecoId,
        int stageOrder,
        string approvalRole,
        string? requiredApprover,
        string createdBy,
        CancellationToken ct)
    {
        if (stageOrder <= 0) return Result.Failure<EcoApproval>("StageOrder must be > 0.");
        if (string.IsNullOrWhiteSpace(approvalRole)) return Result.Failure<EcoApproval>("ApprovalRole is required.");
        if (string.IsNullOrWhiteSpace(createdBy)) return Result.Failure<EcoApproval>("CreatedBy is required.");

        var eco = await _db.EngineeringChangeOrders.FirstOrDefaultAsync(e => e.Id == ecoId, ct);
        if (eco == null) return Result.Failure<EcoApproval>($"ECO {ecoId} not found.");
        if (eco.Status != EcoStatus.Draft && eco.Status != EcoStatus.InApproval)
        {
            return Result.Failure<EcoApproval>(
                $"ECO {ecoId} is in status {eco.Status}; approval stages can only be added while Draft or InApproval.");
        }

        // Idempotent on (EcoId, StageOrder).
        var existing = await _db.EcoApprovals.FirstOrDefaultAsync(a => a.EcoId == ecoId && a.StageOrder == stageOrder, ct);
        if (existing != null)
        {
            _logger.LogInformation("EcrEcoService.AddEcoApprovalStageAsync: stage already exists for ECO {EcoId} StageOrder {Stage}; returning existing (Id={Id}).", ecoId, stageOrder, existing.Id);
            return Result.Success(existing);
        }

        var stage = new EcoApproval
        {
            EcoId = ecoId,
            CompanyId = eco.CompanyId,
            StageOrder = stageOrder,
            ApprovalRole = approvalRole,
            RequiredApprover = requiredApprover,
            Status = EcoApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
        _db.EcoApprovals.Add(stage);

        // First stage added flips ECO from Draft → InApproval.
        if (eco.Status == EcoStatus.Draft)
        {
            eco.Status = EcoStatus.InApproval;
            eco.UpdatedAt = DateTime.UtcNow;
            eco.UpdatedBy = createdBy;
        }
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("EcrEcoService.AddEcoApprovalStageAsync: stage {Id} added to ECO {EcoId} Order={Order} Role='{Role}' by {By}.", stage.Id, ecoId, stageOrder, approvalRole, createdBy);
        return Result.Success(stage);
    }

    public async Task<Result<EcoApproval>> ApproveEcoStageAsync(int approvalId, string approvedBy, string? decisionNotes, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(approvedBy)) return Result.Failure<EcoApproval>("ApprovedBy is required.");

        var stage = await _db.EcoApprovals.FirstOrDefaultAsync(a => a.Id == approvalId, ct);
        if (stage == null) return Result.Failure<EcoApproval>($"EcoApproval {approvalId} not found.");
        if (stage.Status != EcoApprovalStatus.Pending)
        {
            return Result.Failure<EcoApproval>($"EcoApproval {approvalId} is in status {stage.Status}; only Pending stages can be approved.");
        }

        // Enforce in-order approval — fail if any earlier stage is still Pending or Rejected.
        var earlierBlockers = await _db.EcoApprovals
            .Where(a => a.EcoId == stage.EcoId
                     && a.StageOrder < stage.StageOrder
                     && (a.Status == EcoApprovalStatus.Pending || a.Status == EcoApprovalStatus.Rejected))
            .Select(a => new { a.StageOrder, a.Status })
            .ToListAsync(ct);
        if (earlierBlockers.Any())
        {
            var summary = string.Join(", ", earlierBlockers.Select(b => $"Stage {b.StageOrder}={b.Status}"));
            return Result.Failure<EcoApproval>($"Earlier approval stage(s) not yet Approved/Skipped: {summary}. Decide earlier stages first.");
        }

        var now = DateTime.UtcNow;
        stage.Status = EcoApprovalStatus.Approved;
        stage.DecidedAtUtc = now;
        stage.DecidedBy = approvedBy;
        stage.DecisionNotes = decisionNotes;

        // Check if ALL non-Skipped/NotRequired stages on this ECO are now Approved.
        // If yes, flip ECO from InApproval → Approved.
        var allStages = await _db.EcoApprovals.Where(a => a.EcoId == stage.EcoId).ToListAsync(ct);
        var stillBlocking = allStages.Any(a =>
            a.Id != stage.Id &&
            a.Status != EcoApprovalStatus.Approved &&
            a.Status != EcoApprovalStatus.Skipped &&
            a.Status != EcoApprovalStatus.NotRequired);
        if (!stillBlocking)
        {
            var eco = await _db.EngineeringChangeOrders.FirstOrDefaultAsync(e => e.Id == stage.EcoId, ct);
            if (eco != null && eco.Status == EcoStatus.InApproval)
            {
                eco.Status = EcoStatus.Approved;
                eco.ApprovedAtUtc = now;
                eco.ApprovedBy = approvedBy;
                eco.UpdatedAt = now;
                eco.UpdatedBy = approvedBy;
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "EcrEcoService.ApproveEcoStageAsync: stage {Id} on ECO {EcoId} approved by {By}. ECO fully approved: {AllDone}.",
            approvalId, stage.EcoId, approvedBy, !stillBlocking);

        return Result.Success(stage);
    }

    public async Task<Result<EngineeringChangeOrder>> ReleaseEcoAsync(
        int ecoId,
        string releasedBy,
        DateTime? effectiveFromUtc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(releasedBy)) return Result.Failure<EngineeringChangeOrder>("ReleasedBy is required.");

        var eco = await _db.EngineeringChangeOrders
            .Include(e => e.LineItems!)
            .FirstOrDefaultAsync(e => e.Id == ecoId, ct);
        if (eco == null) return Result.Failure<EngineeringChangeOrder>($"ECO {ecoId} not found.");
        if (eco.Status != EcoStatus.Approved)
        {
            return Result.Failure<EngineeringChangeOrder>(
                $"ECO {ecoId} is in status {eco.Status}; only Approved ECOs can be released.");
        }

        var now = DateTime.UtcNow;
        var effective = effectiveFromUtc ?? now;

        // Codex P1 fix (PR #367): make ECO release ALL-OR-NOTHING via an
        // explicit DbContextTransaction. The original implementation
        // delegated to IDocumentService.ReleaseVersionAsync per line item,
        // and that method internally calls SaveChangesAsync — so partial
        // commits could land if a later line failed (header + earlier
        // releases would persist, leaving the system in a half-released
        // state). Wrapping the whole flow in a transaction makes it true
        // all-or-nothing on relational providers. On InMemory the
        // transaction is a no-op (warning suppressed via ConfigureWarnings
        // in test setup); behavior is best-effort there but PG/MSSQL get
        // real atomicity.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // First flip the document versions (each ReleaseVersionAsync
            // calls SaveChanges internally — within the transaction those
            // become deferred-commit on relational providers).
            var supersededCount = 0;
            foreach (var line in eco.LineItems ?? Enumerable.Empty<EcoLineItem>())
            {
                if (line.NewDocumentVersionId.HasValue)
                {
                    var releaseResult = await _docSvc.ReleaseVersionAsync(line.NewDocumentVersionId.Value, releasedBy, effective, ct);
                    if (releaseResult.IsFailure)
                    {
                        // Rollback so no partial DocumentVersion releases land.
                        await tx.RollbackAsync(ct);
                        return Result.Failure<EngineeringChangeOrder>(
                            $"ECO {ecoId} release blocked: DocumentVersion {line.NewDocumentVersionId.Value} on line {line.Id} failed: {releaseResult.Error}");
                    }
                    supersededCount++;
                }
            }

            // Now mark the ECO Released + stamp metadata.
            eco.Status = EcoStatus.Released;
            eco.ReleasedAtUtc = now;
            eco.ReleasedBy = releasedBy;
            eco.EffectiveFromUtc = effective;
            eco.UpdatedAt = now;
            eco.UpdatedBy = releasedBy;
            await _db.SaveChangesAsync(ct);

            // Commit the transaction — all DocumentVersion supersedes + ECO
            // status flip become visible together.
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "EcrEcoService.ReleaseEcoAsync: ECO {EcoId} '{Number}' released by {By} at {EffFrom}. {Count} DocumentVersions superseded atomically.",
                ecoId, eco.EcoNumber, releasedBy, effective, supersededCount);

            return Result.Success(eco);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<EngineeringChangeOrder>> ImplementEcoAsync(int ecoId, string implementedBy, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(implementedBy)) return Result.Failure<EngineeringChangeOrder>("ImplementedBy is required.");

        var eco = await _db.EngineeringChangeOrders.FirstOrDefaultAsync(e => e.Id == ecoId, ct);
        if (eco == null) return Result.Failure<EngineeringChangeOrder>($"ECO {ecoId} not found.");
        if (eco.Status != EcoStatus.Released)
        {
            return Result.Failure<EngineeringChangeOrder>(
                $"ECO {ecoId} is in status {eco.Status}; only Released ECOs can be implemented.");
        }

        eco.Status = EcoStatus.Implemented;
        eco.ImplementedAtUtc = DateTime.UtcNow;
        eco.ImplementedBy = implementedBy;
        eco.UpdatedAt = DateTime.UtcNow;
        eco.UpdatedBy = implementedBy;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("EcrEcoService.ImplementEcoAsync: ECO {EcoId} marked Implemented by {By}.", ecoId, implementedBy);
        return Result.Success(eco);
    }

    public async Task<Result<EngineeringChangeOrder>> CloseEcoAsync(int ecoId, string closedBy, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(closedBy)) return Result.Failure<EngineeringChangeOrder>("ClosedBy is required.");

        var eco = await _db.EngineeringChangeOrders.FirstOrDefaultAsync(e => e.Id == ecoId, ct);
        if (eco == null) return Result.Failure<EngineeringChangeOrder>($"ECO {ecoId} not found.");
        if (eco.Status != EcoStatus.Implemented)
        {
            return Result.Failure<EngineeringChangeOrder>(
                $"ECO {ecoId} is in status {eco.Status}; only Implemented ECOs can be closed.");
        }

        eco.Status = EcoStatus.Closed;
        eco.ClosedAtUtc = DateTime.UtcNow;
        eco.ClosedBy = closedBy;
        eco.UpdatedAt = DateTime.UtcNow;
        eco.UpdatedBy = closedBy;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("EcrEcoService.CloseEcoAsync: ECO {EcoId} closed by {By}.", ecoId, closedBy);
        return Result.Success(eco);
    }

    // ---------- Reads ---------------------------------------------------

    public Task<EngineeringChangeRequest?> GetEcrAsync(int ecrId, CancellationToken ct)
    {
        return _db.EngineeringChangeRequests.AsNoTracking()
            .Include(e => e.ResultingEco)
            .FirstOrDefaultAsync(e => e.Id == ecrId, ct);
    }

    public Task<EngineeringChangeOrder?> GetEcoAsync(int ecoId, CancellationToken ct)
    {
        return _db.EngineeringChangeOrders.AsNoTracking()
            .Include(e => e.LineItems)
            .Include(e => e.Approvals)
            .Include(e => e.SourceEcr)
            .FirstOrDefaultAsync(e => e.Id == ecoId, ct);
    }
}
