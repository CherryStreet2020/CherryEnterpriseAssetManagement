using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Abs.FixedAssets.Tests
{
    // Runtime guard for the Phase 4.1 / Task #17 fail-open redaction
    // contract: when the limiter's DB call throws, the warning log line
    // must NEVER contain the raw (IP, username) partition key. Only the
    // salted SHA-256 keyHash may appear.
    public class PostgresLoginRateLimiterRedactionTests
    {
        private sealed class CapturingLogger : ILogger<PostgresLoginRateLimiter>
        {
            public readonly List<string> Messages = new();
            IDisposable? ILogger.BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Messages.Add(formatter(state, exception));
            }
        }

        // Scope factory whose CreateAsyncScope() is fine, but resolving
        // AppDbContext throws — guaranteeing the limiter walks the catch
        // block where the redacted log line is emitted.
        private sealed class BrokenScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope()
            {
                var sp = new ServiceCollection().BuildServiceProvider();
                return new BrokenScope(sp);
            }
            private sealed class BrokenScope : IServiceScope
            {
                public BrokenScope(IServiceProvider sp) { ServiceProvider = new ThrowingProvider(); }
                public IServiceProvider ServiceProvider { get; }
                public void Dispose() { }
            }
            private sealed class ThrowingProvider : IServiceProvider
            {
                public object? GetService(Type serviceType)
                    => throw new InvalidOperationException("simulated DB outage");
            }
        }

        [Fact]
        public async Task FailOpen_DoesNotLeakRawPartitionKey()
        {
            var logger = new CapturingLogger();
            var cfg = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:LogKeyHashSalt"] = "unit-test-salt-fixed-value",
                })
                .Build();

            var limiter = new PostgresLoginRateLimiter(new BrokenScopeFactory(), logger, cfg);
            const string rawKey = "203.0.113.7:probe-username-leak-canary";

            var allowed = await limiter.TryAcquireAsync(rawKey, CancellationToken.None);

            // Fail-open contract: outage must not lock anyone out.
            Assert.True(allowed, "limiter must fail open on DB error");

            // Exactly one warning emitted.
            Assert.Single(logger.Messages);
            var line = logger.Messages[0];

            // Must not leak any raw component of the partition key.
            Assert.DoesNotContain("203.0.113.7", line);
            Assert.DoesNotContain("probe-username-leak-canary", line);
            Assert.DoesNotContain(rawKey, line);

            // Must contain the salted keyHash and only the keyHash.
            var expectedHash = limiter.ComputeLogKeyTag(rawKey);
            Assert.Contains(expectedHash, line);
            Assert.Equal(12, expectedHash.Length);
        }

        [Fact]
        public void Hash_IsDeterministic_PerSalt_AndDifferent_AcrossSalts()
        {
            var cfgA = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:LogKeyHashSalt"] = "salt-A",
            }).Build();
            var cfgB = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:LogKeyHashSalt"] = "salt-B",
            }).Build();

            var a1 = new PostgresLoginRateLimiter(new BrokenScopeFactory(), new CapturingLogger(), cfgA)
                .ComputeLogKeyTag("1.2.3.4:bob");
            var a2 = new PostgresLoginRateLimiter(new BrokenScopeFactory(), new CapturingLogger(), cfgA)
                .ComputeLogKeyTag("1.2.3.4:bob");
            var b1 = new PostgresLoginRateLimiter(new BrokenScopeFactory(), new CapturingLogger(), cfgB)
                .ComputeLogKeyTag("1.2.3.4:bob");

            Assert.Equal(a1, a2);
            Assert.NotEqual(a1, b1);
        }
    }
}
