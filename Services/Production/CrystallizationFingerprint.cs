// Theme B7 Wave B PR-4 (2026-05-29) — CrystallizationFingerprint.
//
// SHA-256 over the AS-BUILT structure of a Production Order: its frozen BOM
// (ProductionMaterialStructure lines) + its as-run routing (ProductionOperation
// rows). Two PROs that built the SAME thing — same components at the same
// quantities, same operation sequence on the same work centers — produce the
// SAME 64-char hex hash. That equality is the dedupe key: on crystallize, PR-5
// fingerprints the as-built structure and, if it matches an existing
// crystallized standard, surfaces "this matches TI-BRKT-0042 Rev C — link
// instead of create" for HUMAN confirmation (decision #3 — never auto-link
// flight hardware).
//
// Reuses the canonical-pipe-join + invariant-culture + SHA256.HashData pattern
// from PoSnapshotService.ComputeItemFingerprint (which fingerprints a single
// Item). This fingerprints the whole structure. CANONICAL ORDER MATTERS — never
// reorder the appended fields or the segment order; only append. Lines are
// sorted deterministically (BOM by Sequence then frozen part #; ops by
// SequenceNumber then work center) so capture order never perturbs the hash.
//
// What is INTENTIONALLY excluded from the structural fingerprint:
//   - Quantities of the PARENT order (run size) — a 4-off and a 40-off of the
//     same design are the same standard.
//   - Actual times / actual cost — those are run-specific, not structural.
//   - Lot/serial/heat numbers — genealogy, not structure.
//   - ECO effectivity / drawing rev lineage — configuration provenance, not
//     the reusable structural signature (frozen on the record, not hashed).
//   - Audit + tenant columns.
// INCLUDED: component identity (frozen part # + rev) + per-parent quantity +
// UoM + line kind; operation sequence + work center + operation type +
// description. That is the reusable "what is this thing + how is it made"
// signature.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Abs.FixedAssets.Services.Production;

using Abs.FixedAssets.Models.Production;

/// <summary>
/// Computes the structural SHA-256 fingerprint of a Production Order's as-built
/// BOM + as-run routing. Pure + static so PR-4's admin probe and PR-5's
/// crystallization service share one implementation.
/// </summary>
public static class CrystallizationFingerprint
{
    /// <summary>
    /// Build the canonical structural fingerprint. Lower-case hex, 64 chars.
    /// </summary>
    /// <param name="bomLines">As-built BOM — the PRO's ProductionMaterialStructure rows.</param>
    /// <param name="operations">As-run routing — the PRO's ProductionOperation rows.</param>
    public static string Compute(
        IEnumerable<ProductionMaterialStructure> bomLines,
        IEnumerable<ProductionOperation> operations)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder(1024);

        // ── BOM segment ──────────────────────────────────────────
        // Deterministic order: Sequence, then frozen part #, then qty.
        var orderedBom = (bomLines ?? Enumerable.Empty<ProductionMaterialStructure>())
            .OrderBy(l => l.Sequence)
            .ThenBy(l => l.ChildPartNumber, StringComparer.Ordinal)
            .ThenBy(l => l.QuantityPer)
            .ToList();

        sb.Append("BOM("); sb.Append(orderedBom.Count.ToString(inv)); sb.Append(')');
        foreach (var l in orderedBom)
        {
            sb.Append('|');
            sb.Append(l.ChildPartNumber ?? string.Empty); sb.Append('~');
            sb.Append(l.ChildRevision ?? string.Empty); sb.Append('~');
            sb.Append(l.QuantityPer.ToString(inv)); sb.Append('~');
            sb.Append(l.Uom ?? string.Empty); sb.Append('~');
            sb.Append(((int)l.LineKind).ToString(inv));
        }

        // ── Routing segment ──────────────────────────────────────
        // Deterministic order: SequenceNumber, then work center.
        var orderedOps = (operations ?? Enumerable.Empty<ProductionOperation>())
            .OrderBy(o => o.SequenceNumber)
            .ThenBy(o => o.WorkCenterId)
            .ToList();

        sb.Append("||ROUTING("); sb.Append(orderedOps.Count.ToString(inv)); sb.Append(')');
        foreach (var o in orderedOps)
        {
            sb.Append('|');
            sb.Append(o.SequenceNumber.ToString(inv)); sb.Append('~');
            sb.Append(o.WorkCenterId.ToString(inv)); sb.Append('~');
            sb.Append(((int)o.OperationType).ToString(inv)); sb.Append('~');
            sb.Append(o.Description ?? string.Empty);
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        var hex = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) hex.Append(b.ToString("x2"));
        return hex.ToString();
    }
}
