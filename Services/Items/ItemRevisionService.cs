using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Items;

public interface IItemRevisionService
{
    Task<ItemRevision?> GetCurrentReleasedAsync(int itemId);
    Task<ItemRevision?> GetByIdAsync(int revisionId);
    Task<List<ItemRevision>> GetRevisionsForItemAsync(int itemId);
    Task<ItemRevision> CreateDraftFromItemAsync(int itemId, string? changeReason, string? userId = null);
    Task<ItemRevision> CreateDraftFromRevisionAsync(int revisionId, string? changeReason, string? userId = null);
    Task<ItemRevision> UpdateDraftAsync(int revisionId, string? name, string? description, string? changeReason);
    Task<ItemRevision> ReleaseRevisionAsync(int revisionId, string? approvedBy, string? changeReason);
    Task<ItemRevision> ObsoleteRevisionAsync(int revisionId);
    string GenerateNextRevisionCode(IEnumerable<string> existingCodes);
}

public class ItemRevisionService : IItemRevisionService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItemRevisionService> _logger;
    private readonly ITenantContext _tenantContext;

    public ItemRevisionService(AppDbContext db, ILogger<ItemRevisionService> logger, ITenantContext tenantContext)
    {
        _db = db;
        _logger = logger;
        _tenantContext = tenantContext;
    }

    private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

    public async Task<ItemRevision?> GetCurrentReleasedAsync(int itemId)
    {
        var companyId = GetCompanyId();
        var item = await _db.Items
            .Include(i => i.CurrentReleasedRevision)
            .Where(i => i.Id == itemId && _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0))
            .FirstOrDefaultAsync();
        return item?.CurrentReleasedRevision;
    }

    public async Task<ItemRevision?> GetByIdAsync(int revisionId)
    {
        var companyId = GetCompanyId();
        return await _db.ItemRevisions
            .Include(r => r.Item)
            .Include(r => r.SupersedesRevision)
            .Where(r => r.Id == revisionId && r.Item != null && _tenantContext.VisibleCompanyIds.Contains(r.Item.CompanyId ?? 0))
            .FirstOrDefaultAsync();
    }

    public async Task<List<ItemRevision>> GetRevisionsForItemAsync(int itemId)
    {
        var companyId = GetCompanyId();
        var itemBelongsToTenant = await _db.Items.AnyAsync(i => i.Id == itemId && _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0));
        if (!itemBelongsToTenant)
            return new List<ItemRevision>();

        return await _db.ItemRevisions
            .Where(r => r.ItemId == itemId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<ItemRevision> CreateDraftFromItemAsync(int itemId, string? changeReason, string? userId = null)
    {
        var companyId = GetCompanyId();
        var item = await _db.Items
            .Include(i => i.Revisions)
            .Include(i => i.CurrentReleasedRevision)
            .Where(i => i.Id == itemId && _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0))
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Item {itemId} not found");

        var existingDraft = item.Revisions?.FirstOrDefault(r => r.Status == RevisionStatus.Draft);
        if (existingDraft != null)
            throw new InvalidOperationException($"Item already has an active draft revision ({existingDraft.RevisionCode}). Complete or delete it first.");

        if (item.CurrentReleasedRevision != null)
        {
            return await CreateDraftFromRevisionAsync(item.CurrentReleasedRevision.Id, changeReason, userId);
        }

        var existingCodes = item.Revisions?.Select(r => r.RevisionCode).ToList() ?? new List<string>();
        var nextCode = GenerateNextRevisionCode(existingCodes);

        var draft = new ItemRevision
        {
            ItemId = itemId,
            RevisionCode = nextCode,
            Status = RevisionStatus.Draft,
            Name = item.Description,
            Description = item.ExtendedDescription,
            ChangeReason = changeReason,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.ItemRevisions.Add(draft);
        await _db.SaveChangesAsync();

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            Action = "ITEMREV.DRAFT.CREATED",
            EntityType = "ItemRevision",
            EntityId = draft.Id,
            Username = userId,
            AfterJson = System.Text.Json.JsonSerializer.Serialize(new { draft.Id, draft.ItemId, draft.RevisionCode }),
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created draft revision {RevisionCode} for Item {ItemId}", nextCode, itemId);
        return draft;
    }

    public async Task<ItemRevision> CreateDraftFromRevisionAsync(int revisionId, string? changeReason, string? userId = null)
    {
        var companyId = GetCompanyId();
        var sourceRevision = await _db.ItemRevisions
            .Include(r => r.Item)
            .ThenInclude(i => i!.Revisions)
            .Where(r => r.Id == revisionId && r.Item != null && _tenantContext.VisibleCompanyIds.Contains(r.Item.CompanyId ?? 0))
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Revision {revisionId} not found");

        var existingDraft = sourceRevision.Item?.Revisions?.FirstOrDefault(r => r.Status == RevisionStatus.Draft);
        if (existingDraft != null)
            throw new InvalidOperationException($"Item already has an active draft revision ({existingDraft.RevisionCode}). Complete or delete it first.");

        var existingCodes = sourceRevision.Item?.Revisions?.Select(r => r.RevisionCode).ToList() ?? new List<string>();
        var nextCode = GenerateNextRevisionCode(existingCodes);

        var draft = new ItemRevision
        {
            ItemId = sourceRevision.ItemId,
            RevisionCode = nextCode,
            Status = RevisionStatus.Draft,
            Name = sourceRevision.Name,
            Description = sourceRevision.Description,
            ChangeReason = changeReason,
            SupersedesItemRevisionId = sourceRevision.Id,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.ItemRevisions.Add(draft);
        await _db.SaveChangesAsync();

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            Action = "ITEMREV.DRAFT.CREATED",
            EntityType = "ItemRevision",
            EntityId = draft.Id,
            Username = userId,
            AfterJson = System.Text.Json.JsonSerializer.Serialize(new { draft.Id, draft.ItemId, draft.RevisionCode, SourceRevisionId = revisionId }),
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created draft revision {RevisionCode} from revision {SourceId} for Item {ItemId}", 
            nextCode, revisionId, sourceRevision.ItemId);
        return draft;
    }

    public async Task<ItemRevision> UpdateDraftAsync(int revisionId, string? name, string? description, string? changeReason)
    {
        var companyId = GetCompanyId();
        var revision = await _db.ItemRevisions
            .Include(r => r.Item)
            .Where(r => r.Id == revisionId && r.Item != null && _tenantContext.VisibleCompanyIds.Contains(r.Item.CompanyId ?? 0))
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Revision {revisionId} not found");

        if (revision.Status != RevisionStatus.Draft)
            throw new InvalidOperationException($"Cannot update revision {revisionId} - only Draft revisions can be edited");

        if (name != null) revision.Name = name;
        if (description != null) revision.Description = description;
        if (changeReason != null) revision.ChangeReason = changeReason;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated draft revision {RevisionId}", revisionId);
        return revision;
    }

    public async Task<ItemRevision> ReleaseRevisionAsync(int revisionId, string? approvedBy, string? changeReason)
    {
        if (string.IsNullOrWhiteSpace(changeReason))
            throw new InvalidOperationException("Change reason is required to release a revision.");

        var companyId = GetCompanyId();
        var revision = await _db.ItemRevisions
            .Include(r => r.Item)
            .Where(r => r.Id == revisionId && r.Item != null && _tenantContext.VisibleCompanyIds.Contains(r.Item.CompanyId ?? 0))
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Revision {revisionId} not found");

        if (revision.Status != RevisionStatus.Draft)
            throw new InvalidOperationException($"Cannot release revision {revisionId} - only Draft revisions can be released");

        var item = revision.Item ?? throw new InvalidOperationException("Item not found");

        var previousReleased = item.CurrentReleasedRevisionId.HasValue
            ? await _db.ItemRevisions.Where(r => r.Id == item.CurrentReleasedRevisionId.Value).FirstOrDefaultAsync()
            : null;

        if (previousReleased != null && previousReleased.Status == RevisionStatus.Released)
        {
            previousReleased.Status = RevisionStatus.Obsolete;
            previousReleased.ObsoletedAtUtc = DateTime.UtcNow;
            previousReleased.EffectiveToUtc = DateTime.UtcNow;
        }

        revision.Status = RevisionStatus.Released;
        revision.ApprovedByUserId = approvedBy;
        revision.ApprovedAtUtc = DateTime.UtcNow;
        revision.ReleasedAtUtc = DateTime.UtcNow;
        revision.EffectiveFromUtc = DateTime.UtcNow;
        if (changeReason != null) revision.ChangeReason = changeReason;

        item.CurrentReleasedRevisionId = revision.Id;

        await _db.SaveChangesAsync();

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            Action = "ITEMREV.RELEASED",
            EntityType = "ItemRevision",
            EntityId = revision.Id,
            Username = approvedBy,
            AfterJson = System.Text.Json.JsonSerializer.Serialize(new { revision.Id, revision.ItemId, revision.RevisionCode }),
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("Released revision {RevisionCode} for Item {ItemId}", revision.RevisionCode, revision.ItemId);
        return revision;
    }

    public async Task<ItemRevision> ObsoleteRevisionAsync(int revisionId)
    {
        var companyId = GetCompanyId();
        var revision = await _db.ItemRevisions
            .Include(r => r.Item)
            .Where(r => r.Id == revisionId && r.Item != null && _tenantContext.VisibleCompanyIds.Contains(r.Item.CompanyId ?? 0))
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Revision {revisionId} not found");

        if (revision.Status == RevisionStatus.Obsolete)
            return revision;

        var item = revision.Item;
        if (item != null && item.CurrentReleasedRevisionId == revision.Id)
        {
            item.CurrentReleasedRevisionId = null;
        }

        revision.Status = RevisionStatus.Obsolete;
        revision.ObsoletedAtUtc = DateTime.UtcNow;
        revision.EffectiveToUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            Action = "ITEMREV.OBSOLETED",
            EntityType = "ItemRevision",
            EntityId = revision.Id,
            AfterJson = System.Text.Json.JsonSerializer.Serialize(new { revision.Id, revision.ItemId, revision.RevisionCode }),
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("Obsoleted revision {RevisionCode} for Item {ItemId}", revision.RevisionCode, revision.ItemId);
        return revision;
    }

    public string GenerateNextRevisionCode(IEnumerable<string> existingCodes)
    {
        var codes = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        for (char c = 'A'; c <= 'Z'; c++)
        {
            var code = c.ToString();
            if (!codes.Contains(code))
                return code;
        }

        for (char c1 = 'A'; c1 <= 'Z'; c1++)
        {
            for (char c2 = 'A'; c2 <= 'Z'; c2++)
            {
                var code = $"{c1}{c2}";
                if (!codes.Contains(code))
                    return code;
            }
        }

        return $"R{DateTime.UtcNow:yyyyMMddHHmmss}";
    }
}
