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

    private static readonly TimeSpan[] BackoffSchedule =
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16),
    };

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

                _logger.LogWarning(
                    "Voyage transient failure (attempt {Attempt}/{Max}): {Status} — backing off {Delay}s",
                    attempt + 1, MaxAttempts, (int)response.StatusCode,
                    BackoffSchedule[attempt].TotalSeconds);
                lastException = new VoyageException((int)response.StatusCode,
                    $"Transient failure: {(int)response.StatusCode}");
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
                await Task.Delay(BackoffSchedule[attempt], ct);
            }
        }

        throw lastException ?? new VoyageException(0, "Voyage embed gave up after retries.");
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
