using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Voice;

// Sprint 12C / ADR-021 §D1 + §D4 — Voyage AI HTTP client implementation.
//
// Retry policy (ADR-021 §D4): exponential backoff 1→2→4→8→16s on
// 429/5xx/network, max 5 attempts, then throw VoyageException. The
// caller (worker for batch; voice endpoint for per-query) handles the
// failure mode appropriate to its context (mark row attempts++ for
// the worker; fall back to keyword routing for voice).
//
// API key resolved from env var VOYAGE_API_KEY at construction time.
public sealed class VoyageClient : IVoyageClient
{
    private const string ApiEndpoint = "https://api.voyageai.com/v1/embeddings";
    private const string ModelId = "voyage-3-large";
    private const int MaxAttempts = 5;

    // Sprint 12C PR #1.5 — backoff hardened against Voyage free-tier
    // rate limits (empirically ~3 req burst then 429 for 30+ seconds).
    // Initial 10s gives the bucket time to refill even on free tier;
    // paid tier (2K req/min) makes this overkill but harmless.
    // Cap at 60s per ADR-021 §D4 spirit: never starve the worker for
    // longer than the poll interval would naturally take.
    private static readonly TimeSpan[] BackoffSchedule =
    {
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(40),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(60),
    };

    // Cap for a server-supplied Retry-After header (in case Voyage ever
    // sends an absurd value). Same 60s ceiling as the backoff schedule.
    private static readonly TimeSpan RetryAfterCap = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly ILogger<VoyageClient> _logger;
    private readonly string _apiKey;

    public VoyageClient(HttpClient http, ILogger<VoyageClient> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("VOYAGE_API_KEY") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning(
                "VOYAGE_API_KEY env var is not set. Voyage embedding calls will fail until it is configured in Replit Secrets.");
        }
    }

    public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct)
        => EmbedAsync(texts, inputType: "document", ct);

    public async Task<float[]> EmbedQueryAsync(string text, CancellationToken ct)
    {
        var batch = await EmbedAsync(new[] { text }, inputType: "query", ct);
        return batch[0];
    }

    private async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        string inputType,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new VoyageException(0,
                "VOYAGE_API_KEY env var is not set; cannot call Voyage API.");
        }
        if (texts.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        var request = new VoyageEmbedRequest
        {
            Input = texts,
            Model = ModelId,
            InputType = inputType,
            Truncation = true,
        };

        Exception? lastException = null;
        TimeSpan? lastRetryAfter = null;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                using var msg = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint)
                {
                    Content = JsonContent.Create(request),
                };
                msg.Headers.Add("Authorization", $"Bearer {_apiKey}");

                using var response = await _http.SendAsync(msg, ct);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadFromJsonAsync<VoyageEmbedResponse>(ct);
                    if (body?.Data is null)
                    {
                        throw new VoyageException((int)response.StatusCode,
                            "Voyage response had no data field.");
                    }
                    // Voyage returns items in input-order; cast to float[][].
                    var vectors = new float[body.Data.Count][];
                    foreach (var item in body.Data)
                    {
                        vectors[item.Index] = item.Embedding ?? Array.Empty<float>();
                    }
                    return vectors;
                }

                // Retryable: 429 (rate limit) + 5xx (server-side).
                bool retryable = response.StatusCode == HttpStatusCode.TooManyRequests
                    || ((int)response.StatusCode >= 500 && (int)response.StatusCode <= 599);
                var errBody = await response.Content.ReadAsStringAsync(ct);

                if (!retryable)
                {
                    throw new VoyageException((int)response.StatusCode,
                        $"Voyage rejected request: {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(errBody, 200)}");
                }

                // Sprint 12C PR #1.5 — honor server-supplied Retry-After
                // (RFC 7231 §7.1.3 — either a delta-seconds integer or a
                // HTTP-date). Falls back to the per-attempt schedule if
                // the server didn't send one. We override the next sleep
                // by stashing in `serverRetryAfter`.
                var serverRetryAfter = ReadRetryAfter(response);
                if (serverRetryAfter.HasValue)
                {
                    _logger.LogWarning(
                        "Voyage transient failure (attempt {Attempt}/{Max}): {Status} — server Retry-After {Delay}s",
                        attempt + 1, MaxAttempts, (int)response.StatusCode,
                        serverRetryAfter.Value.TotalSeconds);
                }
                else
                {
                    _logger.LogWarning(
                        "Voyage transient failure (attempt {Attempt}/{Max}): {Status} — backing off {Delay}s",
                        attempt + 1, MaxAttempts, (int)response.StatusCode,
                        BackoffSchedule[attempt].TotalSeconds);
                }

                lastException = new VoyageException((int)response.StatusCode,
                    $"Transient failure: {(int)response.StatusCode}");
                lastRetryAfter = serverRetryAfter;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // HttpClient timeout — retryable.
                _logger.LogWarning(
                    "Voyage timeout (attempt {Attempt}/{Max}) — backing off {Delay}s",
                    attempt + 1, MaxAttempts, BackoffSchedule[attempt].TotalSeconds);
                lastException = new VoyageException(0, "Voyage request timed out.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex,
                    "Voyage network error (attempt {Attempt}/{Max}) — backing off {Delay}s",
                    attempt + 1, MaxAttempts, BackoffSchedule[attempt].TotalSeconds);
                lastException = new VoyageException(0, $"Network: {ex.Message}");
            }

            if (attempt + 1 < MaxAttempts)
            {
                // Prefer server-supplied Retry-After if present; otherwise
                // fall back to the per-attempt schedule. Always capped.
                var delay = lastRetryAfter ?? BackoffSchedule[attempt];
                if (delay > RetryAfterCap) delay = RetryAfterCap;
                await Task.Delay(delay, ct);
                lastRetryAfter = null;  // only honor once
            }
        }

        throw lastException ?? new VoyageException(0, "Voyage embed gave up after retries.");
    }

    /// <summary>
    /// Read RFC 7231 §7.1.3 Retry-After header. Either delta-seconds or
    /// an HTTP-date. Returns null if absent / unparseable.
    /// </summary>
    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var ra = response.Headers.RetryAfter;
        if (ra is null) return null;
        if (ra.Delta.HasValue) return ra.Delta.Value;
        if (ra.Date.HasValue)
        {
            var delta = ra.Date.Value - DateTimeOffset.UtcNow;
            if (delta > TimeSpan.Zero) return delta;
        }
        return null;
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s[..max] + "…";
    }

    private sealed class VoyageEmbedRequest
    {
        [JsonPropertyName("input")] public IReadOnlyList<string> Input { get; set; } = Array.Empty<string>();
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("input_type")] public string InputType { get; set; } = "document";
        [JsonPropertyName("truncation")] public bool Truncation { get; set; } = true;
    }

    private sealed class VoyageEmbedResponse
    {
        [JsonPropertyName("data")] public List<VoyageEmbedDatum>? Data { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("usage")] public VoyageUsage? Usage { get; set; }
    }

    private sealed class VoyageEmbedDatum
    {
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("embedding")] public float[]? Embedding { get; set; }
    }

    private sealed class VoyageUsage
    {
        [JsonPropertyName("total_tokens")] public int TotalTokens { get; set; }
    }
}
