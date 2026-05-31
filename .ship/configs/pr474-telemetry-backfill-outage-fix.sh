#!/bin/bash
# Per-PR config — fix the 2026-05-31 prod restart-loop outage caused by the
# telemetry historical backfill seeder. NO schema (code-only). Two parts:
#  (1) Program.cs — run the multi-million-row backfill fire-and-forget on a
#      background DI scope instead of awaiting it on the startup readiness path.
#  (2) Seeder — write Id explicitly in the COPY (the live SensorEvents.Id lost
#      its BIGSERIAL default in the manual TimescaleDB removal) + guarded setval.
BRANCH="fix/telemetry-backfill-startup-outage"
TITLE="fix(telemetry): stop startup restart-loop — background backfill + explicit SensorEvents.Id in COPY"
COMMIT_MSG=".ship/msgs/pr474-commit.txt"
PR_BODY=".ship/msgs/pr474-body.md"
FILES=(
  "Services/Seeding/TelemetryHistoricalBackfillSeeder.cs"
  "Program.cs"
  ".ship/configs/pr474-telemetry-backfill-outage-fix.sh"
)
