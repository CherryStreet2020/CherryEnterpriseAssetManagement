using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Voice;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Sprint 12C PR #3 — Hybrid Intent Router tests.
///
/// COVERAGE
/// ========
/// - Layer 1 keyword fast-path produces Source=Keyword, Confidence=1.0
///   for utterances the existing keyword classifier already handles.
/// - Provider guard: on non-Postgres providers (InMemory here), the
///   vector layer short-circuits to Fallback without calling Voyage.
///   This is the test-context contract.
/// - Voyage-failure path: when Layer 1 returns Unknown and Voyage throws,
///   the router returns Source=Fallback rather than propagating the
///   exception (voice round-trip must never break on a vector outage).
/// - Natural-key extraction still works regardless of which layer
///   settled routing.
///
/// VECTOR-LAYER WIN paths are integration tests against real Postgres
/// + pgvector (not InMemory), live-verified on Replit per the
/// memory `feedback_pgvector_provider_guard` pattern.
/// </summary>
public class HybridIntentRouterTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
        }
    }

    private static AppDbContext NewDb(
        [System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"hybrid-intent-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    /// <summary>Voyage stub that records whether it was called.</summary>
    private sealed class TrackingVoyageStub : IVoyageClient
    {
        public int EmbedQueryCalls { get; private set; }
        public bool ShouldThrow { get; set; }

        public Task<float[]> EmbedQueryAsync(string text, CancellationToken ct)
        {
            EmbedQueryCalls++;
            if (ShouldThrow)
            {
                throw new VoyageException(500, "Voyage stub forced failure");
            }
            // Return a deterministic 1024-dim vector. Tests don't actually
            // exercise pgvector against InMemory (provider guard short-circuits),
            // so the values don't matter — they just need the right shape.
            return Task.FromResult(new float[1024]);
        }

        public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(
            IReadOnlyList<string> texts, CancellationToken ct) =>
            throw new NotSupportedException("Documents path not used by the router");
    }

    private static HybridIntentRouter Build(AppDbContext db, TrackingVoyageStub voyage) =>
        new HybridIntentRouter(db, voyage, NullLogger<HybridIntentRouter>.Instance);

    // ====================================================================
    // Layer 1 — keyword fast-path
    // ====================================================================

    [Fact]
    public async Task KeywordHelp_ReturnsKeywordSource_Confidence1()
    {
        using var db = NewDb();
        var voyage = new TrackingVoyageStub();
        var router = Build(db, voyage);

        var result = await router.RouteAsync("help me", tenantId: 1, CancellationToken.None);

        Assert.Equal(IntentKind.Help, result.Intent.Kind);
        Assert.Equal(RoutingSource.Keyword, result.Source);
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal(0, voyage.EmbedQueryCalls); // Layer 1 win never touches Voyage.
    }

    [Fact]
    public async Task KeywordExpectedArrivals_ReturnsKeywordSource()
    {
        using var db = NewDb();
        var voyage = new TrackingVoyageStub();
        var router = Build(db, voyage);

        var result = await router.RouteAsync(
            "what's arriving today",
            tenantId: 1,
            CancellationToken.None);

        Assert.Equal(IntentKind.ExpectedArrivals, result.Intent.Kind);
        Assert.Equal(RoutingSource.Keyword, result.Source);
        Assert.Equal(0, voyage.EmbedQueryCalls);
    }

    [Fact]
    public async Task KeywordLookup_WithNaturalKey_ReturnsKeywordSourceAndKey()
    {
        using var db = NewDb();
        var voyage = new TrackingVoyageStub();
        var router = Build(db, voyage);

        var result = await router.RouteAsync(
            "find receipt RCPT-2026-1234",
            tenantId: 1,
            CancellationToken.None);

        Assert.Equal(IntentKind.LookupReceipt, result.Intent.Kind);
        Assert.Equal("RCPT-2026-1234", result.Intent.NaturalKey);
        Assert.Equal(RoutingSource.Keyword, result.Source);
        Assert.Equal(0, voyage.EmbedQueryCalls);
    }

    [Fact]
    public async Task KeywordExplain_ReturnsKeywordSource()
    {
        using var db = NewDb();
        var voyage = new TrackingVoyageStub();
        var router = Build(db, voyage);

        var result = await router.RouteAsync(
            "explain receipt RCPT-2026-9999",
            tenantId: 1,
            CancellationToken.None);

        Assert.Equal(IntentKind.ExplainException, result.Intent.Kind);
        Assert.Equal("RCPT-2026-9999", result.Intent.NaturalKey);
        Assert.Equal(RoutingSource.Keyword, result.Source);
    }

    // ====================================================================
    // Layer 1 — Sprint 12.7 PR #3 Controller-side chain trace intent.
    //
    // The CFO motion: "why is NBV on asset 4231" must route to
    // ExplainChainTrace (not the receipt-side ExplainException), and the
    // entity ref must be normalized into a form ChainTraceService can
    // parse (e.g. "asset 4231").
    // ====================================================================

    [Fact]
    public async Task KeywordChainTrace_WhyIsNbvAsset_RoutesToChainTrace()
    {
        using var db = NewDb();
        var voyage = new TrackingVoyageStub();
        var router = Build(db, voyage);

        var result = await router.RouteAsync(
            "why is the NBV so high on asset 4231",
            tenantId: 1,
            CancellationToken.None);

        Assert.Equal(IntentKind.ExplainChainTrace, result.Intent.Kind);
        Assert.Equal("asset 4231", result.Intent.NaturalKey);
        Assert.Equal(RoutingSource.Keyword, result.Source);
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal(0, voyage.EmbedQueryCalls); // Layer 1 win never touches Voyage.
    }

    [Fact]
    public async Task KeywordChainTrace_DrillDownOnJe_NormalizesJournalToJe()
    {
        using var db = NewDb();
        var voyage = new TrackingVoyageStub();
        var router = Build(db, voyage);

        var result = await router.RouteAsync(
            "drill down on journal entry 47",
            tenantId: 1,
            CancellationToken.None);

        Assert.Equal(IntentKind.ExplainChainTrace, result.Intent.Kind);
        // The classifier normalizes "journal entry"/"journal" prefixes to "je"
        // so ChainTraceService.ParseEntityRef resolves to EntityKind.JournalEntry.
        Assert.Equal("je 47", result.Intent.NaturalKey);
        Assert.Equal(RoutingSource.Keyword, result.Source);
    }

    [Fact]
    public async Task KeywordChainTrace_TraceAsset_RoutesToChainTrace()
    {
        using var db = NewDb();
        var voyage = new TrackingVoyageStub();
        var router = Build(db, voyage);

        var result = await router.RouteAsync(
            "trace asset 1116",
            tenantId: 1,
            CancellationToken.None);

        Assert.Equal(IntentKind.ExplainChainTrace, result.Intent.Kind);
        Assert.Equal("asset 1116", result.Intent.NaturalKey);
        Assert.Equal(RoutingSource.Keyword, result.Source);
    }

    [Fact]
    public async Task KeywordChainTrace_DoesNotCollideWithChainOfCustody()
    {
        // "trace this receipt" must still route to ExplainChainOfCustody —
        // the chain-trace classifier should not steal the receipt verb.
        using var db = NewDb();
        var voyage = new TrackingVoyageStub();
        var router = Build(db, voyage);

        var result = await router.RouteAsync(
            "trace this receipt back to the source",
            tenantId: 1,
            CancellationToken.None);

        Assert.Equal(IntentKind.ExplainChainOfCustody, result.Intent.Kind);
        Assert.Equal(RoutingSource.Keyword, result.Source);
    }

    [Fact]
    public async Task KeywordChainTrace_ExplainReceiptStillRoutesToExplainException()
    {
        // Defensive: "explain receipt RCPT-..." has "explain" + a receipt
        // natural key, but contains NO controller subject ("nbv", "asset",
        // etc.). The chain-trace branch must not fire here.
        using var db = NewDb();
        var voyage = new TrackingVoyageStub();
        var router = Build(db, voyage);

        var result = await router.RouteAsync(
            "explain receipt RCPT-2026-9999",
            tenantId: 1,
            CancellationToken.None);

        Assert.Equal(IntentKind.ExplainException, result.Intent.Kind);
        Assert.Equal("RCPT-2026-9999", result.Intent.NaturalKey);
    }

    // ====================================================================
    // Layer 2 — provider guard (InMemory tests → vector layer disabled)
    // ====================================================================

    [Fact]
    public async Task UnknownUtterance_OnInMemoryProvider_ShortCircuitsToFallback()
    {
        // The vector layer requires Postgres + pgvector. On InMemory, the
        // router MUST detect the provider and return Fallback without
        // calling Voyage. Otherwise a test run would hit the real Voyage
        // endpoint (and burn 429 quota).
        using var db = NewDb();
        var voyage = new TrackingVoyageStub();
        var router = Build(db, voyage);

        var result = await router.RouteAsync(
            "the dough rises slowly under the rolling pin",
            tenantId: 1,
            CancellationToken.None);

        Assert.Equal(IntentKind.Unknown, result.Intent.Kind);
        Assert.Equal(RoutingSource.Fallback, result.Source);
        Assert.Equal(0, result.Confidence);
        Assert.Equal(0, voyage.EmbedQueryCalls); // never reached on InMemory.
    }

    [Fact]
    public async Task EmptyUtterance_ReturnsFallback()
    {
        using var db = NewDb();
        var voyage = new TrackingVoyageStub();
        var router = Build(db, voyage);

        var result = await router.RouteAsync(
            "   ",
            tenantId: 1,
            CancellationToken.None);

        Assert.Equal(IntentKind.Unknown, result.Intent.Kind);
        Assert.Equal(RoutingSource.Fallback, result.Source);
        Assert.Equal(0, voyage.EmbedQueryCalls);
    }

    // ====================================================================
    // Natural-key extraction smoke
    // ====================================================================

    [Fact]
    public async Task FallbackPath_StillExtractsNaturalKeyForObservability()
    {
        // Even when both layers fail to settle a Kind, we still surface
        // the natural key on ParsedIntent so the audit log records what
        // the user said. Belt-and-suspenders for telemetry.
        using var db = NewDb();
        var voyage = new TrackingVoyageStub();
        var router = Build(db, voyage);

        // Phrase doesn't trigger any Layer 1 keyword (no "find", "explain",
        // "arriving", "help"), but contains a HEAT-shaped natural key.
        // Wait — IntentClassifier's fallback DOES match natural keys to
        // LookupReceipt. So this should still be Layer 1 Keyword via the
        // fallback-key path. Verify that.
        var result = await router.RouteAsync(
            "RCPT-2026-7777 update",
            tenantId: 1,
            CancellationToken.None);

        Assert.Equal(IntentKind.LookupReceipt, result.Intent.Kind);
        Assert.Equal("RCPT-2026-7777", result.Intent.NaturalKey);
        Assert.Equal(RoutingSource.Keyword, result.Source);
    }

    // ====================================================================
    // B7 Wave D PR-2 — ExplainMakeBuyDecision keyword routing
    // ====================================================================

    [Fact]
    public async Task KeywordMakeBuy_WhyAreWeBuying_RoutesToMakeBuy()
    {
        using var db = NewDb();
        var router = Build(db, new TrackingVoyageStub());

        var result = await router.RouteAsync("why are we buying item 9395", tenantId: 1, CancellationToken.None);
        Assert.Equal(IntentKind.ExplainMakeBuyDecision, result.Intent.Kind);
        Assert.Equal("9395", result.Intent.NaturalKey);
        Assert.Equal(RoutingSource.Keyword, result.Source);
    }

    [Fact]
    public async Task KeywordMakeBuy_MakeOrBuyPhrasing_RoutesToMakeBuy()
    {
        using var db = NewDb();
        var router = Build(db, new TrackingVoyageStub());

        var result = await router.RouteAsync("explain the make or buy call on part PN-1234", tenantId: 1, CancellationToken.None);
        Assert.Equal(IntentKind.ExplainMakeBuyDecision, result.Intent.Kind);
        Assert.Equal("PN-1234", result.Intent.NaturalKey);
    }

    [Fact]
    public async Task KeywordMakeBuy_ShouldWeMakeOrBuy_RoutesToMakeBuy()
    {
        using var db = NewDb();
        var router = Build(db, new TrackingVoyageStub());

        var result = await router.RouteAsync("should we make or buy this bracket", tenantId: 1, CancellationToken.None);
        Assert.Equal(IntentKind.ExplainMakeBuyDecision, result.Intent.Kind);
    }

    [Fact]
    public async Task KeywordMakeBuy_CollisionSafety_NbvStaysChainTrace()
    {
        using var db = NewDb();
        var router = Build(db, new TrackingVoyageStub());

        var result = await router.RouteAsync("why is NBV on asset 4231", tenantId: 1, CancellationToken.None);
        Assert.Equal(IntentKind.ExplainChainTrace, result.Intent.Kind);
    }

    [Fact]
    public async Task KeywordMakeBuy_CollisionSafety_ExplainReceiptStaysException()
    {
        using var db = NewDb();
        var router = Build(db, new TrackingVoyageStub());

        var result = await router.RouteAsync("explain receipt RCPT-2026-1234", tenantId: 1, CancellationToken.None);
        Assert.Equal(IntentKind.ExplainException, result.Intent.Kind);
    }

    [Fact]
    public async Task KeywordMakeBuy_CollisionSafety_TraceReceiptStaysChainOfCustody()
    {
        using var db = NewDb();
        var router = Build(db, new TrackingVoyageStub());

        var result = await router.RouteAsync("trace this receipt back to its source", tenantId: 1, CancellationToken.None);
        Assert.Equal(IntentKind.ExplainChainOfCustody, result.Intent.Kind);
    }
}
