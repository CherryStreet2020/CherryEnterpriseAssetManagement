using Microsoft.AspNetCore.Mvc;
using Abs.FixedAssets.Services.Integrations;
using System.Text;

namespace Abs.FixedAssets.Controllers;

[ApiController]
[Route("api/integrations")]
public class IntegrationWebhookController : ControllerBase
{
    private readonly IInboundWebhookService _webhookService;
    private readonly ILogger<IntegrationWebhookController> _logger;

    public IntegrationWebhookController(
        IInboundWebhookService webhookService,
        ILogger<IntegrationWebhookController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    [HttpPost("inbound/{integrationKey}")]
    public async Task<IActionResult> ReceiveWebhook(string integrationKey)
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync();
        }

        var timestamp = Request.Headers["X-CherryAI-Timestamp"].FirstOrDefault();
        var signature = Request.Headers["X-CherryAI-Signature"].FirstOrDefault();
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

        var headers = new Dictionary<string, string>();
        foreach (var header in Request.Headers)
        {
            if (header.Key.StartsWith("X-CherryAI", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Idempotency-Key", StringComparison.OrdinalIgnoreCase))
            {
                headers[header.Key] = header.Value.ToString();
            }
        }

        var (success, message, eventId) = await _webhookService.ReceiveWebhookAsync(
            integrationKey, rawBody, timestamp, signature, idempotencyKey, headers);

        if (!success)
        {
            _logger.LogWarning("Inbound webhook rejected for {Key}: {Message}", integrationKey, message);
            return BadRequest(new { success = false, message });
        }

        return Ok(new { success = true, message, eventId });
    }
}
