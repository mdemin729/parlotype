#!/usr/bin/env bash
# Flags vault notes where last_updated exceeds a threshold.
# Usage: bash memory/scripts/check-staleness.sh [days]
# Default threshold: 90 days

set -euo pipefail

VAULT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
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
while IFS= read -r file; do
  # Extract last_updated from frontmatter
  last_updated=$(grep -m1 "^last_updated:" "$file" 2>/dev/null | sed 's/last_updated: *//' | tr -d '"' | tr -d "'" || echo "")

  if [ -z "$last_updated" ]; then
    echo "  NO DATE: $(basename "$file")"
    continue
  fi

  if [[ "$last_updated" < "$threshold_date" ]]; then
    echo "  STALE ($last_updated): $file"
    stale=$((stale + 1))
  fi
done < <(find "$VAULT_DIR" -name "*.md" -not -path "*/.obsidian/*" -not -path "*/scripts/*")

echo ""
if [ "$stale" -eq 0 ]; then
  echo "No stale notes found."
else
  echo "$stale stale note(s) found. Consider updating or archiving."
fi
