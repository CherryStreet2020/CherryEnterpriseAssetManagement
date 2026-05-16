using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Telemetry;
using Abs.FixedAssets.Services.Telemetry;
using Microsoft.AspNetCore.Mvc;

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
    private readonly ILogger<SensorEventsController> _logger;

    public SensorEventsController(
        ISensorIngestService ingest,
        ILogger<SensorEventsController> logger)
    {
        _ingest = ingest;
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
}

public class SensorEventBatchRequest
{
    public List<SensorEvent> Events { get; set; } = new();
}
