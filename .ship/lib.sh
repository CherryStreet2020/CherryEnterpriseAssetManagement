#!/bin/bash
# .ship/lib.sh — helpers for the ship harness. Sourced by run.sh.
#
# These helpers encode every pitfall we hit on Sprint 11 PRs #1 + #2:
#   - osxkeychain credential helper hangs when invoked from osascript
#     → we disable it via GIT_CONFIG_COUNT env vars and put the gh token
#       directly into the remote URL.
#   - AppleScript `do shell script` mangles backslash continuations
#     → we pass commit messages and PR bodies via -F / --body-file.
#   - `dotnet build | tail -N` deadlocks output until stdin closes
#     → we always redirect dotnet output to a log file directly, never
#       through a pipe.
#   - MSBuild --nodereuse=true with stale processes hangs cold builds
#     → we always pass --nodereuse:false --disable-build-servers.
#
# All log output for a run goes under .ship/runs/<branch>/<step>.log so a
# resumed run can pick up where the last one died.
#
# Conventions:
#   - All functions return 0 on success, non-zero on failure.
#   - All functions log to stdout AND to $SHIP_LOG_DIR/<step>.log.
#   - log_step "N" "label"                      prints + emits step header
#   - log_info "message"                        prints + emits info line
#   - log_err "message"                         prints + emits red error
#   - die "message"                             log_err then exit 1
#
# Required env (set by run.sh before sourcing):
#   SHIP_REPO_DIR        absolute path to the git repo
#   SHIP_LOG_DIR         absolute path to .ship/runs/<branch>/
#   SHIP_TOKEN           gh token for github.com (from `gh auth token`)
#   SHIP_REPO_SLUG       e.g. CherryStreet2020/CherryEnterpriseAssetManagement
#   SHIP_REPO_URL        token-bearing https URL for git ops

set -uo pipefail

# ---------- pretty printing ------------------------------------------

# Colors only if stdout is a tty.
if [ -t 1 ]; then
  C_RESET="\033[0m"; C_DIM="\033[2m"; C_RED="\033[31m"
  C_GREEN="\033[32m"; C_YELLOW="\033[33m"; C_CYAN="\033[36m"; C_BOLD="\033[1m"
else
  C_RESET=""; C_DIM=""; C_RED=""; C_GREEN=""; C_YELLOW=""; C_CYAN=""; C_BOLD=""
fi

log_step() {
  local n="$1" label="$2"
  printf '\n%b━━ %s · %s %b\n' "$C_BOLD$C_CYAN" "$n" "$label" "$C_RESET"
}

log_info() { printf '%b%s%b\n' "$C_DIM" "$*" "$C_RESET"; }
log_ok()   { printf '%b%s%b\n' "$C_GREEN" "$*" "$C_RESET"; }
log_warn() { printf '%b%s%b\n' "$C_YELLOW" "$*" "$C_RESET"; }
log_err()  { printf '%b%s%b\n' "$C_RED" "$*" "$C_RESET" >&2; }

die() { log_err "$@"; exit 1; }

# ---------- env --------------------------------------------------------

# Disable osxkeychain credential helper for git ops in this process.
# Without this, git fetch/push from osascript hangs forever.
disable_keychain() {
  export GIT_CONFIG_COUNT=1
  export GIT_CONFIG_KEY_0="credential.helper"
  export GIT_CONFIG_VALUE_0=""
  export GIT_TERMINAL_PROMPT=0
}

# Returns 0 if gh CLI exists at the expected path AND is authenticated.
check_gh() {
  [ -x "$GH" ] || die "gh CLI not found at $GH"
  "$GH" auth status >/dev/null 2>&1 || die "gh CLI is not authenticated. Run: $GH auth login"
  return 0
}

# ---------- git helpers -----------------------------------------------

# Fetch origin/main using token in URL, bypassing credential helper.
sync_origin_main() {
  log_info "fetching origin/main with gh token in URL"
  cd "$SHIP_REPO_DIR" || die "cannot cd to $SHIP_REPO_DIR"
  git fetch "$SHIP_REPO_URL" main:refs/remotes/origin/main \
    > "$SHIP_LOG_DIR/fetch.log" 2>&1 \
    || die "git fetch failed — see $SHIP_LOG_DIR/fetch.log"
  log_ok "origin/main HEAD: $(git log origin/main --oneline -1)"
}

# Hard reset local main to origin/main. Destructive — fail-fast guarded.
reset_local_main() {
  cd "$SHIP_REPO_DIR" || die "cannot cd"

  # Fail-fast guard against losing uncommitted tracked work.
  # Pre-guard: this used to call reset --hard blindly, which silently wiped
  # uncommitted edits on main. Cost a real round of work before the guard
  # landed. Untracked files are fine (the reset doesn't touch them).
  local dirty
  dirty=$(git status --porcelain --untracked-files=no 2>/dev/null)
  if [ -n "$dirty" ]; then
    log_err "Local main has uncommitted tracked changes — refusing to reset."
    log_err "These files would be wiped:"
    echo "$dirty" | sed 's/^/    /' >&2
    log_err ""
    log_err "Resolution:"
    log_err "  1. Stash your work:    git stash"
    log_err "  2. Commit your work:   git add ... && git commit -m '...'"
    log_err "  3. Discard your work:  git checkout -- . && git clean -fd"
    log_err "Then re-run the harness."
    exit 1
  fi

  git checkout main >/dev/null 2>&1 || die "cannot checkout main"
  git reset --hard origin/main > "$SHIP_LOG_DIR/reset.log" 2>&1 \
    || die "git reset failed — see $SHIP_LOG_DIR/reset.log"
  log_ok "local main reset to $(git log --oneline -1)"
}

