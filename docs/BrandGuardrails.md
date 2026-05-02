# CherryAI EAM Brand Guardrails

**Version:** 1.0  
**Last Updated:** 2026-01-22  
**Status:** ACTIVE - All new pages must follow these rules

---

## Overview

This document defines the visual and UX standards for CherryAI EAM. All pages must conform to these patterns for brand consistency. Deviations require explicit approval and documentation.

---

## 1. Layout Structure

### Page Container
```css
.main-content {
  padding: 1.5rem 2rem;
  background: var(--bg-primary);    /* #f1f5f9 */
}
```

### Page Sections
- **Hero Header**: Required at top of every page
- **Content Sections**: Use `.section-card` wrapper
- **Action Bars**: Fixed at bottom of forms when needed

### Hero Header Pattern
Every page MUST have a hero header with:
- Page title (H1)
- Optional subtitle/description
- Primary action button (right-aligned)
- Breadcrumbs (above title)

```html
<div class="hero-header">
    <div class="hero-breadcrumbs">...</div>
    <div class="hero-content">
        <div class="hero-text">
            <h1 class="hero-title">Page Title</h1>
            <p class="hero-subtitle">Brief description</p>
        </div>
        <div class="hero-actions">
            <a class="btn btn-primary">Primary Action</a>
        </div>
    </div>
</div>
```

---

## 2. Color Palette

### Primary Colors
| Token | Value | Usage |
|-------|-------|-------|
| `--primary` | `#2e4a7d` | Primary buttons, links, accents |
| `--primary-hover` | `#1e3a5f` | Button hover states |
| `--primary-light` | `#e8eef7` | Light backgrounds, selected states |

### Semantic Colors
| Token | Value | Usage |
|-------|-------|-------|
| `--success` | `#22c55e` | Success states, positive actions |
| `--warning` | `#f59e0b` | Warning states, pending items |
| `--danger` | `#ef4444` | Error states, destructive actions |
| `--info` | `#06b6d4` | Informational content |

### Background Colors
| Token | Value | Usage |
|-------|-------|-------|
| `--bg-primary` | `#f1f5f9` | Page background |
| `--bg-secondary` | `#ffffff` | Cards, modals |
| `--bg-tertiary` | `#f8fafc` | Alternating rows, subtle sections |

### Text Colors
| Token | Value | Usage |
|-------|-------|-------|
| `--text-primary` | `#0f172a` | Main content text |
| `--text-secondary` | `#475569` | Secondary text, descriptions |
| `--text-muted` | `#94a3b8` | Placeholder, disabled text |

---

## 3. Typography

### Font Family
- Primary: `Inter` (Google Fonts)
- Fallback: `-apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif`

### Font Sizes
| Element | Size | Weight |
|---------|------|--------|
| H1 (Page Title) | 1.75rem | 700 |
| H2 (Section Title) | 1.25rem | 600 |
| H3 (Subsection) | 1.125rem | 600 |
| Body | 14px | 400 |
| Small/Caption | 0.875rem | 400 |
| Label | 0.875rem | 500 |

---

## 4. Buttons

### Primary Button
```css
.btn-primary {
  background: var(--primary-gradient);  /* linear-gradient(135deg, #2e4a7d 0%, #1e3a5f 100%) */
  color: white;
  padding: 0.625rem 1.25rem;
  border-radius: var(--radius-lg);      /* 0.75rem */
  font-weight: 500;
  box-shadow: var(--shadow-md);
}
```

### Secondary Button
```css
.btn-secondary {
  background: var(--bg-secondary);
  color: var(--text-primary);
  border: 1px solid var(--border);
  padding: 0.625rem 1.25rem;
  border-radius: var(--radius-lg);
}
```

### Danger Button
```css
.btn-danger {
  background: var(--danger);
  color: white;
  padding: 0.625rem 1.25rem;
  border-radius: var(--radius-lg);
}
```

### Button Sizes
| Size | Padding | Font Size |
|------|---------|-----------|
| Small | 0.375rem 0.75rem | 0.875rem |
| Default | 0.625rem 1.25rem | 0.875rem |
| Large | 0.75rem 1.5rem | 1rem |

---

## 5. Cards

### Standard Section Card
```css
.section-card {
  background: var(--bg-secondary);
  border-radius: var(--radius-xl);      /* 1rem */
  padding: 1.5rem;
  box-shadow: var(--shadow-card);
  border: 1px solid var(--border);
}
```

### Card Header Pattern
```html
<div class="section-card">
    <div class="section-card-header">
        <h2 class="section-card-title">Section Title</h2>
        <a class="btn btn-sm btn-secondary">Action</a>
    </div>
    <div class="section-card-content">
        <!-- Content -->
    </div>
</div>
```

### DO NOT USE
- Translucent/ghost panels with `backdrop-filter: blur()`
- Modal-style chrome on full pages
- Cards without borders on white backgrounds

---

## 6. Badges

### Status Badges
```css
.badge {
  display: inline-flex;
  align-items: center;
  padding: 0.25rem 0.625rem;
  border-radius: 9999px;            /* Fully rounded */
  font-size: 0.75rem;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.025em;
}
```

### Badge Variants
| Variant | Background | Text Color |
|---------|------------|------------|
| Success | `var(--success-light)` | `#15803d` |
| Warning | `var(--warning-light)` | `#b45309` |
| Danger | `var(--danger-light)` | `#dc2626` |
| Info | `var(--info-light)` | `#0e7490` |
| Neutral | `#f1f5f9` | `var(--text-secondary)` |

