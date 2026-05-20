using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abs.FixedAssets.Services.Voice;

// Sprint 12C / ADR-021 §D1 — Voyage AI HTTP client interface.
//
// Default model: voyage-3-large (1024 dims, halfvec storage).
// API doc: https://docs.voyageai.com/reference/embeddings-api
//
// Two input types per Voyage's asymmetric-embedding contract:
//   - document — for entity embeds stored in DB
//   - query    — for voice-query embeds in the intent router
// Mixing them improves retrieval ~3-5% (per Voyage's MTEB results).
public interface IVoyageClient
{
    /// <summary>
    /// Embed a batch of texts as `document` inputs. Returns the embeddings
    /// in the same order as inputs. Throws VoyageException on persistent
    /// failure (after the retry envelope already gave up).
    /// </summary>
    Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct);

    /// <summary>
    /// Embed a single text as a `query` input. Used by the voice intent
    /// router for per-utterance embeds.
    /// </summary>
    Task<float[]> EmbedQueryAsync(string text, CancellationToken ct);
}

public sealed class VoyageException : System.Exception
{
    public int StatusCode { get; }
    public VoyageException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
