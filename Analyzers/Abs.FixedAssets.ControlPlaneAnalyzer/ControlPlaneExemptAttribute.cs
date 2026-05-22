// ADR-025 D4 — Opt-out attribute for the control-plane Roslyn analyzer (CHERRY025).
//
// Decorate a class with [ControlPlaneExempt("reason")] to allow direct AppDbContext
// injection without tripping the gate. Reserved for legitimate read-only admin/lookup
// surfaces that have no operational mutations.
//
// The `reason` argument is REQUIRED. Code review polices the use of the attribute
// by grepping for usages and validating each reason. Sparing use only.
//
// Backward compatibility: the leading `// PRAGMA: control-plane-exempt` comment
// pattern from the bash gate is still honored by the analyzer. New code should
// prefer this attribute (typed, refactor-safe).

using System;

namespace Abs.FixedAssets.ControlPlane;

/// <summary>
/// Marks a class as exempt from the ADR-025 Service Layer Standard CI gate.
/// The analyzer will NOT emit CHERRY025 on a class decorated with this attribute.
/// </summary>
/// <remarks>
/// Reserved for legitimate read-only admin/lookup surfaces (lookup tables, simple
/// admin CRUD with no business logic). Code review enforces sparing use.
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
