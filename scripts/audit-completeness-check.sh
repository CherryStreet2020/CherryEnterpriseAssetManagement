#!/usr/bin/env bash
# ADR-025 D4 — Audit-completeness ratchet (Sprint 12.9 PR #6).
#
# Enforces strict equality between the *expected* control-plane allowlist
# size (Analyzers/.allowlist-ratchet) and the *actual* number of data
# lines in Analyzers/ControlPlaneAllowlist.txt.
#
# Why a ratchet (not a one-way gate):
#   The Sprint 12.9 PR cadence is "each PR removes its target file from
#   the allowlist; the line count IS the audit-completeness metric." A
#   ratchet locked at the previous count means:
#     1) A PR that *adds* an entry fails CI (you must refactor instead,
#        or mark the class with [ControlPlaneExempt("reason")]).
#     2) A PR that *removes* an entry without also lowering the ratchet
#        also fails CI — this prevents drift where the count silently
#        regresses on a subsequent PR.
#     3) A PR that removes an entry AND lowers the ratchet by the same
#        amount passes.
#
# The strict-equality invariant turns the ratchet file into a
# diff-reviewable "audit-completeness scoreboard" — every drop is visible
# in PR diffs and shows up in `git log .allowlist-ratchet`.
#
# Run locally:
#   bash scripts/audit-completeness-check.sh
#
# Output:
#   - On pass: prints "OK — N entries, ratchet N (matched)"
#   - On fail: prints a remediation hint AND a short diff sketch
#   - In CI: writes a Step Summary block via $GITHUB_STEP_SUMMARY if set.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ALLOWLIST="$REPO_ROOT/Analyzers/ControlPlaneAllowlist.txt"
RATCHET="$REPO_ROOT/Analyzers/.allowlist-ratchet"

[ -f "$ALLOWLIST" ] || { echo "::error::missing $ALLOWLIST"; exit 2; }
[ -f "$RATCHET" ]   || { echo "::error::missing $RATCHET"; exit 2; }

# Count non-blank, non-comment lines. Trim trailing whitespace.
COUNT=$(grep -cv '^\s*#\|^\s*$' "$ALLOWLIST" || true)
EXPECTED=$(head -1 "$RATCHET" | tr -d '[:space:]')

# Sanity-check ratchet format.
if ! [[ "$EXPECTED" =~ ^[0-9]+$ ]]; then
  echo "::error file=$RATCHET::ratchet file must contain a single non-negative integer; got '$EXPECTED'"
  exit 2
fi

# Build the summary block (GitHub Actions Step Summary if present).
SUMMARY="### Control-Plane Audit-Completeness Metric

| Metric | Value |
|---|---:|
| Allowlist entries (current) | **$COUNT** |
| Ratchet target | **$EXPECTED** |
| Sprint 12.9 baseline (PR #272) | 118 |
| Net reduction since baseline | $((118 - COUNT)) |
"

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  printf '%s\n' "$SUMMARY" >> "$GITHUB_STEP_SUMMARY"
fi

# Final verdict.
if [ "$COUNT" -eq "$EXPECTED" ]; then
  echo "OK — $COUNT entries, ratchet $EXPECTED (matched)"
  exit 0
fi

if [ "$COUNT" -gt "$EXPECTED" ]; then
  cat >&2 <<EOF
::error file=Analyzers/ControlPlaneAllowlist.txt::Allowlist GREW: $EXPECTED → $COUNT (+$((COUNT - EXPECTED))).

A new entry was added to Analyzers/ControlPlaneAllowlist.txt. New entries
are not permitted — the ratchet only goes down.

Remediation (pick one):
  1) Refactor the new code to use a typed domain service (the preferred
     fix; see docs/ADR-025-service-layer-standard.md).
  2) If this is a legitimate read-only admin/lookup surface, mark the
     class with [ControlPlaneExempt("reason")] instead of adding it to
     the allowlist.

Both options keep the audit-completeness scoreboard moving in one
direction: down.
EOF
  exit 1
fi

# COUNT < EXPECTED — count went DOWN but the ratchet wasn't lowered.
cat >&2 <<EOF
::error file=Analyzers/.allowlist-ratchet::Ratchet drift: count dropped $EXPECTED → $COUNT but the ratchet still reads $EXPECTED.

Nice work reducing the allowlist by $((EXPECTED - COUNT)) — but please also
lower the ratchet to match, so future PRs are pinned to the new floor.

Fix:
  echo $COUNT > Analyzers/.allowlist-ratchet

Then commit the change as part of this PR.
EOF
exit 1
