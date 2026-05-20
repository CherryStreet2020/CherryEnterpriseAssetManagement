using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services.Voice;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Endpoints;

// Sprint 12C / ADR-021 — admin endpoints for the bulk-backfill flow.
//
// POST /_admin/embed/backfill?entity=ReceiptProfile
//   → Enqueues every existing row of the named entity type into
//     PendingEmbeddings. Worker drains the queue async. Returns
//     immediately with the count enqueued.
//
// GET /_admin/embed/status
//   → Reports queue depth + Embeddings table size + failure count.
//     Useful for live verify + Shadi demo monitoring.
//
// Auth: behind .RequireAuthorization() at MapVoiceEndpoints-style.
public static class AdminEmbedEndpoint
{
    public static IEndpointConventionBuilder MapAdminEmbedEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/_admin/embed");

        grp.MapPost("/backfill", BackfillAsync).WithName("AdminEmbedBackfill");
        grp.MapGet("/status", StatusAsync).WithName("AdminEmbedStatus");
        // Sprint 12C PR #1.5 — reset failed rows so the worker re-tries.
        // Used when a rate-limit or transient outage pushed rows past
        // the Attempts=5 cap. Saves manual psql.
        grp.MapPost("/retry", RetryFailedAsync).WithName("AdminEmbedRetry");

        return grp;
    }

    /// <summary>
    /// Reset Attempts=0 + LastError=NULL on all rows where Attempts ≥ 5.
    /// Worker will re-attempt on next 5s poll. Use after the underlying
    /// cause has been fixed (rate-limit recovered, API key updated, etc.).
    /// </summary>
    private static async Task<IResult> RetryFailedAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var reset = await db.PendingEmbeddings
            .Where(p => p.Attempts >= 5)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(p => p.Attempts, 0)
                .SetProperty(p => p.LastError, (string?)null),
                ct);

        return Results.Ok(new
        {
            ok = true,
            resetRows = reset,
            note = "Failed rows reset. Worker will re-attempt within the next 5s poll."
        });
    }

    private static async Task<IResult> BackfillAsync(
        HttpContext ctx,
        IEmbeddingBackfillService backfill,
        string? entity,
        CancellationToken ct)
    {
        entity ??= EmbeddingSourceText.EntityTypeReceiptProfile;

        int count;
        switch (entity)
        {
            case EmbeddingSourceText.EntityTypeReceiptProfile:
                count = await backfill.EnqueueAllReceiptProfilesAsync(ct);
                break;
            case EmbeddingSourceText.EntityTypeItem:
                count = await backfill.EnqueueAllItemsAsync(ct);
                break;
            case EmbeddingSourceText.EntityTypeVendor:
                count = await backfill.EnqueueAllVendorsAsync(ct);
                break;
            case EmbeddingSourceText.EntityTypeWorkOrder:
                count = await backfill.EnqueueAllWorkOrdersAsync(ct);
                break;
            case EmbeddingSourceText.EntityTypeAiCommand:
                count = await backfill.EnqueueAllAuditAiCommandsAsync(ct);
                break;
            default:
                return Results.BadRequest(new
                {
                    ok = false,
                    error = $"Unknown entity '{entity}'. Supported: " +
                            $"'{EmbeddingSourceText.EntityTypeReceiptProfile}', " +
                            $"'{EmbeddingSourceText.EntityTypeItem}', " +
                            $"'{EmbeddingSourceText.EntityTypeVendor}', " +
                            $"'{EmbeddingSourceText.EntityTypeWorkOrder}', " +
                            $"'{EmbeddingSourceText.EntityTypeAiCommand}'."
                });
        }

        return Results.Ok(new
        {
            ok = true,
            entity,
            enqueued = count,
            modelVersion = "voyage-3-large/v1",
            note = "Worker will process the queue in the background. Poll /_admin/embed/status to monitor."
        });
    }

    private static async Task<IResult> StatusAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var queueDepth = await db.PendingEmbeddings.CountAsync(ct);
        var failedRows = await db.PendingEmbeddings.CountAsync(p => p.Attempts >= 5, ct);
        var embeddingsCount = await db.Embeddings.CountAsync(ct);
        var byType = await db.Embeddings
            .GroupBy(e => e.EntityType)
            .Select(g => new { entityType = g.Key, count = g.Count() })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            ok = true,
            queueDepth,
            failedRows,
            embeddingsCount,
            byType,
        });
    }
}
