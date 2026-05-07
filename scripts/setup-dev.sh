#!/usr/bin/env bash
# One-time setup for a fresh local clone.
# Run this once after cloning the repo on a new machine.

set -euo pipefail

cd "$(dirname "$0")/.."

echo "→ Configuring git hooks (.githooks)"
git config core.hooksPath .githooks

echo "→ Marking hooks executable"
chmod +x .githooks/*

if ! command -v gitleaks >/dev/null 2>&1; then
  echo "→ gitleaks not found. Recommended: brew install gitleaks"
  echo "  (the pre-commit hook falls back to a lightweight regex scan without it)"
fi

echo "→ Verifying git identity"
name=$(git config user.name || echo "")
email=$(git config user.email || echo "")
if [ -z "$name" ] || [ -z "$email" ]; then
  echo "  ⚠  No git identity set on this clone. Run:"
  echo "     git config user.name  \"Your Name\""
  echo "     git config user.email \"your-email@example.com\""
fi

echo
echo "✓ Setup complete. See CLAUDE.md for the workflow."
