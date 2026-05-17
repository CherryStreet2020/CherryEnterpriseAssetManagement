#!/usr/bin/env bash
# CherryAI EAM — run all schema + integration + route smoke tests.
# Run from inside Replit Shell. Exits 0 only if every layer passes.

set -u

ROOT="$(cd "$(dirname "$0")" && pwd)"
FAILED_LAYERS=()

run_layer() {
  local label="$1"
  local script="$2"

  echo ""
  echo "############################################################"
  echo "# $label"
  echo "############################################################"

  if bash "$script"; then
    return 0
  else
    FAILED_LAYERS+=("$label")
    return 1
  fi
}

run_layer "Layer 1 — DB schema validation" \
  "$ROOT/db-validation/01-schema-validation.sh"

run_layer "Layer 2 — Integration scenarios" \
  "$ROOT/integration-scenarios/02-integration-scenarios.sh"

run_layer "Layer 3 — HTTP route smoke" \
  "$ROOT/route-smoke/03-route-smoke.sh"

echo ""
echo "############################################################"
if [ "${#FAILED_LAYERS[@]}" = "0" ]; then
  echo "# ALL LAYERS PASSED"
  echo "############################################################"
  exit 0
else
  echo "# FAILED LAYERS:"
  for L in "${FAILED_LAYERS[@]}"; do
    echo "#   - $L"
  done
  echo "############################################################"
  exit 1
fi
