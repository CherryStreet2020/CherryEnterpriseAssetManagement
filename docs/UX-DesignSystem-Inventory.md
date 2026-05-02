# UX Design System Inventory (Phase 0)

> Generated: 2026-01-26 | Status: AWAITING APPROVAL

This document inventories the current state of styling across CherryAI EAM to inform the Design Token migration (ADR-010).

---

## 1. Color Source Files

| File | Purpose | Primary Colors Found |
|------|---------|---------------------|
| `wwwroot/css/modern.css` | Core layout + sidebar | `#2e4a7d`, `#1e3a5f`, `#3b82f6`, `#64748b`, `#e2e8f0` |
| `wwwroot/css/premium-components.css` | Hero, KPI cards, badges | `#1e3a5f`, `#2d4a6f`, `#10b981`, `#ef4444`, `#f59e0b` |
| `wwwroot/css/modules/tabs.css` | Tab navigation | Uses CSS vars with fallbacks |
| `wwwroot/css/modules/headers.css` | Screen headers (ADR-008) | `#1e3a5f`, `#0a1628`, `#ffffff` |
| `wwwroot/css/sidebar-nav.css` | Sidebar navigation | Extends modern.css vars |
| `wwwroot/css/work-order-details.css` | Work order module | `#1e3a5f`, `#2d4a6f`, `#3b82f6` |

### Top 15 Colors by Frequency (modern.css + premium-components.css)

| Hex | Count | Usage |
|-----|-------|-------|
| `#e2e8f0` | 52 | Borders, dividers |
| `#3b82f6` | 51 | Primary accent (blue) |
| `#64748b` | 49 | Secondary text |
| `#1e293b` | 31 | Dark text |
| `#f1f5f9` | 30 | Light surface |
| `#f8fafc` | 29 | Near-white background |
| `#94a3b8` | 18 | Muted text |
| `#2e4a7d` | 14 | Brand primary (dark blue) |
| `#ffffff` | 13 | White |
| `#f59e0b` | 13 | Warning (amber) |
| `#ef4444` | 12 | Danger (red) |
| `#22c55e` | 12 | Success (green) |
| `#1e3a5f` | 11 | Brand dark |
| `#475569` | 8 | Secondary gray |
| `#2563eb` | 8 | Blue link |

---

## 2. CSS Variable Definitions Today

### modern.css :root (lines 8-62)
```css
--primary: #2e4a7d
--primary-hover: #1e3a5f
--primary-light: #e8eef7
--secondary: #64748b
--success: #22c55e
--warning: #f59e0b
--danger: #ef4444
--info: #06b6d4
--bg-primary: #f1f5f9
--bg-secondary: #ffffff
--bg-tertiary: #f8fafc
--text-primary: #0f172a
--text-secondary: #475569
--text-muted: #94a3b8
--text-inverse: #f8fafc
--border: #e2e8f0
--border-focus: #3b82f6
--shadow-sm/md/lg/xl (defined)
--radius-sm/md/lg/xl/2xl (defined)
--transition-fast/normal/bounce (defined)
```

### Gaps / Inconsistencies
- `premium-components.css` uses hard-coded hex instead of vars
- `headers.css` uses hard-coded hex instead of vars
- `tabs.css` uses CSS vars with fallbacks (partial compliance)
- No unified spacing scale tokens
- No font-size scale tokens

---

## 3. Legacy Tab Patterns (Must Migrate)

