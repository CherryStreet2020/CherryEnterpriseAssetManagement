# `.ship/` — single-command ship harness

The full Mac → GitHub → Replit ship workflow as one shell script. Encodes every pitfall we hit shipping Sprint 11 PRs #1 + #2 so subsequent PRs ship without ceremony.

## Quick start

Write a config file in `.ship/configs/<name>.sh`:

```bash
#!/bin/bash
BRANCH="feat/sprint-11-pr3-receiving-service"
TITLE="feat(sprint-11): IReceivingControlCenterService + state machine"
COMMIT_MSG="../.ship/msgs/pr3-commit.txt"
PR_BODY="../.ship/msgs/pr3-body.md"
SHIP_COMMENT="../.ship/msgs/pr3-comment.md"

FILES=(
  "Services/Receiving/IReceivingControlCenterService.cs"
  "Services/Receiving/ReceivingControlCenterService.cs"
  "Models/Receiving/...etc"
)
```

Write the commit message, PR body, and (optional) ship comment files. Then:

```bash
bash .ship/run.sh full .ship/configs/pr3.sh
```

That runs preflight → local dotnet build → branch + commit + push → open PR → poll CI until green → squash-merge + delete branch → sync local main → post ship comment. All logs land in `.ship/runs/<branch>/`.

## What it solves

| Pitfall (PR #1 + #2) | Fix |
|---|---|
| osxkeychain credential helper hangs when git is invoked from `osascript` | `disable_keychain()` sets `GIT_CONFIG_COUNT=1` + empty `credential.helper`, and the gh token is embedded directly in the remote URL |
| AppleScript `do shell script` mangles backslash continuations | Commit messages via `-F <file>`; PR bodies via `--body-file`; never inline-escaped |
| `dotnet build … \| tail -N` deadlocks output until stdin closes | Direct file redirect to `runs/<branch>/build.log`; never piped through `tail` |
| MSBuild node-reuse hangs on cold builds with stale processes | Always `--nodereuse:false --disable-build-servers` |
| Duplicate PRs from retries that succeeded silently | `do_pr_create()` checks for an existing PR on the branch and reuses it |
| `gh pr merge --auto` rejected because repo disables it | Always `--squash --delete-branch`, never `--auto` |
| Hard-reset wipes work | `reset_local_main()` only runs after fresh fetch; never on branches that still hold staged work |
| Repeated commits / pushes / PRs on partial reruns | Every step is idempotent — pre-checks for existing branch / pending commit / open PR |

## Subcommands

- `full <config>` — end-to-end ship. The main entry point.
- `preflight` — sanity-check gh auth, sync local main from origin.
- `build` — local dotnet build only (no git ops).
- `branch-commit-push <config>` — just the git portion.
- `open-pr <config>` — open a PR for an already-pushed branch.
- `wait-ci <pr-num> [max-seconds]` — poll until CI passes/fails (default 600s).
- `merge <pr-num>` — squash-merge + delete branch + sync local main.
- `comment <pr-num> <body-file>` — post a ship comment.

## Recovery

If `full` dies mid-flight, re-running picks up where it left off. The harness inspects the actual state (does the branch exist? is the PR open? is it already merged?) before retrying each step.

## Logs

Every command writes to `.ship/runs/<branch>/<step>.log`. Look there first when something errors. The runs directory is gitignored.

## Required environment

- macOS with bash
- `gh` CLI at `/Users/deandunagan/bin/gh` (override with `GH=/path/to/gh`)
- `dotnet` SDK 9 at `$HOME/.dotnet` (override with `SHIP_CSPROJ` to target a different project)
- gh authenticated against `github.com` with `repo` scope
- Repo cloned at `.ship/..` (parent of this directory)

## Customization

The defaults are correct for the CherryAI EAM repo. For other projects, override at invocation:

```bash
SHIP_REPO_SLUG="MyOrg/MyRepo" SHIP_CSPROJ="MyApp.csproj" bash .ship/run.sh full .ship/configs/pr1.sh
```
