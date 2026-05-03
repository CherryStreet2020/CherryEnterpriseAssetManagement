using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Abs.FixedAssets.Services.Health;

/// <summary>
/// Probes for libSkiaSharp.so on disk without instantiating any SkiaSharp type
/// (touching SkiaSharp before the native lib is present causes a GC-finalizer
/// crash). Mirrors the on-disk probe in BarcodeService.
/// </summary>
public sealed class SkiaHealthCheck : IHealthCheck
{
    private static readonly string[] CandidatePaths = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "libSkiaSharp.so"),
        Path.Combine(AppContext.BaseDirectory, "runtimes", "linux-x64", "native", "libSkiaSharp.so"),
    };

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var candidate in CandidatePaths)
        {
            if (File.Exists(candidate))
            {
                return Task.FromResult(HealthCheckResult.Healthy(
                    $"libSkiaSharp.so present at {candidate}"));
            }
        }
        return Task.FromResult(HealthCheckResult.Degraded(
            "libSkiaSharp.so not found on disk; barcode/label endpoints will return HTTP 503."));
    }
}
