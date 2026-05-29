#!/bin/bash
# Per-PR config for Theme B11 Wave R2-6 — resource calendars + finite-capacity (CLOSES Wave R2).

BRANCH="feat/b11.r2-6-resource-calendars"
TITLE="B11 R2-6 — resource calendars + finite-capacity (CLOSES Wave R2)"
COMMIT_MSG=".ship/msgs/pr434-commit.txt"
PR_BODY=".ship/msgs/pr434-body.md"

FILES=(
  "Models/Production/ResourceCalendarException.cs"
  "Models/Production/ProductionResource.cs"
  "Data/AppDbContext.cs"
  "Pages/Admin/ResourceCalendarProbe.cshtml"
  "Pages/Admin/ResourceCalendarProbe.cshtml.cs"
  "Migrations/20260529180457_B11_R2_6_ResourceCalendarsFiniteCapacity.cs"
  "Migrations/20260529180457_B11_R2_6_ResourceCalendarsFiniteCapacity.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  ".ship/configs/b11.r2-6-resource-calendars.sh"
  ".ship/msgs/pr434-commit.txt"
  ".ship/msgs/pr434-body.md"
  ".ship/_r26_migrate.sh"
)
