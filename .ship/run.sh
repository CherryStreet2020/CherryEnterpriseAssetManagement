#!/bin/bash
# .ship/run.sh — portable single-command ship harness.
#
# Implements steps 1-6 of the cowork-github-replit-process skill as one
# idempotent shell script. Steps 7 (Replit pull) and 8 (E2E verify) stay
# manual since they involve the browser + the live URL.
#
# Usage:
#   bash .ship/run.sh <subcommand> [args]
#
# Subcommands:
#   full <pr-config>             # end-to-end: build → branch → PR → merge → comment
#   preflight                    # sanity-check env, sync local main
#   build                        # local build only
#   branch-commit-push <cfg>     # branch + stage + commit + push
#   open-pr <cfg>                # open PR + write pr-number to log dir
#   wait-ci <pr-num> [max-sec]   # poll CI until green/fail (default 600s)
#   merge <pr-num>               # squash-merge + delete branch
#   comment <pr-num> <body-file> # post a comment
#
# CONFIG FILES
# ------------
#
# Project-level config: ".ship-config.sh" at the repo root.
# Sourced once at startup. Required exports:
#
#   SHIP_REPO_SLUG          e.g. "acme/myrepo"
#   GH                      absolute path to gh CLI (e.g. "/Users/me/bin/gh")
#
# Optional:
#   SHIP_DEFAULT_BASE_BRANCH  usually "main"
#   SHIP_BUILD_CMD            shell command for local build
#   SHIP_BUILD_SUCCESS_RE     regex to verify build success
#   SHIP_GIT_NAME             commit author name (defaults to git's global)
#   SHIP_GIT_EMAIL            commit author email
#
# Per-PR config: ".ship/configs/<name>.sh". Sourced when needed. Required:
#
#   BRANCH        branch name (e.g. "feat/sprint-7-cool-feature")
#   TITLE         PR title
#   COMMIT_MSG    path to commit-message file (use -F, never inline)
#   PR_BODY       path to PR-body markdown file (--body-file)
#   FILES         array of paths to stage  (e.g. FILES=( "src/foo.cs" "src/bar.cs" ))
#
# Optional:
#   SHIP_COMMENT  path to a ship-comment markdown file
#   SKIP_BUILD    set to 1 to skip the local build (docs-only PRs)
#
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export SHIP_REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Load project-level config from the repo root.
if [ -f "$SHIP_REPO_DIR/.ship-config.sh" ]; then
  # shellcheck disable=SC1090
  source "$SHIP_REPO_DIR/.ship-config.sh"
elif [ -f "$SCRIPT_DIR/.ship-config.sh" ]; then
  # Alternate placement: inside .ship/ itself
  # shellcheck disable=SC1090
  source "$SCRIPT_DIR/.ship-config.sh"
fi

: "${SHIP_REPO_SLUG:?SHIP_REPO_SLUG must be set in .ship-config.sh — e.g. SHIP_REPO_SLUG=\"acme/myrepo\"}"
: "${GH:?GH must be set in .ship-config.sh — absolute path to the gh CLI binary}"

# shellcheck disable=SC1090
source "$SCRIPT_DIR/lib.sh"

# Discover the gh token. Cached in env after first call.
get_token() {
  [ -n "${SHIP_TOKEN:-}" ] && return 0
  export SHIP_TOKEN
  SHIP_TOKEN=$("$GH" auth token 2>/dev/null) || die "gh auth token failed"
  [ -n "$SHIP_TOKEN" ] || die "gh returned empty token"
  export SHIP_REPO_URL="https://x-access-token:${SHIP_TOKEN}@github.com/${SHIP_REPO_SLUG}.git"
}

init_log_dir() {
  local label="${1:-default}"
  local safe="${label//\//_}"
  export SHIP_LOG_DIR="$SCRIPT_DIR/runs/$safe"
  mkdir -p "$SHIP_LOG_DIR"
}

load_pr_config() {
  local cfg="$1"
  [ -f "$cfg" ] || die "PR config file not found: $cfg"
  # shellcheck disable=SC1090
  source "$cfg"
  : "${BRANCH:?BRANCH not set in $cfg}"
  : "${TITLE:?TITLE not set in $cfg}"
  : "${COMMIT_MSG:?COMMIT_MSG not set in $cfg}"
  : "${PR_BODY:?PR_BODY not set in $cfg}"
  [ -f "$COMMIT_MSG" ] || die "COMMIT_MSG file not found: $COMMIT_MSG"
  [ -f "$PR_BODY" ]    || die "PR_BODY file not found: $PR_BODY"
  [ "${#FILES[@]}" -gt 0 ] || die "FILES array is empty in $cfg"
}

