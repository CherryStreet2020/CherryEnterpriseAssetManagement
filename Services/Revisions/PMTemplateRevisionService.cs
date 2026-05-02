// TENANT SCOPING NOTE: PMTemplateRevision does not have a CompanyId column.
// Scoping is enforced through the parent PMTemplate entity which has CompanyId.
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Revisions
{
    public interface IPMTemplateRevisionService
    {
        Task<PMTemplateRevision> CreateDraftFromTemplateAsync(int pmTemplateId, string? changeReason, string? userId);
        Task<PMTemplateRevision> CreateDraftFromRevisionAsync(int revisionId, string? changeReason, string? userId);
        Task<PMTemplateRevision> ReleaseRevisionAsync(int revisionId, string? approvedByUserId);
        Task ObsoleteRevisionAsync(int revisionId);
        Task<PMTemplateRevision?> GetCurrentReleasedRevisionAsync(int pmTemplateId);
        Task<IList<PMTemplateRevision>> GetRevisionHistoryAsync(int pmTemplateId);
        Task<PMTemplateRevision?> GetRevisionByIdAsync(int revisionId);
        Task UpdateDraftRevisionAsync(PMTemplateRevision revision);
        Task DeleteDraftRevisionAsync(int revisionId);
        string GenerateNextRevisionCode(string? currentCode);
    }

    public class PMTemplateRevisionService : IPMTemplateRevisionService
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;

        public PMTemplateRevisionService(AppDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

        public async Task<PMTemplateRevision> CreateDraftFromTemplateAsync(int pmTemplateId, string? changeReason, string? userId)
        {
            var companyId = GetCompanyId();
            var template = await _context.PMTemplates
                .Include(t => t.CurrentReleasedRevision)
                .Where(t => t.Id == pmTemplateId && _tenantContext.VisibleCompanyIds.Contains(t.CompanyId ?? 0))
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException($"PM Template {pmTemplateId} not found");

            var latestRevision = await _context.Set<PMTemplateRevision>()
                .Where(r => r.PMTemplateId == pmTemplateId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .FirstOrDefaultAsync();

            var nextCode = GenerateNextRevisionCode(latestRevision?.RevisionCode);

            var revision = new PMTemplateRevision
            {
                PMTemplateId = pmTemplateId,
                RevisionCode = nextCode,
                Status = RevisionStatus.Draft,
                ChangeReason = changeReason,
                CreatedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                SupersedesRevisionId = template.CurrentReleasedRevisionId,
                Name = template.Name,
                Description = template.Description,
                Type = template.Type,
                Priority = template.Priority,
                TriggerType = template.TriggerType,
                CalendarInterval = template.CalendarInterval,
                CalendarIntervalValue = template.CalendarIntervalValue,
                MeterType = template.MeterType,
                MeterInterval = template.MeterInterval,
                EstimatedHours = template.EstimatedHours,
                EstimatedLaborCost = template.EstimatedLaborCost,
                EstimatedPartsCost = template.EstimatedPartsCost,
                EstimatedTotalCost = template.EstimatedTotalCost,
                RequiresShutdown = template.RequiresShutdown,
                RequiresLOTO = template.RequiresLOTO,
                SkillLevel = template.SkillLevel,
                Craft = template.Craft,
                Procedure = template.Procedure,
                SafetyInstructions = template.SafetyInstructions,
                ToolsRequired = template.ToolsRequired,
                ReferenceDocuments = template.ReferenceDocuments,
                AssetCategoryId = template.AssetCategoryId,
                ManufacturerId = template.ManufacturerId,
                ModelPattern = template.ModelPattern,
                IsOEMRecommended = template.IsOEMRecommended,
                OEMReference = template.OEMReference,
                IsRegulatoryRequired = template.IsRegulatoryRequired,
                RegulatoryReference = template.RegulatoryReference
            };

            _context.Set<PMTemplateRevision>().Add(revision);
            await _context.SaveChangesAsync();

            return revision;
        }

        public async Task<PMTemplateRevision> CreateDraftFromRevisionAsync(int revisionId, string? changeReason, string? userId)
        {
            var companyId = GetCompanyId();
            var sourceRevision = await _context.Set<PMTemplateRevision>()
                .Include(r => r.Operations)
                .Include(r => r.PMTemplate)
                .Where(r => r.Id == revisionId && r.PMTemplate != null && _tenantContext.VisibleCompanyIds.Contains(r.PMTemplate.CompanyId ?? 0))
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException($"Revision {revisionId} not found");

            var latestRevision = await _context.Set<PMTemplateRevision>()
                .Where(r => r.PMTemplateId == sourceRevision.PMTemplateId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .FirstOrDefaultAsync();

            var nextCode = GenerateNextRevisionCode(latestRevision?.RevisionCode);

            var revision = new PMTemplateRevision
            {
                PMTemplateId = sourceRevision.PMTemplateId,
                RevisionCode = nextCode,
                Status = RevisionStatus.Draft,
                ChangeReason = changeReason,
                CreatedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                SupersedesRevisionId = revisionId,
                Name = sourceRevision.Name,
                Description = sourceRevision.Description,
                Type = sourceRevision.Type,
                Priority = sourceRevision.Priority,
                TriggerType = sourceRevision.TriggerType,
                CalendarInterval = sourceRevision.CalendarInterval,
                CalendarIntervalValue = sourceRevision.CalendarIntervalValue,
                MeterType = sourceRevision.MeterType,
                MeterInterval = sourceRevision.MeterInterval,
                EstimatedHours = sourceRevision.EstimatedHours,
                EstimatedLaborCost = sourceRevision.EstimatedLaborCost,
                EstimatedPartsCost = sourceRevision.EstimatedPartsCost,
                EstimatedTotalCost = sourceRevision.EstimatedTotalCost,
                RequiresShutdown = sourceRevision.RequiresShutdown,
                RequiresLOTO = sourceRevision.RequiresLOTO,
                SkillLevel = sourceRevision.SkillLevel,
                Craft = sourceRevision.Craft,
                Procedure = sourceRevision.Procedure,
                SafetyInstructions = sourceRevision.SafetyInstructions,
                ToolsRequired = sourceRevision.ToolsRequired,
                ReferenceDocuments = sourceRevision.ReferenceDocuments,
                AssetCategoryId = sourceRevision.AssetCategoryId,
                ManufacturerId = sourceRevision.ManufacturerId,
                ModelPattern = sourceRevision.ModelPattern,
                IsOEMRecommended = sourceRevision.IsOEMRecommended,
                OEMReference = sourceRevision.OEMReference,
                IsRegulatoryRequired = sourceRevision.IsRegulatoryRequired,
                RegulatoryReference = sourceRevision.RegulatoryReference,
                Operations = sourceRevision.Operations?.Select(op => new PMTemplateRevisionOperation
                {
                    Sequence = op.Sequence,
                    Description = op.Description,
                    EstimatedHours = op.EstimatedHours,
                    Craft = op.Craft,
                    Notes = op.Notes,
                    IsRequired = op.IsRequired,
                    CreatedAtUtc = DateTime.UtcNow
                }).ToList()
            };

            _context.Set<PMTemplateRevision>().Add(revision);
            await _context.SaveChangesAsync();

            return revision;
        }

        public async Task<PMTemplateRevision> ReleaseRevisionAsync(int revisionId, string? approvedByUserId)
        {
            var companyId = GetCompanyId();
            var revision = await _context.Set<PMTemplateRevision>()
                .Include(r => r.PMTemplate)
                .Where(r => r.Id == revisionId && r.PMTemplate != null && _tenantContext.VisibleCompanyIds.Contains(r.PMTemplate.CompanyId ?? 0))
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException($"Revision {revisionId} not found");

            if (revision.Status != RevisionStatus.Draft)
            {
                throw new InvalidOperationException($"Only Draft revisions can be released. Current status: {revision.Status}");
            }

            if (revision.SupersedesRevisionId.HasValue)
            {
                var supersededRevision = await _context.Set<PMTemplateRevision>()
                    .FirstOrDefaultAsync(r => r.Id == revision.SupersedesRevisionId.Value);

                if (supersededRevision != null && supersededRevision.Status == RevisionStatus.Released)
                {
                    supersededRevision.Status = RevisionStatus.Obsolete;
                    supersededRevision.EffectiveToUtc = DateTime.UtcNow;
                    supersededRevision.ObsoletedAtUtc = DateTime.UtcNow;
                }
            }

            revision.Status = RevisionStatus.Released;
            revision.ApprovedByUserId = approvedByUserId;
            revision.ApprovedAtUtc = DateTime.UtcNow;
            revision.ReleasedAtUtc = DateTime.UtcNow;
            revision.EffectiveFromUtc = DateTime.UtcNow;

            var template = revision.PMTemplate!;
            template.CurrentReleasedRevisionId = revision.Id;

            await _context.SaveChangesAsync();

            return revision;
        }

        public async Task ObsoleteRevisionAsync(int revisionId)
        {
            var companyId = GetCompanyId();
            var revision = await _context.Set<PMTemplateRevision>()
                .Include(r => r.PMTemplate)
                .Where(r => r.Id == revisionId && r.PMTemplate != null && _tenantContext.VisibleCompanyIds.Contains(r.PMTemplate.CompanyId ?? 0))
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException($"Revision {revisionId} not found");

            if (revision.Status != RevisionStatus.Released)
            {
                throw new InvalidOperationException($"Only Released revisions can be obsoleted. Current status: {revision.Status}");
            }

            revision.Status = RevisionStatus.Obsolete;
            revision.EffectiveToUtc = DateTime.UtcNow;
            revision.ObsoletedAtUtc = DateTime.UtcNow;

            if (revision.PMTemplate?.CurrentReleasedRevisionId == revision.Id)
            {
                revision.PMTemplate.CurrentReleasedRevisionId = null;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<PMTemplateRevision?> GetCurrentReleasedRevisionAsync(int pmTemplateId)
        {
            var companyId = GetCompanyId();
            var templateBelongsToTenant = await _context.PMTemplates.AnyAsync(t => t.Id == pmTemplateId && _tenantContext.VisibleCompanyIds.Contains(t.CompanyId ?? 0));
            if (!templateBelongsToTenant)
                return null;

            return await _context.Set<PMTemplateRevision>()
                .Include(r => r.Operations)
                .Where(r => r.PMTemplateId == pmTemplateId && r.Status == RevisionStatus.Released)
                .OrderByDescending(r => r.ReleasedAtUtc)
                .FirstOrDefaultAsync();
        }

        public async Task<IList<PMTemplateRevision>> GetRevisionHistoryAsync(int pmTemplateId)
        {
            var companyId = GetCompanyId();
            var templateBelongsToTenant = await _context.PMTemplates.AnyAsync(t => t.Id == pmTemplateId && _tenantContext.VisibleCompanyIds.Contains(t.CompanyId ?? 0));
            if (!templateBelongsToTenant)
                return new List<PMTemplateRevision>();

            return await _context.Set<PMTemplateRevision>()
                .Where(r => r.PMTemplateId == pmTemplateId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToListAsync();
        }

        public async Task<PMTemplateRevision?> GetRevisionByIdAsync(int revisionId)
        {
            var companyId = GetCompanyId();
            return await _context.Set<PMTemplateRevision>()
                .Include(r => r.Operations)
                .Include(r => r.PMTemplate)
                .Where(r => r.Id == revisionId && r.PMTemplate != null && _tenantContext.VisibleCompanyIds.Contains(r.PMTemplate.CompanyId ?? 0))
                .FirstOrDefaultAsync();
        }

        public async Task UpdateDraftRevisionAsync(PMTemplateRevision revision)
        {
            if (revision.Status != RevisionStatus.Draft)
            {
                throw new InvalidOperationException("Only Draft revisions can be updated");
            }

            var companyId = GetCompanyId();
            var existing = await _context.Set<PMTemplateRevision>()
                .Include(r => r.PMTemplate)
                .Where(r => r.Id == revision.Id && r.PMTemplate != null && _tenantContext.VisibleCompanyIds.Contains(r.PMTemplate.CompanyId ?? 0))
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException($"Revision {revision.Id} not found");

            _context.Set<PMTemplateRevision>().Update(revision);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteDraftRevisionAsync(int revisionId)
        {
            var companyId = GetCompanyId();
            var revision = await _context.Set<PMTemplateRevision>()
                .Include(r => r.PMTemplate)
                .Where(r => r.Id == revisionId && r.PMTemplate != null && _tenantContext.VisibleCompanyIds.Contains(r.PMTemplate.CompanyId ?? 0))
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException($"Revision {revisionId} not found");

            if (revision.Status != RevisionStatus.Draft)
            {
                throw new InvalidOperationException("Only Draft revisions can be deleted");
            }

            _context.Set<PMTemplateRevision>().Remove(revision);
            await _context.SaveChangesAsync();
        }

        public string GenerateNextRevisionCode(string? currentCode)
        {
            if (string.IsNullOrEmpty(currentCode))
            {
                return "A";
            }

            if (currentCode.Length == 1 && char.IsLetter(currentCode[0]))
            {
                var c = currentCode[0];
                if (c == 'Z' || c == 'z')
                {
                    return "AA";
                }
                return ((char)(c + 1)).ToString();
            }

            var numericPart = 0;
            var letterPart = "";
            foreach (var c in currentCode)
            {
                if (char.IsLetter(c))
                {
                    letterPart += c;
                }
                else if (char.IsDigit(c))
                {
                    numericPart = numericPart * 10 + (c - '0');
                }
            }

            if (!string.IsNullOrEmpty(letterPart) && numericPart == 0)
            {
                var lastChar = letterPart[^1];
                if (lastChar == 'Z' || lastChar == 'z')
                {
                    return letterPart + "A";
                }
                return letterPart[..^1] + ((char)(lastChar + 1)).ToString();
            }

            return letterPart + (numericPart + 1).ToString();
        }
    }
}
