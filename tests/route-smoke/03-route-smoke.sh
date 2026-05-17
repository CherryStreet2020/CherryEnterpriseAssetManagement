#!/usr/bin/env bash
# CherryAI EAM — HTTP route smoke test.
#
# Curls every documented nav route, verifies 200/302 status + presence of
# expected HTML markers. Run from inside Replit Shell where localhost:5000
# is the running Web Server.

set -u

PASS=0
FAIL=0
BASE_URL="${BASE_URL:-http://localhost:5000}"

# Probe a route. Args: $1 = label, $2 = path, $3 = expected status, $4 = marker substring (optional)
probe() {
  local label="$1"
  local path="$2"
  local expected_status="$3"
  local marker="${4:-}"

  local tmp
  tmp=$(mktemp)
  local status
  status=$(curl -s -o "$tmp" -w '%{http_code}' "$BASE_URL$path")

  local ok=1
  if [ "$status" != "$expected_status" ]; then
    ok=0
  fi
  if [ -n "$marker" ] && ! grep -q "$marker" "$tmp"; then
    ok=0
  fi

  if [ "$ok" = "1" ]; then
    echo "  PASS  $label  ($path -> $status)"
    PASS=$((PASS+1))
  else
    echo "  FAIL  $label  ($path -> $status, expected $expected_status)"
    if [ -n "$marker" ]; then
      echo "        marker not found: $marker"
    fi
    FAIL=$((FAIL+1))
  fi

  rm -f "$tmp"
}

echo "================================================================"
echo "  Route smoke test  BASE=$BASE_URL"
echo "================================================================"

echo ""
echo "[Auth-public] login"
probe "Login page"          /Account/Login             200 "Login"

echo ""
echo "[Operational routes] dashboard + WO + asset"
# Most of these require auth, so a redirect (302) is expected. We just need
# to confirm the route exists, not 404 or 500.
probe "Dashboard root"      /                          302
probe "Maintenance index"   /Maintenance               302
probe "Assets index"        /Assets                    302
probe "CIP Projects"        /CIP                       302
probe "CIP Costs"           /CIP/Costs                 302
probe "Maintenance/PMTemplates" /Maintenance/PMTemplates 302
probe "Maintenance/Schedules"   /Maintenance/Schedules   302

echo ""
echo "[Production routes] new Phase E areas (controllers/UI not yet wired)"
# Phase E ships schema only; no UI routes yet. These should 404 since
# /ProductionOrders/ProductionBatches/Nests/etc. have no Razor page. A
# 500 here would mean the EF model is misconfigured.
probe "ProductionOrders (no UI yet -> 404)" /ProductionOrders   404
probe "ProductionBatches (no UI yet -> 404)" /ProductionBatches 404
probe "Nests (no UI yet -> 404)" /Nests                         404
probe "StockReceipts (no UI yet -> 404)" /StockReceipts         404
probe "CutListLines (no UI yet -> 404)" /CutListLines           404

echo ""
echo "[Negative]"
probe "Random nonsense 404" /not-a-real-route          404

echo ""
echo "================================================================"
echo "  RESULT  pass=$PASS  fail=$FAIL"
echo "================================================================"

if [ "$FAIL" -gt 0 ]; then
  exit 1
fi
exit 0
