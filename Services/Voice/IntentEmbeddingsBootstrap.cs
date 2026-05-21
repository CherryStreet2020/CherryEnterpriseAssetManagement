using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Voice;

// Sprint 12C / ADR-021 — Intent prototype seeder (PR #3).
//
// IHostedService that runs once on app startup. Enqueues any missing
// IntentPrototypes.All entries into the PendingEmbeddings queue. The
// EmbeddingWorker (already running every 5s) drains the queue, calls
// Voyage with `input_type=document`, and writes the resulting rows
// into the Embeddings table.
//
// Idempotency:
//   - EmbeddingBackfillService.EnqueueAsync de-dupes by (EntityType,
//     EntityId, ContentHash) so re-enqueues from a re-deploy are no-ops.
//   - If the prototype text changes in code, the hash changes and the
//     row gets re-embedded automatically.
//
// Non-Postgres safety:
//   - The Embeddings DbSet is .Ignore()'d on non-Postgres providers
//     (see AppDbContext.OnModelCreating). The bootstrap detects this
//     and short-circuits — test contexts get nothing seeded, which is
//     what we want.
//
// Voyage outage safety:
//   - EnqueueAsync just writes to PendingEmbeddings. If Voyage is down,
//     rows stack up in the queue with Attempts++ but the bootstrap
//     never blocks app startup.

public sealed class IntentEmbeddingsBootstrap : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<IntentEmbeddingsBootstrap> _logger;

    public IntentEmbeddingsBootstrap(
        IServiceProvider services,
        ILogger<IntentEmbeddingsBootstrap> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var backfill = scope.ServiceProvider.GetRequiredService<IEmbeddingBackfillService>();

            // Test contexts ignore Embeddings entirely. Nothing to seed.
            if (!db.Database.IsNpgsql())
            {
                _logger.LogInformation(
                    "Intent prototype seed skipped — non-Postgres provider (test context).");
                return;
            }

            var enqueuedCount = 0;
            foreach (var proto in IntentPrototypes.All)
            {
                // EnqueueAsync handles all idempotency. We just call it for
                // every prototype every startup; same-hash rows no-op.
                await backfill.EnqueueAsync(
                    entityType: IntentPrototypes.EntityType,
                    entityId: (long)proto.Kind,
                    tenantId: IntentPrototypes.SystemTenantId,
                    sourceText: proto.Utterance,
                    cancellationToken);
                enqueuedCount++;
            }

            _logger.LogInformation(
                "Intent prototype seed pass complete — {Count} prototypes considered. Worker will drain new rows in <5s.",
                enqueuedCount);
        }
        catch (Exception ex)
        {
            // Bootstrap MUST NOT crash app startup. The hybrid router
            // gracefully degrades to keyword-only without seeded
            // prototypes (returns Unknown for the queries vector would
            // have caught, which is the same behavior we had pre-PR #3).
            _logger.LogWarning(ex, "Intent prototype seed failed — hybrid router will operate keyword-only until next restart.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
