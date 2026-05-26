#!/bin/bash
# Per-PR config for B6 Foundation Sprint PR-FS-6 — CustomerItemXref (SAP CMIR
# equivalent) + ICustomerItemXrefService + /Admin/CustomerItemXrefProbe page.

BRANCH="feat/b6-fs-6-customeritemxref"
TITLE="feat(b6-fs-6): CustomerItemXref (SAP CMIR — customer-PN ↔ Item translation) + service + admin probe"
COMMIT_MSG=".ship/msgs/fs6-commit.txt"
PR_BODY=".ship/msgs/fs6-body.md"

FILES=(
  "Models/Masters/CustomerItemXref.cs"
  "Models/Item.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260526220538_AddCustomerItemXrefFsPr6.cs"
  "Migrations/20260526220538_AddCustomerItemXrefFsPr6.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Items/ICustomerItemXrefService.cs"
  "Services/Items/CustomerItemXrefService.cs"
  "Pages/Admin/CustomerItemXrefProbe.cshtml"
  "Pages/Admin/CustomerItemXrefProbe.cshtml.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/Services/Items/CustomerItemXrefServiceTests.cs"
  ".ship/configs/b6-fs-6.sh"
)
