#!/bin/bash
# Per-PR config — B9 Wave 5 PR-13: quote-vs-actual surface.
# IProjectQuoteVsActualService reads the frozen estimate snapshot (PR-5) vs live
# actuals/EAC (PR-12), bucket-for-bucket. NO SCHEMA CHANGE (read-only service +
# a demo quoted-baseline snapshot seed).
BRANCH="feat/b9.w5-13-quote-vs-actual"
TITLE="feat(b9): quote-vs-actual surface — frozen estimate snapshot vs live EAC by cost bucket (B9 W5 PR-13)"
COMMIT_MSG=".ship/msgs/pr467-commit.txt"
PR_BODY=".ship/msgs/pr467-body.md"
FILES=(
  "Services/Projects/IProjectQuoteVsActualService.cs"
  "Services/Projects/ProjectQuoteVsActualService.cs"
  "Services/Seeding/CooMotionDemoSeeder.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectQuoteVsActualServiceTests.cs"
  ".ship/configs/pr467-b9-w5-pr13-quote-vs-actual.sh"
)