| File | Pattern | Status |
|------|---------|--------|
| `Pages/Assets/Asset.cshtml` | `.premium-tabs` wrapper | Migrated to _TabNav (PR#3) but wrapper class remains |
| `Pages/Materials/ItemEdit.cshtml` | `.premium-tab-nav`, `.premium-tab-btn`, `.premium-tab-content` | **LEGACY - needs migration** |
| `Pages/Maintenance/Assignments/Index.cshtml` | `.premium-tabs` with inline style | **LEGACY - needs migration** |
| `Pages/Maintenance/Schedules.cshtml` | `.premium-tabs` with inline style | **LEGACY - needs migration** |

**Total legacy tab references:** 102 occurrences across 4 files

---

## 4. Inline Style Audit

| Metric | Count |
|--------|-------|
| `<style>` blocks in Pages/*.cshtml | **1** (`_ScreenHeader.cshtml`) |
| `style="..."` attributes in Pages/*.cshtml | **1,944** |

**Top offenders (inline style= count by file):**
- Asset-related pages (estimated 150+ each)
- ItemEdit.cshtml
- Work order pages

---

## 5. Header/Hero Patterns

| Pattern | File Count | Notes |
|---------|------------|-------|
| `.page-hero` (premium) | **92 pages** | Primary hero component |
| `.screen-header` (ADR-008) | **~5 pages** | New unified pattern |

---

## 6. DataGrid Contract Compliance

| Metric | Count |
|--------|-------|
| Pages with `data-enhanced-grid="true"` | **15** |
| Estimated pages needing DataGrid | ~50+ |

---

## 7. Empty State Usage

| Metric | Count |
|--------|-------|
| Pages using `_EmptyState.cshtml` | **2** |
| Pages with custom empty states | Unknown (needs audit) |

---

## 8. Module CSS Files (wwwroot/css/modules/)

| File | Lines | Purpose |
|------|-------|---------|
| `tabs.css` | 113 | Unified tab system |
| `headers.css` | 181 | Screen headers |
| `workorders.css` | ~400 | Work order module |
| `assets.css` | ~300 | Assets module |
| `dashboard.css` | ~200 | Dashboard |
| `admin.css` | ~250 | Admin section |
| `layout-components.css` | 130 | User dropdown, help modal |

---

## 9. Drift Vectors (Risk Areas)

1. **Hard-coded colors** in premium-components.css and headers.css - not consuming vars
2. **Inline styles** - 1,944 occurrences to remediate over time
3. **Legacy tab classes** - 4 pages still using `.premium-tab-*` instead of `_TabNav`
4. **Inconsistent spacing** - no spacing scale, each component defines its own
5. **Font size drift** - 25+ different font-size values (10px-42px)

---

---

## TOKEN PROPOSAL (tokens.css)

Based on analysis of existing CSS, here is the proposed token structure:

### A. Color Palette

```css
:root {
  /* ===== BRAND (extracted from modern.css/premium-components.css) ===== */
  --color-brand-900: #0a1628;  /* Darkest (header gradient end) */
  --color-brand-800: #1e3a5f;  /* Primary dark (sidebar, heroes) */
  --color-brand-700: #2e4a7d;  /* Primary (buttons, links) - existing --primary */
  --color-brand-600: #3d5a8a;  /* Hover state */
  --color-brand-500: #4a6fa5;  /* Accent */
  
  /* ===== SURFACES ===== */
  --color-surface-0: #f1f5f9;  /* App background - existing --bg-primary */
  --color-surface-1: #ffffff;  /* Cards, panels - existing --bg-secondary */
  --color-surface-2: #f8fafc;  /* Raised elements - existing --bg-tertiary */
  
  /* ===== BORDERS ===== */
  --color-border-1: #e2e8f0;   /* Primary border - existing --border */
  --color-border-2: #cbd5e1;   /* Darker border */
  
  /* ===== TEXT ===== */
  --color-text-1: #0f172a;     /* Primary - existing --text-primary */
  --color-text-2: #475569;     /* Secondary - existing --text-secondary */
  --color-text-3: #94a3b8;     /* Muted - existing --text-muted */
  --color-text-inverse: #f8fafc; /* On dark backgrounds */
  
  /* ===== SEMANTIC ===== */
  --color-success-600: #22c55e;
  --color-success-100: #dcfce7;
  --color-warning-600: #f59e0b;
  --color-warning-100: #fef3c7;
  --color-danger-600: #ef4444;
  --color-danger-100: #fee2e2;
  --color-info-600: #3b82f6;
  --color-info-100: #dbeafe;
  
  /* ===== INTERACTIVE STATES ===== */
  --color-focus: #3b82f6;      /* Focus ring color */
  --color-disabled-bg: #f1f5f9;
  --color-disabled-text: #94a3b8;
  --color-disabled-border: #e2e8f0;
}
```

### B. Typography

```css
:root {
  /* Font stack - already using Inter */
  --font-sans: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
  
  /* Font sizes (extracted from usage patterns) */
  --font-size-10: 0.625rem;  /* 10px - labels, badges */
  --font-size-11: 0.6875rem; /* 11px - small text */
  --font-size-12: 0.75rem;   /* 12px - captions */
  --font-size-13: 0.8125rem; /* 13px - secondary text */
  --font-size-14: 0.875rem;  /* 14px - body (default) */
  --font-size-16: 1rem;      /* 16px - emphasis */
  --font-size-18: 1.125rem;  /* 18px - large text */
  --font-size-20: 1.25rem;   /* 20px - heading 4 */
  --font-size-24: 1.5rem;    /* 24px - heading 3 */
  --font-size-28: 1.75rem;   /* 28px - heading 2 */
  --font-size-32: 2rem;      /* 32px - heading 1 */
  --font-size-42: 2.625rem;  /* 42px - hero title */
  
  /* Font weights */
  --font-weight-400: 400;    /* Normal */
  --font-weight-500: 500;    /* Medium */
  --font-weight-600: 600;    /* Semibold */
  --font-weight-700: 700;    /* Bold */
  --font-weight-800: 800;    /* Extra bold */
  
  /* Line heights */
  --line-height-tight: 1.1;
  --line-height-normal: 1.5;
  --line-height-relaxed: 1.6;
}
```

### C. Spacing Scale

```css
:root {
  --space-2: 0.125rem;   /* 2px */
  --space-4: 0.25rem;    /* 4px */
  --space-6: 0.375rem;   /* 6px */
  --space-8: 0.5rem;     /* 8px */
  --space-10: 0.625rem;  /* 10px */
  --space-12: 0.75rem;   /* 12px */
  --space-14: 0.875rem;  /* 14px */
  --space-16: 1rem;      /* 16px */
  --space-20: 1.25rem;   /* 20px */
  --space-24: 1.5rem;    /* 24px */
  --space-32: 2rem;      /* 32px */
  --space-40: 2.5rem;    /* 40px */
  --space-48: 3rem;      /* 48px */
  --space-64: 4rem;      /* 64px */
}
```

### D. Radius, Shadows, Focus

```css
:root {
  /* Border radius - existing vars renamed */
  --radius-4: 0.25rem;   /* 4px */
  --radius-6: 0.375rem;  /* 6px */
  --radius-8: 0.5rem;    /* 8px */
  --radius-10: 0.625rem; /* 10px */
  --radius-12: 0.75rem;  /* 12px */
  --radius-16: 1rem;     /* 16px */
  --radius-20: 1.25rem;  /* 20px */
  --radius-full: 9999px; /* Pills */
  
  /* Shadows - existing vars */
  --shadow-1: 0 1px 2px 0 rgb(0 0 0 / 0.05);
  --shadow-2: 0 4px 6px -1px rgb(0 0 0 / 0.08), 0 2px 4px -2px rgb(0 0 0 / 0.06);
  --shadow-3: 0 10px 25px -5px rgb(0 0 0 / 0.1), 0 8px 10px -6px rgb(0 0 0 / 0.08);
  --shadow-4: 0 20px 40px -10px rgb(0 0 0 / 0.15);
  --shadow-card: 0 1px 3px rgb(0 0 0 / 0.05), 0 20px 40px -20px rgb(0 0 0 / 0.1);
  
  /* Focus ring */
  --ring-focus: 0 0 0 3px rgba(59, 130, 246, 0.3);
}
```

### E. Motion

```css
:root {
  --ease-standard: cubic-bezier(0.4, 0, 0.2, 1);
  --ease-bounce: cubic-bezier(0.34, 1.56, 0.64, 1);
  --dur-120: 0.12s;
  --dur-180: 0.18s;
  --dur-240: 0.24s;
  --dur-300: 0.3s;
}
```

---

## MAPPING: Existing → New Tokens

| Current Variable | New Token | File Source |
|-----------------|-----------|-------------|
| `--primary` | `--color-brand-700` | modern.css:9 |
| `--primary-hover` | `--color-brand-800` | modern.css:10 |
| `--bg-primary` | `--color-surface-0` | modern.css:28 |
| `--bg-secondary` | `--color-surface-1` | modern.css:29 |
| `--bg-tertiary` | `--color-surface-2` | modern.css:30 |
| `--text-primary` | `--color-text-1` | modern.css:35 |
| `--text-secondary` | `--color-text-2` | modern.css:36 |
| `--text-muted` | `--color-text-3` | modern.css:37 |
| `--border` | `--color-border-1` | modern.css:40 |
| `--border-focus` | `--color-focus` | modern.css:41 |
| `--success` | `--color-success-600` | modern.css:19 |
| `--warning` | `--color-warning-600` | modern.css:21 |
| `--danger` | `--color-danger-600` | modern.css:23 |
| `--info` | `--color-info-600` | modern.css:25 |
| `--radius-sm/md/lg/xl` | `--radius-6/8/12/16` | modern.css:50-54 |
| `--shadow-sm/md/lg/xl` | `--shadow-1/2/3/4` | modern.css:43-48 |
| `--transition-fast` | `--dur-180 var(--ease-standard)` | modern.css:59 |

---

## APPROVAL GATE

**Before implementing Phase 1:**

1. Do you approve the token naming convention?
2. Should existing `--primary`, `--bg-*`, etc. be aliased or replaced entirely?
3. Any colors missing from the palette?
4. Any spacing/sizing adjustments needed?

**Once approved, Phase 1 deliverables:**
- Create `wwwroot/css/tokens.css`
- Create `wwwroot/css/base.css`
- Update `_ModernLayout.cshtml` load order
- Create `docs/adr/ADR-010-Design-Tokens.md`

---

**Status: AWAITING APPROVAL**