---

## 7. Forms

### Form Group Pattern
```html
<div class="form-group">
    <label class="form-label">Field Label</label>
    <input type="text" class="form-control" />
    <span class="form-hint">Optional hint text</span>
</div>
```

### Input Styling
```css
.form-control {
  width: 100%;
  padding: 0.625rem 0.875rem;
  border: 1px solid var(--border);
  border-radius: var(--radius-md);  /* 0.5rem */
  background: var(--bg-secondary);
  font-size: 0.875rem;
  transition: border-color var(--transition-fast);
}

.form-control:focus {
  outline: none;
  border-color: var(--border-focus);
  box-shadow: 0 0 0 3px rgb(59 130 246 / 0.15);
}
```

---

## 8. Empty States

### Standard Empty State Pattern
```html
<div class="empty-state">
    <div class="empty-state-icon">
        <i class="fas fa-inbox"></i>
    </div>
    <h3 class="empty-state-title">No items found</h3>
    <p class="empty-state-description">
        Add your first item to get started.
    </p>
    <a class="btn btn-primary">
        <i class="fas fa-plus"></i> Add Item
    </a>
</div>
```

### Empty State Styling
```css
.empty-state {
  text-align: center;
  padding: 3rem 1.5rem;
  color: var(--text-secondary);
}

.empty-state-icon {
  font-size: 3rem;
  color: var(--text-muted);
  margin-bottom: 1rem;
}

.empty-state-title {
  font-size: 1.125rem;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 0.5rem;
}
```

---

## 9. Tables / Data Grids

### Table Container
```html
<div class="table-container">
    <table class="data-table">
        <thead>...</thead>
        <tbody>...</tbody>
    </table>
</div>
```

### Row Hover
```css
.data-table tbody tr:hover {
  background: var(--bg-tertiary);
}
```

### Clickable Rows
```css
.data-table tbody tr.clickable {
  cursor: pointer;
}
```

---

## 10. Shadows

| Token | Value | Usage |
|-------|-------|-------|
| `--shadow-sm` | `0 1px 2px 0 rgb(0 0 0 / 0.05)` | Subtle lift |
| `--shadow-md` | `0 4px 6px -1px rgb(0 0 0 / 0.08)` | Buttons, small cards |
| `--shadow-lg` | `0 10px 25px -5px rgb(0 0 0 / 0.1)` | Dropdowns, popovers |
| `--shadow-card` | `0 1px 3px rgb(0 0 0 / 0.05), 0 20px 40px -20px rgb(0 0 0 / 0.1)` | Main cards |

---

## 11. Border Radius

| Token | Value | Usage |
|-------|-------|-------|
| `--radius-sm` | `0.375rem` | Inputs, small elements |
| `--radius-md` | `0.5rem` | Default radius |
| `--radius-lg` | `0.75rem` | Buttons |
| `--radius-xl` | `1rem` | Cards |
| `--radius-2xl` | `1.25rem` | Large cards, modals |

---

## 12. Transitions

| Token | Value | Usage |
|-------|-------|-------|
| `--transition-fast` | `0.15s cubic-bezier(0.4, 0, 0.2, 1)` | Button hover |
| `--transition-normal` | `0.25s cubic-bezier(0.4, 0, 0.2, 1)` | Expand/collapse |
| `--transition-bounce` | `0.3s cubic-bezier(0.34, 1.56, 0.64, 1)` | Pop effects |

---

## 13. Anti-Patterns (DO NOT USE)

### Forbidden Patterns
1. **Translucent ghost panels** - No `backdrop-filter: blur()` on cards
2. **Modal chrome on full pages** - Full pages are not modals
3. **Custom fonts** - Only use Inter
4. **Magic numbers** - Use CSS variables
5. **Inline styles** - Use classes
6. **!important** - Avoid except for utility overrides
7. **Nested cards** - Maximum 1 level of card nesting

### Legacy Patterns to Avoid
- Old button styles (`.btn-old`, `.button-primary`)
- Inline color definitions
- Fixed pixel widths (use responsive patterns)
- Z-index wars (use defined layers)

---

## 14. Sidebar Navigation

### Active State Detection
Sidebar items use path-based detection:
```razor
@(currentPage.StartsWith("/Materials/Item") ? "active" : "")
```

### Section Patterns
- Feature areas: `/Assets/*`, `/Maintenance/*`, `/Materials/*`, `/Purchasing/*`, `/Finance/*`, `/Reports/*`
- Configuration: `/Admin/*` (Setup and Administration only)

---

## 15. Legacy Warning Banner

For deprecated routes that still render, add a legacy banner:

```html
<div class="legacy-banner">
    <i class="fas fa-exclamation-triangle"></i>
    <span>This page has been moved. Please use 
        <a href="/Materials/Items">Item Master</a> instead.
    </span>
</div>
```

```css
.legacy-banner {
  background: var(--warning-light);
  border: 1px solid var(--warning);
  border-radius: var(--radius-md);
  padding: 0.75rem 1rem;
  margin-bottom: 1rem;
  display: flex;
  align-items: center;
  gap: 0.75rem;
  color: #b45309;
  font-size: 0.875rem;
}
```

---

*Document maintained by CherryAI EAM Development Team*