# Create a fresh branch from origin/main. Idempotent — if the branch
# already exists locally with the right base, reuse it.
make_branch() {
  local branch="$1"
  cd "$SHIP_REPO_DIR" || die "cannot cd"
  if git show-ref --verify --quiet "refs/heads/$branch"; then
    log_info "branch $branch already exists locally — switching to it"
    git checkout "$branch" >/dev/null 2>&1 || die "cannot checkout $branch"
  else
    git checkout -b "$branch" origin/main > "$SHIP_LOG_DIR/branch.log" 2>&1 \
      || die "cannot create branch $branch — see $SHIP_LOG_DIR/branch.log"
    log_ok "branched off origin/main as $branch"
  fi
}

# Stage explicit file paths. Refuses to run if any path is missing.
stage_files() {
  cd "$SHIP_REPO_DIR" || die "cannot cd"
  local missing=0
  for f in "$@"; do
    if [ ! -e "$f" ]; then
      log_err "missing file: $f"
      missing=$((missing+1))
    fi
  done
  [ "$missing" -eq 0 ] || die "$missing files missing — refusing to stage"
  git add -- "$@" > "$SHIP_LOG_DIR/stage.log" 2>&1 \
    || die "git add failed — see $SHIP_LOG_DIR/stage.log"
  log_ok "staged $# file(s)"
  git status --short | sed 's/^/    /'
}

# Commit using a message file. Idempotent — if nothing is staged, skip.
do_commit() {
  local msg_file="$1"
  cd "$SHIP_REPO_DIR" || die "cannot cd"
  [ -f "$msg_file" ] || die "commit message file not found: $msg_file"
  if git diff --cached --quiet; then
    log_warn "nothing staged — skipping commit"
    return 0
  fi
  git -c user.name="${SHIP_GIT_NAME:-Dean Dunagan}" \
      -c user.email="${SHIP_GIT_EMAIL:-dunagan.dean@gmail.com}" \
      commit -F "$msg_file" > "$SHIP_LOG_DIR/commit.log" 2>&1 \
    || die "commit failed — see $SHIP_LOG_DIR/commit.log"
  log_ok "committed: $(git log --oneline -1)"
}

# Push the named branch via token URL.
do_push() {
  local branch="$1"
  cd "$SHIP_REPO_DIR" || die "cannot cd"
  git push "$SHIP_REPO_URL" "$branch:$branch" > "$SHIP_LOG_DIR/push.log" 2>&1 \
    || die "push failed — see $SHIP_LOG_DIR/push.log"
  log_ok "pushed $branch"
}

# ---------- build ------------------------------------------------------

# Run a local dotnet build of the main app csproj. Returns 0 on success.
# Output goes to .ship/runs/<branch>/build.log so we can grep for errors.
do_local_build() {
  local csproj="${SHIP_CSPROJ:-Abs.FixedAssets.csproj}"
  cd "$SHIP_REPO_DIR" || die "cannot cd"
  export PATH="$HOME/.dotnet:$PATH"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1
  export DOTNET_NOLOGO=1
  export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
  local log="$SHIP_LOG_DIR/build.log"
  log_info "dotnet build $csproj (output: $log)"

  # Direct redirect — NEVER pipe through tail (deadlocks).
  # --nodereuse:false + --disable-build-servers avoids MSBuild server hangs.
  "$HOME/.dotnet/dotnet" build "$csproj" \
    --nologo -c Debug -v m \
    --nodereuse:false \
    --disable-build-servers \
    > "$log" 2>&1
  local rc=$?

  if [ $rc -ne 0 ]; then
    log_err "dotnet build FAILED (exit $rc) — see $log"
    grep -E "error CS|error MSB" "$log" | head -20 | sed 's/^/    /'
    return $rc
  fi

  local elapsed
  elapsed=$(grep "Time Elapsed" "$log" | tail -1 | awk '{print $NF}')
  local errs warns
  errs=$(grep -oE '[0-9]+ Error\(s\)' "$log" | tail -1 | awk '{print $1}')
  warns=$(grep -oE '[0-9]+ Warning\(s\)' "$log" | tail -1 | awk '{print $1}')
  log_ok "build clean: ${errs:-0} errors, ${warns:-?} warnings, ${elapsed:-?} elapsed"
  return 0
}

# ---------- gh CLI / PR ------------------------------------------------

