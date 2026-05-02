# ADR-004: UI Hygiene Prohibits Inline Style Blocks on Operational Pages

**Status:** Accepted  
**Date:** 2026-01-24  
**Deciders:** Development Team  
**Categories:** UX, Architecture

---

## Context

During UI development, `<style>` blocks were being added directly to Razor Pages for quick styling fixes. This led to:

1. Duplicate CSS across multiple pages
2. Conflicting styles with same class names
3. Difficulty maintaining consistent design
4. Increasing page payload sizes
5. Hard-to-find style definitions

The codebase had inline styles scattered across 20+ operational pages, making systematic UI updates difficult.

## Decision

**Inline `<style>` blocks are prohibited on operational pages.**

Specifically:
1. Operational pages (under `Pages/`) must not contain `<style>` tags
2. All styles must go in `wwwroot/css/` files
3. Layout files (`_ModernLayout`, `_Layout`) are exempt (they define page structure)
4. Specific pages can be allowlisted with documented justification
5. Smoke tests enforce this rule automatically

## Alternatives Considered

### Alternative 1: Allow Any Inline Styles
- **Description:** No restrictions on where styles are defined
- **Pros:** Maximum flexibility
- **Cons:** Unmaintainable, inconsistent, performance issues
- **Why rejected:** Creates technical debt

### Alternative 2: CSS-in-JS or Scoped Styles
- **Description:** Use component-scoped styling system
- **Pros:** True isolation, no conflicts
- **Cons:** Requires major architecture change, not standard for Razor Pages
- **Why rejected:** Too invasive for existing codebase

### Alternative 3: Per-Page CSS Files
- **Description:** Each page gets its own CSS file
- **Pros:** Clear ownership
- **Cons:** Many small files, duplication, build complexity
- **Why rejected:** Overhead not justified

## Consequences

### Positive
- Centralized style management
- Consistent design across application
- Smaller page payloads (shared CSS cached)
- Easier to update design system
- Automated enforcement via smoke tests

### Negative
- Requires refactoring existing pages
- New developers must learn where to add styles
- Some quick fixes take slightly longer

### Neutral
- Allowlist mechanism for edge cases
- Layout files remain exempt

## Implementation Notes

### Smoke Test Enforcement

```csharp
// Test 50: UI → No Inline Style Blocks
private async Task<SmokeTestResult> Test50_NoInlineStyleBlocks()
{
    var violations = new List<string>();
    var allowlist = LoadAllowlist();
    
    foreach (var file in GetOperationalPageFiles())
    {
        if (allowlist.Contains(file)) continue;
        
        var content = await File.ReadAllTextAsync(file);
        if (content.Contains("<style"))
        {
            violations.Add(file);
        }
    }
    
    return violations.Any() 
        ? Fail($"Inline styles found: {string.Join(", ", violations)}")
        : Pass();
}
```

### Allowlist Location

`docs/UI-Conformance-Allowlist.md` documents exempted pages with justification.

### Correct Pattern

Instead of:
```html
<!-- BAD: In Razor Page -->
<style>
    .my-button { background: blue; }
</style>
```

Do:
```css
/* GOOD: In wwwroot/css/components.css */
.my-button { background: blue; }
```

## Related Documents

- [UXStandards.md](../UXStandards.md) - UX standards
- [UI-Conformance-Allowlist.md](../UI-Conformance-Allowlist.md) - Allowlist
- [UI-UX-Conformance-Audit.md](../UI-UX-Conformance-Audit.md) - Audit report

## Revision History

| Date | Author | Description |
|------|--------|-------------|
| 2026-01-24 | Development Team | Initial version |
