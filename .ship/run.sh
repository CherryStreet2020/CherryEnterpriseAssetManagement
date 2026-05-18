#!/bin/bash
# .ship/run.sh — single-command ship harness for CherryAI EAM PRs.
#
# Implements the cowork-github-replit-process workflow as one idempotent,
# recoverable shell script. Encodes every pitfall we hit on PRs #1 + #2.
#
# Usage:
#   bash .ship/run.sh <subcommand> [args]
#
# Subcommands:
#   full <config>                # end-to-end: build → branch → PR → merge → comment
#   preflight                    # sanity-check env, sync local main
#   build                        # local dotnet build only
#   branch-commit-push <config>  # branch + stage + commit + push
#   open-pr <config>             # open PR + write pr-number to log dir
#   wait-ci <pr-num>             # poll CI until green/fail
#   merge <pr-num>               # squash-merge + delete branch
#   comment <pr-num> <body-file> # post a comment
#
# Config file (sourced as bash) — typically .ship/configs/<branch>.sh.
# Required exports:
#   BRANCH       feat/sprint-11-pr3-receiving-service
#   TITLE        feat(sprint-11): ...
#   COMMIT_MSG   /path/to/commit-msg.txt
#   PR_BODY      /path/to/pr-body.md
#   FILES        array of paths to stage  (e.g. FILES=( "Pages/..." "Services/..." ))
# Optional exports:
#   SHIP_COMMENT /path/to/ship-comment.md   (skips comment if unset)
#   SKIP_BUILD   1                          (skip local dotnet build)
#
# Example .ship/configs/pr3.sh:
#   #!/bin/bash
#   BRANCH="feat/sprint-11-pr3-receiving-service"
#   TITLE="feat(sprint-11): IReceivingControlCenterService + state machine"
#   COMMIT_MSG="/Users/deandunagan/Documents/Claude/Projects/EnterpriseAssetManagament/.ship/msgs/pr3-commit.txt"
#   PR_BODY="/Users/deandunagan/Documents/Claude/Projects/EnterpriseAssetManagament/.ship/msgs/pr3-body.md"
#   SHIP_COMMENT="/Users/deandunagan/Documents/Claude/Projects/EnterpriseAssetManagament/.ship/msgs/pr3-comment.md"
#   FILES=(
#     "Services/Receiving/IReceivingControlCenterService.cs"
#     "Services/Receiving/ReceivingControlCenterService.cs"
#     "Models/Receiving/...etc"
#   )
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export SHIP_REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
export SHIP_REPO_SLUG="${SHIP_REPO_SLUG:-CherryStreet2020/CherryEnterpriseAssetManagement}"
export SHIP_CSPROJ="${SHIP_CSPROJ:-Abs.FixedAssets.csproj}"
export GH="${GH:-/Users/deandunagan/bin/gh}"

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

# Set up SHIP_LOG_DIR for the given branch name.
init_log_dir() {
  local branch="${1:-default}"
  # Hex-sanitize branch for filesystem safety (replace / with _).
  local safe="${branch//\//_}"
  export SHIP_LOG_DIR="$SCRIPT_DIR/runs/$safe"
  mkdir -p "$SHIP_LOG_DIR"
}

# Source a config file and validate its contents.
load_config() {
  local cfg="$1"
  [ -f "$cfg" ] || die "config file not found: $cfg"
  # shellcheck disable=SC1090
  source "$cfg"
  : "${BRANCH:?BRANCH not set in $cfg}"
  : "${TITLE:?TITLE not set in $cfg}"
  : "${COMMIT_MSG:?COMMIT_MSG not set in $cfg}"
  : "${PR_BODY:?PR_BODY not set in $cfg}"
  [ -f "$COMMIT_MSG" ] || die "COMMIT_MSG file not found: $COMMIT_MSG"
  [ -f "$PR_BODY" ]    || die "PR_BODY file not found: $PR_BODY"
  [ "${#FILES[@]}" -gt 0 ] || die "FILES array is empty"
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
  log_step "1" "Local dotnet build"
  do_local_build
}

cmd_branch_commit_push() {
  load_config "$1"
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
  load_config "$1"
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
  load_config "$cfg"
  init_log_dir "$BRANCH"

  log_step "0" "Preflight"
  check_gh
  get_token
  disable_keychain
  sync_origin_main
  reset_local_main

  if [ "${SKIP_BUILD:-0}" != "1" ]; then
    log_step "1" "Local dotnet build"
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
      sed -n '2,30p' "${BASH_SOURCE[0]}"
      ;;
    *)
      log_err "unknown subcommand: $sub"
      sed -n '2,30p' "${BASH_SOURCE[0]}"
      exit 2
      ;;
  esac
}

main "$@"
