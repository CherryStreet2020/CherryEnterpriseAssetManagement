using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Voice;

// Sprint 12C / ADR-021 §D3 — embedding worker as a hosted BackgroundService.
//
// Poll loop:
//   - Every PollIntervalSeconds, call IEmbeddingBackfillService.ProcessPendingAsync
//   - On no-rows, sleep the full interval
//   - On rows-processed, immediately poll again (drain bursts faster)
//
// Lifecycle:
//   - Starts with the app (via builder.Services.AddHostedService<EmbeddingWorker>)
//   - Survives Replit hot-reloads (BackgroundService standard contract)
//   - Stops cleanly on app shutdown (cancellation token wired through)
//
// Single-instance assumption is fine for now (Replit runs one process).
// Multi-instance safety lives in the SQL itself (FOR UPDATE SKIP LOCKED
// in EmbeddingBackfillService.ProcessPendingAsync).
public sealed class EmbeddingWorker : BackgroundService
{
    private const int PollIntervalSeconds = 5;
    private const int BatchSize = 32;

    private readonly IServiceProvider _services;
    private readonly ILogger<EmbeddingWorker> _logger;

    public EmbeddingWorker(IServiceProvider services, ILogger<EmbeddingWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmbeddingWorker started — polling every {Seconds}s for pending embeddings", PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processed;
                using (var scope = _services.CreateScope())
                {
                    var svc = scope.ServiceProvider.GetRequiredService<IEmbeddingBackfillService>();
                    processed = await svc.ProcessPendingAsync(BatchSize, stoppingToken);
                }

                if (processed == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), stoppingToken);
                }
                else
                {
                    _logger.LogDebug("EmbeddingWorker processed {N} rows; polling again immediately", processed);
                    // Yield briefly to keep the loop from starving other work.
                    await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                }
            }
            catch (OperationCanceledException) { /* shutdown */ }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmbeddingWorker loop iteration failed; sleeping {Seconds}s before retry", PollIntervalSeconds);
                try { await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), stoppingToken); } catch { /* shutdown */ }
            }
        }

        _logger.LogInformation("EmbeddingWorker stopped");
    }
}
