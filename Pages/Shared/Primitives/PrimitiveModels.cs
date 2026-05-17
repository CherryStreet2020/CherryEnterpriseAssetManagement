// =============================================================================
// CherryAI EAM — Design System Primitive view-models
// PR #116d.1 — typed view-models for Razor primitive partials.
// Subsequent #116d.1b PR will add: DataTableModel, EmptyStateModel,
// SkeletonLoaderModel, ContextDrawerModel, ButtonGroupModel, BrandChipModel.
// =============================================================================

using System.Collections.Generic;

namespace Abs.FixedAssets.Pages.Shared.Primitives;

/// <summary>
/// Universal "DataCard" primitive — the canonical surface for any bounded content
/// object on the page. Supports a tone for accent (success/warning/critical/etc.),
/// an optional image strip (image-left variant), eyebrow row, body slot, footer,
/// and floating badges. Hover-lifts when <see cref="Interactive"/> is true.
/// </summary>
public sealed class DataCardModel
{
    /// <summary>Visible above the title in 11px caps + tracking.</summary>
    public string? Eyebrow { get; set; }

    /// <summary>Main heading (Inter, 18px, weight 700).</summary>
    public string? Title { get; set; }

    /// <summary>Optional supporting line under the title.</summary>
    public string? Subtitle { get; set; }

    /// <summary>Tone: neutral | brand | info | success | warning | critical.</summary>
    public string Tone { get; set; } = "neutral";

    /// <summary>If true, card gains hover-lift + cursor + role=button.</summary>
    public bool Interactive { get; set; }

    /// <summary>If set, the card root becomes an &lt;a&gt; with this href.</summary>
    public string? Href { get; set; }

    /// <summary>If image-left variant, this URL paints the imagery column.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Optional CSS class applied to the image div (brand tint).</summary>
    public string? ImageBrandClass { get; set; }

    /// <summary>Optional AI-storyline narrative shown below subtitle.</summary>
    public string? Storyline { get; set; }

    /// <summary>Floating badges over the image (image-left variant).</summary>
    public List<FloatingBadgeModel> FloatingBadges { get; set; } = new();

    /// <summary>Optional footer text/links.</summary>
    public string? Footer { get; set; }

    /// <summary>Optional extra CSS classes appended to the root element.</summary>
    public string? ExtraClasses { get; set; }
}

public sealed class FloatingBadgeModel
{
    /// <summary>top-left | top-right | bottom-left | bottom-right.</summary>
    public string Position { get; set; } = "top-left";

    /// <summary>Tone for the leading dot.</summary>
    public string Tone { get; set; } = "neutral";

    /// <summary>Mono-numeric snippet shown first (e.g. "30").</summary>
    public string? MonoNum { get; set; }

    /// <summary>Trailing label (e.g. "Critical").</summary>
    public string? Label { get; set; }
}

/// <summary>
/// KPI tile — number + label + delta + sparkline. Sits in a 4-up grid on
/// dashboards.
/// </summary>
public sealed class KpiTileModel
{
    /// <summary>Caps label above the number ("Plant health").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The big numeric (auto-animated via primitives.js count-up).</summary>
    public string Value { get; set; } = "0";

    /// <summary>Optional unit shown smaller next to the value ("%", "hrs", "$").</summary>
    public string? Unit { get; set; }

    /// <summary>Delta line: "▲ 2.1% vs last week" — formatted by the caller.</summary>
    public string? DeltaText { get; set; }

    /// <summary>up | down | flat — drives the delta pill color.</summary>
    public string DeltaDirection { get; set; } = "flat";

    /// <summary>Sparkline data points (0..1 normalized OR absolute — Spark normalizes).</summary>
    public IEnumerable<double>? SparkPoints { get; set; }

    /// <summary>Sparkline tone (success/warning/danger/info/muted/brand).</summary>
    public string SparkTone { get; set; } = "muted";

    /// <summary>Optional drill-through link.</summary>
    public string? Href { get; set; }
}

/// <summary>
/// Tone-pill — used for status, classification, and counts. Includes optional
/// leading dot and optional mono-numeric chip.
/// </summary>
public sealed class StatusPillModel
{
    /// <summary>Label text.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>neutral | active | success | warning | danger | critical | info | brand | muted.</summary>
    public string Tone { get; set; } = "neutral";

    /// <summary>If true, prepend a colored dot.</summary>
    public bool ShowDot { get; set; } = true;

    /// <summary>Optional mono-numeric chip prepended to the label.</summary>
    public string? MonoNum { get; set; }
}

/// <summary>
/// Sparkline — inline SVG renderer. Auto-normalizes data points and draws a
/// soft area + crisp line. Tone drives the stroke + fill color.
/// </summary>
public sealed class SparklineModel
{
    /// <summary>Data points (positive numbers; trend direction is left-to-right).</summary>
    public IEnumerable<double> Points { get; set; } = System.Array.Empty<double>();

    /// <summary>success | warning | danger | critical | info | muted | brand.</summary>
    public string Tone { get; set; } = "muted";

    /// <summary>SVG viewBox width — default 200.</summary>
    public int ViewBoxWidth { get; set; } = 200;

    /// <summary>SVG viewBox height — default 60.</summary>
    public int ViewBoxHeight { get; set; } = 60;

    /// <summary>If true, render the soft area fill under the line.</summary>
    public bool ShowArea { get; set; } = true;

    /// <summary>If true, render at "thin" weight (1.0 stroke instead of 1.4).</summary>
    public bool Thin { get; set; }

    /// <summary>Optional CSS class to add to the SVG root.</summary>
    public string? ExtraClasses { get; set; }
}
