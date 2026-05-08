# Architecture Decision Records

ADRs document significant architectural decisions: the context, the chosen approach, and the alternatives considered. Each PR that implements a decision links back to its ADR.

| # | Title | Status | Closes |
|---|---|---|---|
| [001](ADR-001-receiving-accrual-and-inventory-movement.md) | Receiving accrual + inventory movement (GR/IR) | Proposed | S1-1 |
| [002](ADR-002-ap-posting-and-invoice-matching.md) | AP posting + invoice matching | Proposed | S1-5, S2-10 |
| [003](ADR-003-central-gl-account-resolver.md) | Central GL account resolver | Proposed | S2-7 |

## When to write an ADR

- Touching the financial ledger (new posting flow, new entity in a JE chain)
- New cross-cutting concern (auth, period locking, GL resolution)
- Schema migrations that change a model's identity / primary key / unique constraint shape
- Anything where the "why" is non-obvious from the code or where a reasonable engineer might reach a different conclusion 6 months from now

## When NOT to write an ADR

- Bug fixes
- Local refactors that don't change cross-file contracts
- UI changes
- Test-only changes

## Format

Lead with **Status** and **Context**. List **Decisions** as numbered, individually addressable bullets (`D-NNN-N`) so PR descriptions can cite them. Spell out **Open questions** with recommended resolutions. Include a **Tests** section enumerating the load-bearing test names.
