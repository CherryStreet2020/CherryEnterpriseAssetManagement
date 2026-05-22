// ADR-025 D4 — Opt-out attribute for the control-plane Roslyn analyzer (CHERRY025).
//
// This is the main-app-visible copy of the attribute. The analyzer project
// (Analyzers/Abs.FixedAssets.ControlPlaneAnalyzer) defines an identical type
// for its own self-contained build; we duplicate the type here so PageModel /
// Controller / Endpoint / BackgroundService code can apply it without taking
// a runtime dependency on the analyzer DLL.
//
// The analyzer matches BOTH the simple name (ControlPlaneExemptAttribute) and
// the fully-qualified name (Abs.FixedAssets.ControlPlane.ControlPlaneExemptAttribute)
// — so either definition triggers the exemption.
//
// Usage:
//
//   [ControlPlaneExempt("Read-only admin lookup table. No business logic.")]
//   public class CountryListModel : PageModel { ... }
//
// Code review enforces sparing use. Reason string is required.
//
// See:
//   - docs/ADR-025-service-layer-standard.md
//   - docs/ADR-025-roslyn-analyzer-design.md
//   - Analyzers/Abs.FixedAssets.ControlPlaneAnalyzer/ControlPlaneExemptAttribute.cs

using System;

namespace Abs.FixedAssets.ControlPlane;

/// <summary>
/// Marks a class as exempt from the ADR-025 Service Layer Standard CI gate.
/// The CHERRY025 analyzer will NOT emit a diagnostic on a class decorated with
/// this attribute.
/// </summary>
/// <remarks>
/// Reserved for legitimate read-only admin/lookup surfaces (lookup tables,
/// simple admin CRUD with no business logic). Code review enforces sparing use.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ControlPlaneExemptAttribute : Attribute
{
    /// <summary>
    /// Why this class is exempt. Required so code review can audit each usage.
    /// </summary>
    public string Reason { get; }

    public ControlPlaneExemptAttribute(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "ControlPlaneExempt requires a non-empty reason string.",
                nameof(reason));
        }
        Reason = reason;
    }
}
