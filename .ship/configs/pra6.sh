#!/bin/bash
# Sprint 13.5 PRA-6 — Currency / PaymentTerm / TaxAuthority / TaxCode masters
# Master Files Baseline cascade ship #4

BRANCH="sprint-13.5/pra-6-currency-paymentterm-tax-masters"
TITLE="feat(masters): PRA-6 Currency / PaymentTerm / TaxAuthority / TaxCode masters"

COMMIT_MSG="$SCRIPT_DIR/msgs/pra6-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra6-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra6-comment.md"

FILES=(
  "Models/Masters/CurrencyMaster.cs"
  "Models/Masters/PaymentTermMaster.cs"
  "Models/Masters/TaxAuthority.cs"
  "Models/Masters/TaxCodeMaster.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260524190000_AddCurrencyPaymentTermTaxMastersPRA6.cs"
  ".ship/configs/pra6.sh"
)
