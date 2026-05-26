#!/bin/bash
# Per-PR config for B6 Foundation Sprint PR-FS-5 — ItemSourcingRule (multi-source
# AVL + priority + customer-mandated AS9100 §8.4.1) + IItemSourcingRuleService
# + /Admin/ItemSourcingProbe page.

BRANCH="feat/b6-fs-5-itemsourcingrule-avl"
TITLE="feat(b6-fs-5): ItemSourcingRule (multi-source AVL + priority + customer-mandated AS9100 §8.4.1) + service + admin probe"
COMMIT_MSG=".ship/msgs/fs5-commit.txt"
PR_BODY=".ship/msgs/fs5-body.md"

FILES=(
  "Models/Masters/ItemSourcingRule.cs"
  "Models/Item.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260526214236_AddItemSourcingRuleFsPr5.cs"
  "Migrations/20260526214236_AddItemSourcingRuleFsPr5.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Items/IItemSourcingRuleService.cs"
  "Services/Items/ItemSourcingRuleService.cs"
  "Pages/Admin/ItemSourcingProbe.cshtml"
  "Pages/Admin/ItemSourcingProbe.cshtml.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/Services/Items/ItemSourcingRuleServiceTests.cs"
  ".ship/configs/b6-fs-5.sh"
)
