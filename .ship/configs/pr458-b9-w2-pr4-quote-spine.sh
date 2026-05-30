#!/bin/bash
# Per-PR config — B9 Wave 2 PR-4 (OPENS Wave 2): quote-to-cash spine, quote layer.
# ProjectRfq / ProjectQuote / ProjectQuoteRevision / ProjectQuoteLine + locked
# submitted-snapshot rule + IProjectQuoteService + Command Center "What did we
# quote?" wired to live data. SCHEMA CHANGE (4 new tables, additive).
BRANCH="feat/b9.w2-4-quote-spine"
TITLE="feat(b9): quote-to-cash spine — RFQ/Quote/Revision/Line + locked snapshot (B9 W2 PR-4)"
COMMIT_MSG=".ship/msgs/pr458-commit.txt"
PR_BODY=".ship/msgs/pr458-body.md"
FILES=(
  "Models/Projects/ProjectQuote.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260530163627_B9WaveW2_ProjectQuoteSpine.cs"
  "Migrations/20260530163627_B9WaveW2_ProjectQuoteSpine.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectQuoteService.cs"
  "Services/Projects/ProjectQuoteService.cs"
  "Services/Projects/ProjectCommandCenterService.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectQuoteServiceTests.cs"
  "tests/Abs.FixedAssets.Tests/ProjectCommandCenterServiceTests.cs"
  ".ship/configs/pr458-b9-w2-pr4-quote-spine.sh"
)
