# reference/repos/ — External Library Source as Agent Context

**Adopted:** 2026-05-24
**Status:** Standing engineering infrastructure
**Source:** Adapted from Pawel Cell's agentic-engineering skill `source-code-context` (Micky Podcast / David Ondrej / Michael Shimeles interviews)

---

## Why this exists

Docs lag. Examples are incomplete. Blog posts drift behind the current API. When the agent has access to the actual library source on disk, it can grep for the real function names, types, examples, and error codes instead of guessing.

This folder holds shallow clones of the **high-touch** libraries we depend on, so the agent can search ground truth before coding.

**Real example that triggered this adoption (2026-05-24):**
The prod Republish failed with `MSB3030: Could not copy ... not found`. Diagnosing it without the .NET SDK source on disk took ~5 minutes of inference about Microsoft.NET.Sdk.Web's implicit Content scan + the `_CopyFilesMarkedCopyLocal` target. With `dotnet/sdk` cloned here, a single `grep -rn "MSB3030" reference/repos/dotnet/sdk` would have surfaced the exact code path in 30 seconds.

## What's in here

Only the **high-touch** deps that we hit deeply enough to need the source:

| Repo | Why we need it |
|---|---|
| `dotnet/sdk` | MSBuild errors (MSB3030 etc.), `dotnet publish` flow, web SDK implicit Content/Compile/None Include rules |
| `dotnet/efcore` | EF Core 9 migration internals, model snapshot generation, query translation issues, FluentAPI semantics |
| `npgsql/Npgsql.EntityFrameworkCore.PostgreSQL` | Npgsql PG provider — partial UNIQUE index quirks, JSONB column mapping, prod-validator quoting issues |

That's it. **Don't add more without a clear reason.** Disk + clone time + agent context noise all grow with each repo.

Things we **don't** clone (and why):
- Razor / Razor Pages source — we know it well enough
- pgvector — small surface, docs are sufficient
- Most NuGet packages — touch them lightly; docs are fine
- Playwright — used at E2E test layer, not core path

## How to (re)build the folder

Run from the repo root:

```bash
cd reference/repos
bash setup.sh
```

The script:
- Shallow-clones each repo at depth 1 (small disk footprint).
- Pins each to a specific tag/branch matching our production dependency version (so source matches reality).
- Updates clones in place if they already exist (`git fetch --depth 1 + reset --hard`).

Re-run any time we bump a package version. The script is idempotent.

## How the agent uses it

When the agent is about to integrate a library, debug a build failure, or trace a runtime exception in one of the high-touch deps:

1. **Grep the reference source first.** Look for the exact error code, function name, or class.
2. **Cite which files were referenced** in the eventual fix / PR body.
3. **Don't install an alternative package** if the source can be found in the reference clone — search first, swap last.

Example prompt the agent should run mentally:

```
We need to integrate <feature using EF Core>.
Reference source: reference/repos/dotnet/efcore
Find the current FluentAPI pattern for <thing>.
Then implement only the minimal service function.
Explain which source files were referenced.
```

## Why this folder is .gitignored

We commit `.gitignore` + `README.md` + `setup.sh` only. The actual cloned source isn't committed because:

1. **Disk:** Each repo is 100-500 MB shallow-cloned. Multiplied by 3 it would balloon the working tree.
2. **CI:** Every CI run would re-checkout megabytes of unused-by-CI code.
3. **Canonical copies live upstream.** If you need the source, run `setup.sh`. Don't fork.
4. **Replit doesn't need it.** The Replit build/deploy never reads `reference/repos/` — it's purely a developer-machine grep target.

Future agents who land on a fresh clone of this repo run `bash reference/repos/setup.sh` once to materialize the references on their disk.

## Cross-references

- `docs/engineering/code-structure-cleanup-skill.md` — sister skill (what to do AFTER a feature works)
- Memory: `feedback_use_reference_repos_for_grep.md` (the trigger note for future Claude sessions)
- Source: https://github.com/pawel-cell/micky-podcast-agentic-engineering/blob/main/skills/source-code-context/SKILL.md