# ---------- subcommands ----------------------------------------------

cmd_preflight() {
  init_log_dir "preflight"
  log_step "0" "Preflight"
  check_gh
  get_token
  disable_keychain
  sync_origin_main
  reset_local_main
  log_ok "preflight ok"
}

cmd_build() {
  init_log_dir "build"
  log_step "1" "Local build"
  do_local_build
}

cmd_branch_commit_push() {
  load_pr_config "$1"
  init_log_dir "$BRANCH"
  log_step "2" "Branch · commit · push"
  get_token
  disable_keychain
  make_branch "$BRANCH"
  stage_files "${FILES[@]}"
  do_commit "$COMMIT_MSG"
  do_push "$BRANCH"
}

cmd_open_pr() {
  load_pr_config "$1"
  init_log_dir "$BRANCH"
  log_step "3" "Open PR"
  do_pr_create "$BRANCH" "$TITLE" "$PR_BODY"
}

cmd_wait_ci() {
  local pr="$1"
  init_log_dir "pr-$pr"
  log_step "4" "Wait for CI"
  wait_ci "$pr" "${2:-600}"
}

cmd_merge() {
  local pr="$1"
  init_log_dir "pr-$pr"
  log_step "5" "Squash-merge"
  do_merge "$pr"
  cd "$SHIP_REPO_DIR" || die "cannot cd"
  get_token
  disable_keychain
  sync_origin_main
  reset_local_main
}

cmd_comment() {
  local pr="$1" body="$2"
  init_log_dir "pr-$pr"
  log_step "6" "Ship comment"
  do_comment "$pr" "$body"
}

cmd_full() {
  local cfg="$1"
  load_pr_config "$cfg"
  init_log_dir "$BRANCH"

  log_step "0" "Preflight"
  check_gh
  get_token
  disable_keychain
  sync_origin_main
  reset_local_main

  if [ "${SKIP_BUILD:-0}" != "1" ]; then
    log_step "1" "Local build"
    do_local_build || die "build failed — refusing to ship"
  else
    log_warn "skipping local build (SKIP_BUILD=1)"
  fi

  log_step "2" "Branch · commit · push"
  make_branch "$BRANCH"
  stage_files "${FILES[@]}"
  do_commit "$COMMIT_MSG"
  do_push "$BRANCH"

  log_step "3" "Open PR"
  do_pr_create "$BRANCH" "$TITLE" "$PR_BODY"
  local pr_num
  pr_num=$(cat "$SHIP_LOG_DIR/pr-number")

  log_step "4" "Wait for CI"
  wait_ci "$pr_num" 900 || die "CI not green — halting before merge"

  log_step "5" "Squash-merge"
  do_merge "$pr_num"
  sync_origin_main
  reset_local_main

  if [ -n "${SHIP_COMMENT:-}" ]; then
    log_step "6" "Ship comment"
    do_comment "$pr_num" "$SHIP_COMMENT"
  fi

  log_step "✓" "DONE"
  log_ok "PR #$pr_num shipped — see $SHIP_LOG_DIR for full logs"
  log_info "NEXT: Step 7 (Replit pull) and Step 8 (E2E verify) are still manual."
}

# ---------- dispatch ---------------------------------------------------

main() {
  local sub="${1:-help}"
  shift || true
  case "$sub" in
    preflight)              cmd_preflight ;;
    build)                  cmd_build ;;
    branch-commit-push)     cmd_branch_commit_push "$@" ;;
    open-pr)                cmd_open_pr "$@" ;;
    wait-ci)                cmd_wait_ci "$@" ;;
    merge)                  cmd_merge "$@" ;;
    comment)                cmd_comment "$@" ;;
    full)                   cmd_full "$@" ;;
    help|-h|--help|"")
      sed -n '2,60p' "${BASH_SOURCE[0]}"
      ;;
    *)
      log_err "unknown subcommand: $sub"
      sed -n '2,60p' "${BASH_SOURCE[0]}"
      exit 2
      ;;
  esac
}

main "$@"
