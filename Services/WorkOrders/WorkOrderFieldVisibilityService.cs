using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.WorkOrders
{
    // ADR-012 v0.2 / PR #119.2 — Field visibility resolver implementation.
    //
    // Reads from WorkOrderFieldVisibility table; caches resolved layouts
    // per (Classification, TenantId) tuple in a ConcurrentDictionary.
    // Cache is process-local (not distributed) — fine for the current
    // single-instance Replit deploy; revisit if we ever fan out.
    //
    // Merge precedence:
    //   Tenant-scoped row (TenantId == tenantId) → overrides
    //   Global row (TenantId IS NULL)            → fallback
    //
    // When a tenant has no override for a field, the global row wins.
    // When a tenant explicitly hides a field that global has Optional,
    // the tenant Hidden wins.
    public class WorkOrderFieldVisibilityService : IWorkOrderFieldVisibilityService
    {
        private readonly AppDbContext _db;
        private readonly WorkOrderFieldVisibilityCache _cacheHost;
        private readonly ILogger<WorkOrderFieldVisibilityService> _logger;

        // Cache key is (Classification, TenantId-or-(-1)). The cache itself
        // is held in a Singleton (WorkOrderFieldVisibilityCache) so layouts
        // resolved by one request survive into the next. The service is
        // Scoped to satisfy the DI lifetime check on the injected DbContext.
        private ConcurrentDictionary<(WorkOrderClassification, int), IReadOnlyList<WorkOrderFieldVisibility>> _cache
            => _cacheHost.Layouts;

        public WorkOrderFieldVisibilityService(
            AppDbContext db,
            WorkOrderFieldVisibilityCache cacheHost,
            ILogger<WorkOrderFieldVisibilityService> logger)
        {
            _db = db;
            _cacheHost = cacheHost;
            _logger = logger;
        }

        public async Task<IReadOnlyList<WorkOrderFieldVisibility>> GetLayoutAsync(
            WorkOrderClassification classification,
            int? tenantId,
            CancellationToken ct = default)
        {
            var cacheKey = (classification, tenantId ?? -1);
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

            // Pull global + tenant rows in one query.
            var rows = await _db.WorkOrderFieldVisibility
                .AsNoTracking()
                .Where(v => v.Classification == classification
                         && (v.TenantId == null || v.TenantId == tenantId))
                .ToListAsync(ct);

            // Merge: tenant wins on conflict.
            var byField = new Dictionary<string, WorkOrderFieldVisibility>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows.Where(r => r.TenantId == null))
                byField[r.FieldName] = r;
            foreach (var r in rows.Where(r => r.TenantId == tenantId))
                byField[r.FieldName] = r;

            // Drop Hidden, sort by section + display order.
            var resolved = byField.Values
                .Where(v => v.Visibility != FieldVisibility.Hidden)
                .OrderBy(v => v.SectionName)
                .ThenBy(v => v.DisplayOrder)
                .ThenBy(v => v.FieldName)
                .ToList()
                .AsReadOnly();

            _cache[cacheKey] = resolved;
            return resolved;
        }

        public async Task<IReadOnlyList<SectionLayout>> GetSectionedLayoutAsync(
            WorkOrderClassification classification,
            int? tenantId,
            CancellationToken ct = default)
        {
            var flat = await GetLayoutAsync(classification, tenantId, ct);
            return flat
                .GroupBy(v => v.SectionName)
                .Select(g => new SectionLayout(
                    SectionName: g.Key,
                    Fields: g.OrderBy(v => v.DisplayOrder).ThenBy(v => v.FieldName).ToList()))
                .OrderBy(s => MinDisplayOrder(s.Fields))
                .ToList()
                .AsReadOnly();
        }

        public async Task<FieldVisibility> GetFieldVisibilityAsync(
            WorkOrderClassification classification,
            string fieldName,
            int? tenantId,
            CancellationToken ct = default)
        {
            var layout = await GetLayoutAsync(classification, tenantId, ct);
            var match = layout.FirstOrDefault(v =>
                string.Equals(v.FieldName, fieldName, StringComparison.OrdinalIgnoreCase));
            // If not in the table (or filtered out as Hidden) → treat as Optional.
            // The renderer is conservative; it shows the field when in doubt.
            return match?.Visibility ?? FieldVisibility.Optional;
        }

        public void Invalidate(int? tenantId = null)
        {
            if (tenantId == null)
            {
                _cache.Clear();
                _logger.LogInformation("WorkOrderFieldVisibility cache cleared (all tenants).");
                return;
            }
            var encoded = tenantId.Value;
            var keysToDrop = _cache.Keys.Where(k => k.Item2 == encoded).ToList();
            foreach (var key in keysToDrop) _cache.TryRemove(key, out _);
            _logger.LogInformation("WorkOrderFieldVisibility cache cleared for tenant {TenantId}.", encoded);
        }

        private static int MinDisplayOrder(IReadOnlyList<WorkOrderFieldVisibility> fields)
            => fields.Count == 0 ? int.MaxValue : fields.Min(f => f.DisplayOrder);
    }
}
