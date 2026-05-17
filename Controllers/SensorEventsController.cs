using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Telemetry;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Telemetry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Controllers;

// Sprint 2 PR #118.2 — Sensor ingest API.
//
// The single REST entry point for sensor data on Cherry. Gateways (REST,
// MQTT-bridges, OPC-UA-bridges, Sparkplug B clients, manual operator
// entry, demo seeder) all POST here. Inside the handler we delegate to
// ISensorIngestService for the single-transaction write path
// (SensorEvent + AssetSensorLatest upsert + IsOutOfSpec compute).
//
// Auth model in #118.2: relies on the app's existing cookie auth pipeline
// — any authenticated user can post. PR #128 (OPC-UA / Sparkplug B
// gateway service) will add API-key / service-account auth on this
// endpoint with IEC 62443 zone-claim enforcement.
[ApiController]
[Route("api/v1/sensors")]
public class SensorEventsController : ControllerBase
{
    private readonly ISensorIngestService _ingest;
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<SensorEventsController> _logger;

    public SensorEventsController(
        ISensorIngestService ingest,
        AppDbContext db,
        ITenantContext tenant,
        ILogger<SensorEventsController> logger)
    {
        _ingest = ingest;
        _db = db;
        _tenant = tenant;
        _logger = logger;
    }

    // POST /api/v1/sensors/events
    //
    // Body: { "events": [ <SensorEvent>, ... ] }
    //   - 1 to 1000 events per request
    //   - server overwrites IngestedAt regardless of what the client sent
    //   - IsOutOfSpec computed server-side from SensorProfile thresholds
    //
    // Returns 200 { "ingested": <count> } on success.
    [HttpPost("events")]
    public async Task<IActionResult> PostEvents(
        [FromBody] SensorEventBatchRequest req,
        CancellationToken ct)
    {
        if (req == null || req.Events == null || req.Events.Count == 0)
        {
            return BadRequest(new { error = "events array is required and must be non-empty" });
        }

        if (req.Events.Count > 1000)
        {
            return BadRequest(new { error = "max 1000 events per request" });
        }

        try
        {
            var n = await _ingest.IngestBatchAsync(req.Events, ct);
            return Ok(new { ingested = n });
        }
        catch (SensorIngestService.TenantScopeViolationException tex)
        {
            _logger.LogWarning(tex, "SensorEventsController: tenant scope violation");
            return StatusCode(403, new { error = "forbidden", detail = tex.Message });
        }
        catch (System.ArgumentException ax)
        {
            _logger.LogWarning(ax, "SensorEventsController: bad input");
            return BadRequest(new { error = ax.Message });
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "SensorEventsController: ingest failed after partial work");
            return StatusCode(500, new { error = "ingest failed", detail = ex.Message });
        }
    }

    // GET /api/v1/sensors/latest?assetIds=1,2,3&readingTypes=0,1
    //
    // Bulk read of AssetSensorLatest, the denormalized 1-row-per-
    // (asset, type) cache that powers the Plant Floor tile. Single
    // point-lookup-join query; no joins to the hypertable.
    //
    // Params:
    //   assetIds      — required, comma-separated, max 500 IDs
    //   readingTypes  — optional, comma-separated SensorReadingType enum values
    //                   (if omitted, all types for the requested assets)
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(
        [FromQuery] string? assetIds,
        [FromQuery] string? readingTypes,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(assetIds))
            return BadRequest(new { error = "assetIds is required" });

        var ids = ParseIntList(assetIds);
        if (ids.Count == 0)
            return BadRequest(new { error = "assetIds must contain at least one integer" });
        if (ids.Count > 500)
            return BadRequest(new { error = "max 500 assetIds per request" });

        var typeFilter = ParseEnumList<SensorReadingType>(readingTypes);

        // PR #118.4 — Tenant filter the requested assetIds to those
        // whose owning Company is in the caller's tenant scope. Silent
        // filter (drop ineligible IDs) rather than throw — read-side
        // bulk requests are common and partial visibility is the norm.
        var visibleAssetIds = await _db.Assets
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id)
                     && _tenant.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
            .Select(a => a.Id)
            .ToListAsync(ct);

        var q = _db.AssetSensorLatest.AsNoTracking().Where(x => visibleAssetIds.Contains(x.AssetId));
        if (typeFilter.Count > 0)
            q = q.Where(x => typeFilter.Contains(x.ReadingType));

        var rows = await q
            .OrderBy(x => x.AssetId).ThenBy(x => x.ReadingType)
            .Select(x => new
            {
                x.AssetId,
                x.ReadingType,
                x.Value,
                x.Unit,
                x.ReadingAt,
                x.QualityCode,
                x.IsOutOfSpec,
                x.Tone,
                x.UpdatedAt,
            })
            .ToListAsync(ct);

        return Ok(new { count = rows.Count, latest = rows });
    }

    // GET /api/v1/sensors/events?assetId=1&readingType=0&sinceUtc=...&limit=100
    //
    // History-window read from the SensorEvents hypertable. Returns up
    // to `limit` events (default 100, max 1000) for one (asset, type),
    // ordered by ReadingAt DESC. For multi-asset / aggregated reads,
    // use the rollup endpoints (PR #118.4).
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int assetId,
        [FromQuery] SensorReadingType readingType,
        [FromQuery] DateTime? sinceUtc,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        if (assetId <= 0)
            return BadRequest(new { error = "assetId is required" });
        if (limit < 1) limit = 1;
        if (limit > 1000) limit = 1000;

        // PR #118.4 — Tenant scope check. The asset must belong to a
        // Company in the caller's visible scope. Return 404 (not 403)
        // on miss to avoid disclosing existence of out-of-scope assets.
        var assetVisible = await _db.Assets
            .AsNoTracking()
            .AnyAsync(a => a.Id == assetId
                        && _tenant.VisibleCompanyIds.Contains(a.CompanyId ?? 0), ct);
        if (!assetVisible)
            return NotFound(new { error = $"asset {assetId} not found in your tenant scope" });

        var q = _db.SensorEvents.AsNoTracking()
            .Where(e => e.AssetId == assetId && e.ReadingType == readingType);
        if (sinceUtc.HasValue)
            q = q.Where(e => e.ReadingAt >= sinceUtc.Value);

        var rows = await q
            .OrderByDescending(e => e.ReadingAt)
            .Take(limit)
            .Select(e => new
            {
                e.Id,
                e.AssetId,
                e.ReadingType,
                e.Value,
                e.Unit,
                e.ReadingAt,
                e.IngestedAt,
                e.Source,
                e.SourceZone,
                e.QualityCode,
                e.IsOutOfSpec,
            })
            .ToListAsync(ct);

        return Ok(new { count = rows.Count, events = rows });
    }

    // --- helpers ---

    private static List<int> ParseIntList(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var i) ? i : -1)
            .Where(i => i > 0)
            .Distinct()
            .ToList();

    private static List<T> ParseEnumList<T>(string? csv) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(csv)) return new List<T>();
        var result = new List<T>();
        foreach (var s in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<T>(s, ignoreCase: true, out var val))
                result.Add(val);
            else if (int.TryParse(s, out var n) && Enum.IsDefined(typeof(T), n))
                result.Add((T)Enum.ToObject(typeof(T), n));
        }
        return result.Distinct().ToList();
    }
}

public class SensorEventBatchRequest
{
    public List<SensorEvent> Events { get; set; } = new();
}
