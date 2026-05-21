#!/usr/bin/env bash
# Flags vault notes where last_updated exceeds a threshold.
# Usage: bash memory/scripts/check-staleness.sh [days]
# Default threshold: 90 days
#
# Resolves the vault directory relative to this script so it works
# whether you invoke it as `bash memory/scripts/check-staleness.sh`
# or from any other working directory (including Git-Bash / WSL on
# Windows). File must be saved with LF line endings.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VAULT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
THRESHOLD_DAYS="${1:-90}"

echo "=== Staleness Check (threshold: ${THRESHOLD_DAYS} days) ==="
echo "Vault: $VAULT_DIR"
echo ""

threshold_date=$(date -d "-${THRESHOLD_DAYS} days" +%Y-%m-%d 2>/dev/null || date -v-${THRESHOLD_DAYS}d +%Y-%m-%d 2>/dev/null || echo "")

if [ -z "$threshold_date" ]; then
  echo "ERROR: Could not compute threshold date. Requires GNU date or macOS date."
  exit 1
fi

echo "Notes older than $threshold_date:"
echo ""

stale=0
missing=0
while IFS= read -r file; do
  rel="${file#$VAULT_DIR/}"
  # Extract last_updated from frontmatter
  last_updated=$(grep -m1 "^last_updated:" "$file" 2>/dev/null | sed 's/last_updated: *//' | tr -d '"' | tr -d "'" || echo "")

  if [ -z "$last_updated" ]; then
    echo "  NO DATE: $rel"
    missing=$((missing + 1))
    continue
  fi

  if [[ "$last_updated" < "$threshold_date" ]]; then
    echo "  STALE ($last_updated): $rel"
    stale=$((stale + 1))
  fi
done < <(find "$VAULT_DIR" -name "*.md" -not -path "*/.obsidian/*" -not -path "*/scripts/*" -not -path "*/sessions/*")

echo ""
if [ "$stale" -eq 0 ] && [ "$missing" -eq 0 ]; then
  echo "No stale notes found."
else
  [ "$stale" -gt 0 ] && echo "$stale stale note(s) found. Consider updating or archiving."
  [ "$missing" -gt 0 ] && echo "$missing note(s) missing last_updated frontmatter."
fi