# Open a PR. Idempotent — if a PR already exists for the head branch,
# just print its URL.
do_pr_create() {
  local branch="$1" title="$2" body_file="$3"
  [ -f "$body_file" ] || die "PR body file not found: $body_file"

  local existing
  existing=$("$GH" pr list --repo "$SHIP_REPO_SLUG" --head "$branch" --json number,url -q '.[0]' 2>/dev/null)
  if [ -n "$existing" ] && [ "$existing" != "null" ]; then
    local num url
    num=$(echo "$existing" | sed -E 's/.*"number":([0-9]+).*/\1/')
    url=$(echo "$existing" | sed -E 's/.*"url":"([^"]+)".*/\1/')
    log_warn "PR already exists for $branch: #$num $url"
    echo "$num" > "$SHIP_LOG_DIR/pr-number"
    return 0
  fi

  "$GH" pr create \
    --repo "$SHIP_REPO_SLUG" \
    --base main \
    --head "$branch" \
    --title "$title" \
    --body-file "$body_file" \
    > "$SHIP_LOG_DIR/pr-create.log" 2>&1 \
    || die "gh pr create failed — see $SHIP_LOG_DIR/pr-create.log"

  local url
  url=$(tail -1 "$SHIP_LOG_DIR/pr-create.log")
  log_ok "PR opened: $url"
  local num
  num=$(echo "$url" | sed -E 's|.*/pull/([0-9]+).*|\1|')
  echo "$num" > "$SHIP_LOG_DIR/pr-number"
}

# Poll CI until it passes or fails. Returns 0 only on all-pass.
#
# gh pr checks exit codes:
#   0  all checks passing
#   1  one or more checks failed  -- OR  no checks reported yet (race)
#   8  some checks pending
#
# We can't distinguish "1 = no checks yet" from "1 = a check failed"
# by exit code alone, so we inspect stdout. "no checks reported" is
# treated as a transient "still scheduling" state for the first
# `no_checks_grace_seconds` of polling.
wait_ci() {
  local pr_num="$1"
  local max_wait="${2:-600}"   # 10 minutes default
  local interval=20
  local no_checks_grace_seconds=90  # tolerate "no checks" for up to 90s
  local elapsed=0
  log_info "polling gh pr checks $pr_num (max ${max_wait}s, interval ${interval}s)"
  while [ "$elapsed" -lt "$max_wait" ]; do
    local out rc
    out=$("$GH" pr checks "$pr_num" --repo "$SHIP_REPO_SLUG" 2>&1)
    rc=$?
    if [ "$rc" -eq 0 ]; then
      log_ok "CI green:"
      echo "$out" | sed 's/^/    /'
      return 0
    elif [ "$rc" -eq 8 ]; then
      log_info "  pending after ${elapsed}s — sleeping ${interval}s"
      sleep "$interval"
      elapsed=$((elapsed + interval))
    else
      # rc=1: either no checks reported yet (transient) or a check failed.
      if echo "$out" | grep -qiE "no checks (reported|found)"; then
        if [ "$elapsed" -lt "$no_checks_grace_seconds" ]; then
          log_info "  no checks reported yet (${elapsed}s/${no_checks_grace_seconds}s grace) — sleeping ${interval}s"
          sleep "$interval"
          elapsed=$((elapsed + interval))
          continue
        fi
        log_err "no checks ever reported after ${no_checks_grace_seconds}s — workflow likely not configured for this branch"
        echo "$out" | sed 's/^/    /'
        return 1
      fi
      log_err "CI failing:"
      echo "$out" | sed 's/^/    /'
      return $rc
    fi
  done
  log_err "CI timed out after ${max_wait}s"
  return 124
}

# Squash-merge + delete branch. Idempotent — if PR is already merged,
# just log and return.
do_merge() {
  local pr_num="$1"
  local state
  state=$("$GH" pr view "$pr_num" --repo "$SHIP_REPO_SLUG" --json state -q .state 2>/dev/null)
  if [ "$state" = "MERGED" ]; then
    log_warn "PR #$pr_num already merged — skipping"
    return 0
  fi
  if [ "$state" = "CLOSED" ]; then
    die "PR #$pr_num is closed (not merged) — refusing to proceed"
  fi
  "$GH" pr merge "$pr_num" --repo "$SHIP_REPO_SLUG" \
    --squash --delete-branch \
    > "$SHIP_LOG_DIR/merge.log" 2>&1 \
    || die "merge failed — see $SHIP_LOG_DIR/merge.log"
  local commit
  commit=$("$GH" pr view "$pr_num" --repo "$SHIP_REPO_SLUG" --json mergeCommit -q .mergeCommit.oid 2>/dev/null)
  log_ok "PR #$pr_num squash-merged at $commit"
  echo "$commit" > "$SHIP_LOG_DIR/merge-commit"
}

# Post the ship comment on the PR.
do_comment() {
  local pr_num="$1" body_file="$2"
  [ -f "$body_file" ] || die "comment body file not found: $body_file"
  "$GH" pr comment "$pr_num" --repo "$SHIP_REPO_SLUG" \
    --body-file "$body_file" \
    > "$SHIP_LOG_DIR/comment.log" 2>&1 \
    || die "comment failed — see $SHIP_LOG_DIR/comment.log"
  log_ok "ship comment posted"
}
