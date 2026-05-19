using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Abs.FixedAssets.Services.Navigation.Cockpit;
using Xunit;

namespace Abs.FixedAssets.Tests;

// ADR-018 §D4 + §D6 unit tests — bucket boundaries for ByTimeLens + opt-in
// behavior for CockpitPreviewSerializer. Pin the clock + timezone so tests
// are deterministic regardless of the runner's machine settings.
public class CockpitInfraTests
{
    // ---- Test helpers -----------------------------------------------------

    private sealed class FakeRow : ICockpitQueueRow
    {
        public string Id { get; set; } = "";
        public string Primary { get; set; } = "";
        public string Secondary { get; set; } = "";
        public DateTime? RequiredAt { get; set; }
        public string Tone { get; set; } = "neutral";
        public IReadOnlyList<MetaTriple> Meta { get; set; } = Array.Empty<MetaTriple>();
    }

    // Pin "today" to 2026-05-18 12:00 local in a fixed timezone. Most tests
    // use America/New_York to mirror Dean's typical timezone; tz-edge tests
    // use America/Los_Angeles to prove tz handling.
    private static readonly TimeZoneInfo Eastern =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");
    private static readonly TimeZoneInfo Pacific =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Pacific Standard Time" : "America/Los_Angeles");

    private static DateTime NoonOn(int year, int month, int day)
        => new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Unspecified);

    private static ByTimeLens<FakeRow> LensAt(DateTime localNow, TimeZoneInfo? tz = null)
        => new ByTimeLens<FakeRow>(() => localNow, tz ?? Eastern);

    // ---- Bucket boundary tests --------------------------------------------

    [Fact]
    public void Empty_input_returns_empty_groups()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var groups = lens.Group(Array.Empty<FakeRow>());
        Assert.Empty(groups);
    }

    [Fact]
    public void Row_required_yesterday_lands_in_overdue()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var rows = new[] { new FakeRow { Id = "1", RequiredAt = NoonOn(2026, 5, 17) } };
        var groups = lens.Group(rows);
        Assert.Single(groups);
        Assert.Equal("overdue", groups[0].Code);
        Assert.Equal("danger", groups[0].Tone);
        Assert.Equal("1", groups[0].Rows[0].Id);
    }

    [Fact]
    public void Row_required_today_lands_in_today_not_overdue()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var rows = new[] { new FakeRow { Id = "1", RequiredAt = NoonOn(2026, 5, 18) } };
        var groups = lens.Group(rows);
        Assert.Equal("today", Assert.Single(groups).Code);
    }

    [Fact]
    public void Row_required_at_midnight_today_still_lands_in_today()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var rows = new[]
        {
            new FakeRow { Id = "1", RequiredAt = new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Unspecified) }
        };
        var groups = lens.Group(rows);
        Assert.Equal("today", Assert.Single(groups).Code);
    }

    [Fact]
    public void Row_required_one_second_before_midnight_yesterday_is_overdue()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var rows = new[]
        {
            new FakeRow { Id = "1", RequiredAt = new DateTime(2026, 5, 17, 23, 59, 59, DateTimeKind.Unspecified) }
        };
        var groups = lens.Group(rows);
        Assert.Equal("overdue", Assert.Single(groups).Code);
    }

    [Fact]
    public void Row_required_tomorrow_lands_in_this_week()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var rows = new[] { new FakeRow { Id = "1", RequiredAt = NoonOn(2026, 5, 19) } };
        var groups = lens.Group(rows);
        Assert.Equal("this-week", Assert.Single(groups).Code);
    }

    [Fact]
    public void Row_required_seven_days_out_lands_in_this_week_inclusive()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var rows = new[] { new FakeRow { Id = "1", RequiredAt = NoonOn(2026, 5, 25) } };
        var groups = lens.Group(rows);
        Assert.Equal("this-week", Assert.Single(groups).Code);
    }

    [Fact]
    public void Row_required_eight_days_out_lands_in_later()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var rows = new[] { new FakeRow { Id = "1", RequiredAt = NoonOn(2026, 5, 26) } };
        var groups = lens.Group(rows);
        Assert.Equal("later", Assert.Single(groups).Code);
    }

    [Fact]
    public void Row_with_null_required_at_lands_in_later()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var rows = new[] { new FakeRow { Id = "1", RequiredAt = null } };
        var groups = lens.Group(rows);
        Assert.Equal("later", Assert.Single(groups).Code);
    }

    [Fact]
    public void All_four_buckets_populate_and_render_in_canonical_order()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var rows = new[]
        {
            new FakeRow { Id = "later",    RequiredAt = NoonOn(2026, 6, 30) },
            new FakeRow { Id = "today",    RequiredAt = NoonOn(2026, 5, 18) },
            new FakeRow { Id = "overdue",  RequiredAt = NoonOn(2026, 5, 10) },
            new FakeRow { Id = "thisweek", RequiredAt = NoonOn(2026, 5, 22) },
            new FakeRow { Id = "null",     RequiredAt = null },
        };
        var groups = lens.Group(rows);
        Assert.Equal(new[] { "overdue", "today", "this-week", "later" }, groups.Select(g => g.Code));
        // Both null-RequiredAt and 2026-06-30 land in "later" bucket.
        Assert.Equal(2, groups.Last().Rows.Count);
    }

    [Fact]
    public void Empty_buckets_are_skipped()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var rows = new[] { new FakeRow { Id = "1", RequiredAt = NoonOn(2026, 6, 30) } };
        var groups = lens.Group(rows);
        Assert.Single(groups);
        Assert.Equal("later", groups[0].Code);
    }

    [Fact]
    public void Rows_within_a_bucket_are_sorted_ascending_by_required_at()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var rows = new[]
        {
            new FakeRow { Id = "B", RequiredAt = NoonOn(2026, 5, 12) },
            new FakeRow { Id = "A", RequiredAt = NoonOn(2026, 5, 10) },
            new FakeRow { Id = "C", RequiredAt = NoonOn(2026, 5, 17) },
        };
        var groups = lens.Group(rows);
        Assert.Equal(new[] { "A", "B", "C" }, groups[0].Rows.Select(r => r.Id));
    }

    [Fact]
    public void Group_labels_include_counts()
    {
        var lens = LensAt(NoonOn(2026, 5, 18));
        var rows = new[]
        {
            new FakeRow { Id = "1", RequiredAt = NoonOn(2026, 5, 17) },
            new FakeRow { Id = "2", RequiredAt = NoonOn(2026, 5, 16) },
            new FakeRow { Id = "3", RequiredAt = NoonOn(2026, 5, 15) },
        };
        var groups = lens.Group(rows);
        Assert.Equal("Overdue (3)", groups[0].Label);
    }

    // ---- Timezone edge tests ---------------------------------------------

    [Fact]
    public void Utc_RequiredAt_late_eastern_does_not_leak_into_overdue_on_pacific()
    {
        // RequiredAt is 2026-05-18 23:00 Eastern, which is 2026-05-18 20:00 Pacific
        // (same calendar day Pacific). With "today" pinned to 2026-05-18 noon Pacific,
        // this row must be "today" not "overdue".
        var utc = new DateTime(2026, 5, 19, 3, 0, 0, DateTimeKind.Utc); // 23:00 EDT == 20:00 PDT
        var lens = LensAt(NoonOn(2026, 5, 18), Pacific);
        var rows = new[] { new FakeRow { Id = "1", RequiredAt = utc } };
        var groups = lens.Group(rows);
        Assert.Equal("today", Assert.Single(groups).Code);
    }

    [Fact]
    public void Utc_clock_input_converts_correctly_against_local_required_at()
    {
        // Clock returns UTC now; lens must translate to local "today".
        // 2026-05-18 04:00 UTC == 2026-05-18 00:00 EDT (start of day Eastern).
        // A row required 2026-05-17 12:00 EDT must be "overdue".
        var utcNow = new DateTime(2026, 5, 18, 4, 0, 0, DateTimeKind.Utc);
        var lens = new ByTimeLens<FakeRow>(() => utcNow, Eastern);
        var rows = new[] { new FakeRow { Id = "1", RequiredAt = NoonOn(2026, 5, 17) } };
        var groups = lens.Group(rows);
        Assert.Equal("overdue", Assert.Single(groups).Code);
    }

    // ---- Identity + metadata tests ---------------------------------------

    [Fact]
    public void Lens_identity_is_by_time()
    {
        var lens = new ByTimeLens<FakeRow>();
        Assert.Equal("by-time", lens.Code);
        Assert.Equal("By required date", lens.Label);
    }

    [Fact]
    public void Default_constructor_does_not_throw()
    {
        // Just exercises the System.Local / DateTime.Now path.
        _ = new ByTimeLens<FakeRow>();
    }

    // ---- CockpitPreviewSerializer tests -----------------------------------

    private sealed class PreviewSample
    {
        [CockpitPreviewVisible]
        public string PoNumber { get; set; } = "";

        [CockpitPreviewVisible("vendor")]
        public string VendorName { get; set; } = "";

        [CockpitPreviewVisible]
        public decimal Total { get; set; }

        // No attribute — must NOT be emitted (this is the safety guarantee).
        public string InternalScoringNote { get; set; } = "";
    }

    [Fact]
    public void Serializer_emits_only_attributed_properties()
    {
        CockpitPreviewSerializer.ClearCacheForTests();
        var json = CockpitPreviewSerializer.Serialize(new PreviewSample
        {
            PoNumber = "PO-PWH-0059",
            VendorName = "ROCKWELL AUTOMATION",
            Total = 4164.73m,
            InternalScoringNote = "DO NOT LEAK"
        });

        // Parse + assert exact key set.
        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToArray();
        Assert.Equal(new[] { "poNumber", "total", "vendor" }, keys);
        Assert.DoesNotContain("INTERNAL", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LEAK", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serializer_camelcases_property_names_when_attribute_name_omitted()
    {
        CockpitPreviewSerializer.ClearCacheForTests();
        var json = CockpitPreviewSerializer.Serialize(new PreviewSample
        {
            PoNumber = "PO-1",
            VendorName = "ACME",
            Total = 100m
        });
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("PO-1", doc.RootElement.GetProperty("poNumber").GetString());
    }

    [Fact]
    public void Serializer_honors_explicit_attribute_name()
    {
        CockpitPreviewSerializer.ClearCacheForTests();
        var json = CockpitPreviewSerializer.Serialize(new PreviewSample
        {
            PoNumber = "PO-1",
            VendorName = "ACME",
            Total = 0m
        });
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("ACME", doc.RootElement.GetProperty("vendor").GetString());
    }

    [Fact]
    public void Serialize_many_emits_a_json_array()
    {
        CockpitPreviewSerializer.ClearCacheForTests();
        var json = CockpitPreviewSerializer.SerializeMany(new[]
        {
            new PreviewSample { PoNumber = "PO-1", VendorName = "A", Total = 1m },
            new PreviewSample { PoNumber = "PO-2", VendorName = "B", Total = 2m },
        });
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }
}
