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
        // PR #270 — observable bootstrap. The original try/catch in PR #3 (Sprint
        // 12C) swallowed exceptions silently; in production both dev and prod
        // landed with Embeddings.Intent = 0 and PendingEmbeddings.Intent = 0,
        // meaning the bootstrap never successfully enqueued. The logging here
        // makes every step visible so the next deploy reveals the real failure
        // mode in Replit's runtime logs.
        _logger.LogInformation(
            "[intent-seed] IntentEmbeddingsBootstrap.StartAsync entered — {ProtoCount} prototypes to consider.",
            IntentPrototypes.All.Count);

        try
        {
            using var scope = _services.CreateScope();
            _logger.LogInformation("[intent-seed] step 1: DI scope created.");

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            _logger.LogInformation("[intent-seed] step 2: AppDbContext resolved.");

            var backfill = scope.ServiceProvider.GetRequiredService<IEmbeddingBackfillService>();
            _logger.LogInformation("[intent-seed] step 3: IEmbeddingBackfillService resolved.");

            // Test contexts ignore Embeddings entirely. Nothing to seed.
            if (!db.Database.IsNpgsql())
            {
                _logger.LogInformation(
                    "[intent-seed] non-Postgres provider — bootstrap skipped (test context).");
                return;
            }
            _logger.LogInformation("[intent-seed] step 4: Postgres provider confirmed.");

            // Establish the tenant session variable BEFORE any read or write
            // touches the Embeddings / PendingEmbeddings tables. The RLS policy
            // is `USING ("TenantId" = 0 OR "TenantId" = NULLIF(current_setting(
            // 'app.tenant_id', true), '')::int)` — INSERT of TenantId=0 rows
            // should pass with or without the session var, but setting it
            // defensively rules out one whole class of silent-failure mode
            // (and matches the pattern any per-request middleware would use).
            //
            // SET (no LOCAL) makes it session-scoped, so it persists for the
            // lifetime of this scope's connection. When the connection returns
            // to the pool, any subsequent request grabbing it would have its
            // own middleware overwrite the value — no cross-context leak.
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.tenant_id', '0', false)",
                cancellationToken);
            _logger.LogInformation("[intent-seed] step 5: app.tenant_id session var set to '0'.");

            var enqueuedCount = 0;
            var protoIndex = 0;
            foreach (var proto in IntentPrototypes.All)
            {
                protoIndex++;
                try
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
                catch (Exception enqueueEx)
                {
                    // Per-prototype error log so we know WHICH proto failed
                    // without one bad row breaking the whole loop.
                    _logger.LogError(enqueueEx,
                        "[intent-seed] EnqueueAsync threw on prototype {Index}/{Total} (Kind={Kind}, Utterance={Utterance})",
                        protoIndex, IntentPrototypes.All.Count, proto.Kind, proto.Utterance);
                }
            }

            _logger.LogInformation(
                "[intent-seed] step 6: bootstrap complete — {Enqueued} of {Total} prototypes enqueued (or already present). Worker will drain new rows in <5s.",
                enqueuedCount, IntentPrototypes.All.Count);
        }
        catch (Exception ex)
        {
            // Bootstrap MUST NOT crash app startup. The hybrid router
            // gracefully degrades to keyword-only without seeded prototypes
            // (Layer 1 still covers ~99% of utterances). LogError (not
            // LogWarning) so the failure shows in any errors-only log filter.
            _logger.LogError(ex,
                "[intent-seed] bootstrap FAILED — hybrid router will operate keyword-only until next restart. Inner exception type: {ExType}",
                ex.GetType().FullName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
