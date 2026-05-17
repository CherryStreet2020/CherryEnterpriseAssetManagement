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

// =============================================================================
// PR #116d.1b additions
// =============================================================================

/// <summary>
/// Tabular data primitive. Sortable, sticky header, row hover, optional density-aware
/// row heights. For complex tables, render with Razor inside the body slot; this primitive
/// supplies the chrome (header, sort affordance, scroll container, optional toolbar).
/// </summary>
public sealed class DataTableModel
{
    /// <summary>Caps label above the table (e.g. "Work orders").</summary>
    public string? Eyebrow { get; set; }

    /// <summary>Heading text.</summary>
    public string? Title { get; set; }

    /// <summary>Right-aligned count text (e.g. "279 rows").</summary>
    public string? CountText { get; set; }

    /// <summary>Column definitions.</summary>
    public List<DataTableColumn> Columns { get; set; } = new();

    /// <summary>Pre-rendered rows. Each entry is the inner HTML for &lt;tr&gt; (one td per column).</summary>
    public List<string> RowsHtml { get; set; } = new();

    /// <summary>If true, sticky-position the header row inside its scroll container.</summary>
    public bool StickyHeader { get; set; } = true;

    /// <summary>Empty-state message when RowsHtml is empty.</summary>
    public string EmptyMessage { get; set; } = "No rows to display.";

    /// <summary>Optional extra classes on the root.</summary>
    public string? ExtraClasses { get; set; }
}

public sealed class DataTableColumn
{
    /// <summary>Header label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>left | center | right.</summary>
    public string Align { get; set; } = "left";

    /// <summary>CSS width (e.g. "120px", "12rem", "minmax(120px, 1fr)").</summary>
    public string? Width { get; set; }

    /// <summary>If true, this column is sortable (renders sort affordance).</summary>
    public bool Sortable { get; set; }

    /// <summary>If true, render the values in this column with the mono font.</summary>
    public bool Mono { get; set; }
}

/// <summary>
/// EmptyState v2 — hero illustration slot + headline + body + CTA. Legacy
/// _EmptyState.cshtml is preserved unchanged for backwards compatibility.
/// </summary>
public sealed class EmptyStateModel
{
    /// <summary>Headline (Inter, 18px, weight 600).</summary>
    public string Title { get; set; } = "Nothing here yet";

    /// <summary>Body text, 1-2 sentences.</summary>
    public string? Body { get; set; }

    /// <summary>Optional CTA label.</summary>
    public string? CtaLabel { get; set; }

    /// <summary>Optional CTA href.</summary>
    public string? CtaHref { get; set; }

    /// <summary>One of: search, inbox, folder, document, calendar, wrench, sparkle, chart, lock.</summary>
    public string Icon { get; set; } = "inbox";

    /// <summary>Icon tone (drives glow color).</summary>
    public string Tone { get; set; } = "neutral";

    /// <summary>Optional secondary action shown after CTA as ghost button.</summary>
    public string? SecondaryLabel { get; set; }

    /// <summary>Optional secondary href.</summary>
    public string? SecondaryHref { get; set; }
}

/// <summary>
/// SkeletonLoader — shapes for line / card / table / kpi placeholders. Used while
/// data is loading. Animates a soft sheen across the surface.
/// </summary>
public sealed class SkeletonLoaderModel
{
    /// <summary>line | card | table | kpi.</summary>
    public string Shape { get; set; } = "line";

    /// <summary>For "line" shape, how many lines to render.</summary>
    public int LineCount { get; set; } = 3;

    /// <summary>For "table" shape, how many rows to render.</summary>
    public int RowCount { get; set; } = 5;

    /// <summary>For "kpi" shape, how many tiles to render.</summary>
    public int TileCount { get; set; } = 4;

    /// <summary>Optional extra classes on the root.</summary>
    public string? ExtraClasses { get; set; }
}

/// <summary>
/// ContextDrawer — slide-in right-side drawer for "detail without leaving the list."
/// Server-side renders the markup; JS in primitives.js handles the open/close + ESC + backdrop click.
/// </summary>
public sealed class ContextDrawerModel
{
    /// <summary>The drawer's id (must be unique on the page).</summary>
    public string Id { get; set; } = "drawer";

    /// <summary>Drawer title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional subtitle below the title.</summary>
    public string? Subtitle { get; set; }

    /// <summary>Body HTML.</summary>
    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>Footer HTML (typically buttons).</summary>
    public string? FooterHtml { get; set; }

    /// <summary>Width in pixels (default 480).</summary>
    public int Width { get; set; } = 480;
}

/// <summary>
/// ButtonGroup — a row of related buttons with variant + size handling.
/// </summary>
public sealed class ButtonGroupModel
{
    public List<ButtonModel> Buttons { get; set; } = new();

    /// <summary>start | center | end (CSS justify-content).</summary>
    public string Align { get; set; } = "start";
}

public sealed class ButtonModel
{
    public string Label { get; set; } = string.Empty;

    /// <summary>primary | secondary | ghost | danger.</summary>
    public string Variant { get; set; } = "secondary";

    /// <summary>sm | md | lg.</summary>
    public string Size { get; set; } = "md";

    /// <summary>If set, button becomes an &lt;a&gt; with this href.</summary>
    public string? Href { get; set; }

    /// <summary>Optional onclick JS (use sparingly — server actions preferred).</summary>
    public string? OnClick { get; set; }

    /// <summary>If true, disabled state.</summary>
    public bool Disabled { get; set; }

    /// <summary>Optional icon SVG markup placed before the label.</summary>
    public string? IconHtml { get; set; }
}

/// <summary>
/// BrandChip — per-OEM color accent. Maps a manufacturer name to its brand color
/// so chips render consistently across Plant Floor, Asset Register, etc.
/// </summary>
public sealed class BrandChipModel
{
    /// <summary>Manufacturer name (e.g. "Haas", "Mazak", "Lincoln Electric", "KUKA").</summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>Optional model name shown after the manufacturer (e.g. "VF-2SS").</summary>
    public string? Model { get; set; }

    /// <summary>sm | md | lg.</summary>
    public string Size { get; set; } = "md";
}

