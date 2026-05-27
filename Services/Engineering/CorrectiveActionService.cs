// Sprint 14.3 PR-6 (2026-05-27) — Corrective Action service implementation.
// xmin concurrency via MapXminRowVersion at the AppDbContext level.
// Full 8D lifecycle with automatic DaysToClose computation at closure.

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Engineering
{
    public class CorrectiveActionService : ICorrectiveActionService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<CorrectiveActionService> _log;

        public CorrectiveActionService(AppDbContext db, ILogger<CorrectiveActionService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<Result<CorrectiveActionRequest>> CreateAsync(
            CreateCarRequest req, CancellationToken ct = default)
        {
            var exists = await _db.Set<CorrectiveActionRequest>()
                .AnyAsync(c => c.CompanyId == req.CompanyId && c.CarNumber == req.CarNumber, ct);
            if (exists)
                return Result.Failure<CorrectiveActionRequest>(
                    $"CAR '{req.CarNumber}' already exists in company {req.CompanyId}.");

            var car = new CorrectiveActionRequest
            {
                CompanyId = req.CompanyId,
                CarNumber = req.CarNumber,
                Title = req.Title,
                Source = req.Source,
                Severity = req.Severity,
                Status = CarStatus.Draft,
                ItemId = req.ItemId,
                ProductionOrderId = req.ProductionOrderId,
                CustomerId = req.CustomerId,
                VendorId = req.VendorId,
                OriginatingEcrId = req.OriginatingEcrId,
                RelatedDeviationId = req.RelatedDeviationId,
                RelatedConcessionId = req.RelatedConcessionId,
                NcrReference = req.NcrReference,
                CustomerComplaintReference = req.CustomerComplaintReference,
                AuditFindingReference = req.AuditFindingReference,
                NonConformanceDescription = req.NonConformanceDescription,
                AffectedQuantity = req.AffectedQuantity,
                AffectedLotSerials = req.AffectedLotSerials,
                AffectsForm = req.AffectsForm,
                AffectsFit = req.AffectsFit,
                AffectsFunction = req.AffectsFunction,
                SafetyImpact = req.SafetyImpact,
                RegulatoryImpact = req.RegulatoryImpact,
                Description = req.Description,
                CreatedBy = req.CreatedBy,
            };

            _db.Set<CorrectiveActionRequest>().Add(car);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CAR {Number} created (Id={Id}, Source={Source}, Severity={Severity})",
                car.CarNumber, car.Id, car.Source, car.Severity);
            return Result.Success(car);
        }

        public async Task<Result<CorrectiveActionRequest>> IssueAsync(
            int carId, string issuedBy, string? assignedTo = null,
            string? responsibleDepartment = null, CancellationToken ct = default)
        {
            var car = await Find(carId, ct);
            if (car is null) return NotFound();
            if (car.Status != CarStatus.Draft)
                return Fail($"Cannot issue — status is {car.Status}, expected Draft.");

            car.Status = CarStatus.Issued;
            car.IssuedBy = issuedBy;
            car.IssuedAtUtc = DateTime.UtcNow;
            car.AssignedTo = assignedTo;
            car.ResponsibleDepartment = responsibleDepartment;
            Stamp(car, issuedBy);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CAR {Number} issued by {User}, assigned to {Assignee}",
                car.CarNumber, issuedBy, assignedTo);
            return Result.Success(car);
        }

        public async Task<Result<CorrectiveActionRequest>> BeginInvestigationAsync(
            int carId, string investigator, string? containmentAction = null,
            CancellationToken ct = default)
        {
            var car = await Find(carId, ct);
            if (car is null) return NotFound();
            if (car.Status != CarStatus.Issued)
                return Fail($"Cannot begin investigation — status is {car.Status}, expected Issued.");

            car.Status = CarStatus.UnderInvestigation;
            car.AssignedTo = investigator;
            car.ContainmentAction = containmentAction;
            if (containmentAction != null) car.ContainmentCompletedAtUtc = DateTime.UtcNow;
            Stamp(car, investigator);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CAR {Number} investigation begun by {User}", car.CarNumber, investigator);
            return Result.Success(car);
        }

        public async Task<Result<CorrectiveActionRequest>> RecordRootCauseAsync(
            int carId, string rootCauseAnalysis, string methodology, string identifiedBy,
            CancellationToken ct = default)
        {
            var car = await Find(carId, ct);
            if (car is null) return NotFound();
            if (car.Status != CarStatus.UnderInvestigation)
                return Fail($"Cannot record root cause — status is {car.Status}, expected UnderInvestigation.");

            car.Status = CarStatus.RootCauseIdentified;
            car.RootCauseAnalysis = rootCauseAnalysis;
            car.RootCauseMethodology = methodology;
            car.RootCauseIdentifiedBy = identifiedBy;
            car.RootCauseIdentifiedAtUtc = DateTime.UtcNow;
            Stamp(car, identifiedBy);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CAR {Number} root cause identified via {Method} by {User}",
                car.CarNumber, methodology, identifiedBy);
            return Result.Success(car);
        }

        public async Task<Result<CorrectiveActionRequest>> PlanCorrectiveActionAsync(
            int carId, string correctiveActionPlan, string? preventiveActionPlan = null,
            DateTime? dueDate = null, CancellationToken ct = default)
        {
            var car = await Find(carId, ct);
            if (car is null) return NotFound();
            if (car.Status != CarStatus.RootCauseIdentified)
                return Fail($"Cannot plan corrective action — status is {car.Status}, expected RootCauseIdentified.");

            car.Status = CarStatus.CorrectiveActionPlanned;
            car.CorrectiveActionPlan = correctiveActionPlan;
            car.PreventiveActionPlan = preventiveActionPlan;
            car.CorrectiveActionDueDate = dueDate;
            Stamp(car);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CAR {Number} corrective action planned (due={Due})",
                car.CarNumber, dueDate?.ToString("u") ?? "no date");
            return Result.Success(car);
        }

        public async Task<Result<CorrectiveActionRequest>> BeginImplementationAsync(
            int carId, string implementedBy, CancellationToken ct = default)
        {
            var car = await Find(carId, ct);
            if (car is null) return NotFound();
            if (car.Status != CarStatus.CorrectiveActionPlanned)
                return Fail($"Cannot begin implementation — status is {car.Status}, expected CorrectiveActionPlanned.");

            car.Status = CarStatus.ImplementationInProgress;
            car.ImplementedBy = implementedBy;
            Stamp(car, implementedBy);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CAR {Number} implementation begun by {User}", car.CarNumber, implementedBy);
            return Result.Success(car);
        }

        public async Task<Result<CorrectiveActionRequest>> CompleteImplementationAsync(
            int carId, string implementationNotes, string implementedBy,
            CancellationToken ct = default)
        {
            var car = await Find(carId, ct);
            if (car is null) return NotFound();
            if (car.Status != CarStatus.ImplementationInProgress)
                return Fail($"Cannot complete implementation — status is {car.Status}, expected ImplementationInProgress.");

            car.Status = CarStatus.VerificationPending;
            car.ImplementationNotes = implementationNotes;
            car.ImplementedBy = implementedBy;
            car.ImplementationCompletedAtUtc = DateTime.UtcNow;
            Stamp(car, implementedBy);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CAR {Number} implementation completed by {User}", car.CarNumber, implementedBy);
            return Result.Success(car);
        }

        public async Task<Result<CorrectiveActionRequest>> VerifyAndCloseAsync(
            int carId, string verificationMethod, string verificationResults,
            bool effective, string verifiedBy, CancellationToken ct = default)
        {
            var car = await Find(carId, ct);
            if (car is null) return NotFound();
            if (car.Status != CarStatus.VerificationPending)
                return Fail($"Cannot verify — status is {car.Status}, expected VerificationPending.");

            car.Status = CarStatus.Closed;
            car.VerificationMethod = verificationMethod;
            car.VerificationResults = verificationResults;
            car.VerificationEffective = effective;
            car.VerifiedBy = verifiedBy;
            car.VerifiedAtUtc = DateTime.UtcNow;
            car.ClosedBy = verifiedBy;
            car.ClosedAtUtc = DateTime.UtcNow;

            // Compute days to close from issue date
            if (car.IssuedAtUtc.HasValue)
                car.DaysToClose = (int)(DateTime.UtcNow - car.IssuedAtUtc.Value).TotalDays;

            Stamp(car, verifiedBy);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CAR {Number} closed by {User} — effective={Effective}, days={Days}",
                car.CarNumber, verifiedBy, effective, car.DaysToClose);
            return Result.Success(car);
        }

        public async Task<Result<CorrectiveActionRequest>> CancelAsync(
            int carId, string cancelledBy, string reason, CancellationToken ct = default)
        {
            var car = await Find(carId, ct);
            if (car is null) return NotFound();
            if (car.Status == CarStatus.Closed || car.Status == CarStatus.Cancelled)
                return Fail($"Cannot cancel — status is {car.Status} (terminal).");

            car.Status = CarStatus.Cancelled;
            car.ImplementationNotes = (car.ImplementationNotes ?? "") + $"\n[CANCELLED by {cancelledBy}: {reason}]";
            Stamp(car, cancelledBy);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CAR {Number} cancelled by {User}: {Reason}", car.CarNumber, cancelledBy, reason);
            return Result.Success(car);
        }

        public async Task<CorrectiveActionRequest?> GetAsync(int carId, CancellationToken ct = default)
            => await _db.Set<CorrectiveActionRequest>()
                .Include(c => c.Item)
                .Include(c => c.ProductionOrder)
                .Include(c => c.Customer)
                .Include(c => c.Vendor)
                .Include(c => c.OriginatingEcr)
                .Include(c => c.RelatedDeviation)
                .Include(c => c.RelatedConcession)
                .FirstOrDefaultAsync(c => c.Id == carId, ct);

        public async Task<IReadOnlyList<CorrectiveActionRequest>> GetForItemAsync(
            int itemId, bool includeClosed = false, CancellationToken ct = default)
        {
            var query = _db.Set<CorrectiveActionRequest>().Where(c => c.ItemId == itemId);
            if (!includeClosed)
                query = query.Where(c => c.Status != CarStatus.Closed && c.Status != CarStatus.Cancelled);
            return await query.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        }

        private async Task<CorrectiveActionRequest?> Find(int id, CancellationToken ct)
            => await _db.Set<CorrectiveActionRequest>().FindAsync(new object[] { id }, ct);

        private static Result<CorrectiveActionRequest> NotFound()
            => Result.Failure<CorrectiveActionRequest>("CAR not found.");

        private static Result<CorrectiveActionRequest> Fail(string msg)
            => Result.Failure<CorrectiveActionRequest>(msg);

        private static void Stamp(CorrectiveActionRequest car, string? by = null)
        {
            car.UpdatedAt = DateTime.UtcNow;
            if (by != null) car.UpdatedBy = by;
        }
    }
}
