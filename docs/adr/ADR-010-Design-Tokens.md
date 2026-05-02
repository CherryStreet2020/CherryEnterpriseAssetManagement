# ADR-010: Design Tokens System

**Status:** Accepted  
**Date:** 2026-01-26  
**Context:** Premium UX Design System Phase 1  

## Decision

Introduce a centralized design tokens system via `wwwroot/css/tokens.css` to eliminate hard-coded color values and ensure visual consistency across all UI components.

## Context

The CherryAI EAM application has accumulated:
- **~90+ hard-coded hex values** across CSS files causing visual drift
- **Two parallel tab systems** with inconsistent styling
- **No single source of truth** for brand colors, spacing, typography
- **ADR-004 violations** (inline styles) that are harder to enforce without tokens

A comprehensive CSS inventory identified these drift vectors:
- `#1e3a5f`, `#0a1628` (hero/header gradients)
- `#3b82f6` (accent blue used inconsistently)
- `#64748b`, `#94a3b8` (text colors)
- `#e2e8f0` (border color)

## Token Architecture

### Token Families

| Family | Purpose | Example |
|--------|---------|---------|
| `--color-brand-*` | Brand palette (900-50) | `--color-brand-600: #2e4a7d` |
| `--color-surface-*` | Backgrounds (0-3) | `--color-surface-1: #ffffff` |
| `--color-border-*` | Border colors (1-3) | `--color-border-1: #e2e8f0` |
| `--color-text-*` | Text colors (1-4, inverse) | `--color-text-1: #0f172a` |
| `--color-success-*` | Success semantic | `--color-success-600: #22c55e` |
| `--color-warning-*` | Warning semantic | `--color-warning-600: #f59e0b` |
| `--color-danger-*` | Danger semantic | `--color-danger-600: #ef4444` |
| `--color-info-*` | Info semantic | `--color-info-500: #06b6d4` |
| `--gradient-*` | Pre-built gradients | `--gradient-hero`, `--gradient-brand` |
| `--font-size-*` | Typography scale | `--font-size-14: 0.875rem` |
| `--space-*` | Spacing scale | `--space-9: 1.5rem` (24px) |
| `--radius-*` | Border radius | `--radius-lg: 0.75rem` |
| `--shadow-*` | Box shadows | `--shadow-card` |

### Load Order

```html
<link href="~/css/tokens.css" />  <!-- 1. Tokens (primitives) -->
<link href="~/css/base.css" />    <!-- 2. Base (focus, selection) -->
<link href="~/css/modern.css" /> <!-- 3. Legacy aliases + components -->
<link href="~/css/premium-components.css" />
<!-- ... module CSS files ... -->
```

### Backward Compatibility

Legacy variables in `modern.css` are aliased to tokens:

```css
:root {
  --primary: var(--color-brand-600);
  --border: var(--color-border-1);
  --text-primary: var(--color-text-1);
  /* ... etc ... */
}
```

Existing code using `var(--primary)` continues to work without changes.

## Rules

### MUST

1. **All new CSS** must use token variables, not hex values
2. **tokens.css** is the only file where hex color definitions are allowed
3. **New brand colors** require ADR amendment (brand lock policy)

### MUST NOT

1. Do NOT add hex colors to any CSS file except `tokens.css`
2. Do NOT delete legacy variable aliases in Phase 1
3. Do NOT introduce new color hues without explicit approval

### SHOULD

1. Prefer semantic tokens over brand tokens where applicable
2. Use spacing tokens (`--space-*`) for consistent rhythm
3. Use gradient tokens (`--gradient-*`) for hero/header backgrounds

## Consequences

### Positive

- **Single source of truth** for all design primitives
- **Easier theming** (dark mode, white-label) in future
- **Reduced visual drift** across pages
- **Better enforcement** of ADR-004 (easier to spot violations)

### Negative

- **Initial migration overhead** (Phase 1 is minimal, Phase 2+ will expand)
- **Two naming conventions** temporarily (legacy aliases + new tokens)

### Neutral

- **No visual changes** in Phase 1 (exact same hex values)
- **Module CSS** will be migrated incrementally in future phases

## Migration Path

| Phase | Scope | Files |
|-------|-------|-------|
| Phase 1 | Core tokens + aliases | tokens.css, base.css, modern.css, headers.css, premium-components.css |
| Phase 2 | Module CSS migration | modules/*.css |
| Phase 3 | Inline style elimination | Pages/*.cshtml (per ADR-004) |
| Phase 4 | Full audit + cleanup | Remove legacy aliases |

## Evidence

### Files Created
- `wwwroot/css/tokens.css` - Token definitions
- `wwwroot/css/base.css` - Base focus/selection styles

### Files Modified
- `wwwroot/css/modern.css` - Legacy variable aliases
- `wwwroot/css/premium-components.css` - Hero/status tokens
- `wwwroot/css/modules/headers.css` - Screen header tokens
- `Pages/Shared/_ModernLayout.cshtml` - Load order updated

### Hard-coded Colors Replaced
- `headers.css`: 4 hex values → tokens
- `premium-components.css`: 9 hex values → tokens

## Related ADRs

- **ADR-004**: UI Hygiene - No Inline Styles
- **ADR-007**: Unified Tab System
- **ADR-008**: Unified Screen Header System

## References

- [CSS Custom Properties (MDN)](https://developer.mozilla.org/en-US/docs/Web/CSS/Using_CSS_custom_properties)
- [Design Tokens W3C Community Group](https://www.w3.org/community/design-tokens/)
