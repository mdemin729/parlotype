#!/usr/bin/env bash
# Regenerates _index.md files from vault content.
# Usage: bash memory/scripts/generate-index.sh

set -euo pipefail

VAULT_DIR="$(cd "$(dirname "$0")/.." && pwd)"

echo "=== Parlotype Memory Vault Index Generator ==="
echo "Vault: $VAULT_DIR"
echo ""

# Count notes by type
total=$(find "$VAULT_DIR" -name "*.md" -not -path "*/.obsidian/*" -not -path "*/scripts/*" | wc -l)
echo "Total notes: $total"

for dir in architecture services conventions decisions sessions knowledge skills; do
  if [ -d "$VAULT_DIR/$dir" ]; then
    count=$(find "$VAULT_DIR/$dir" -name "*.md" | wc -l)
    echo "  $dir/: $count notes"
  fi
done

echo ""

# Check for notes missing frontmatter
echo "=== Notes missing frontmatter ==="
missing=0
while IFS= read -r file; do
  if ! head -1 "$file" | grep -q "^---$"; then
    echo "  MISSING: $file"
    missing=$((missing + 1))
  fi
done < <(find "$VAULT_DIR" -name "*.md" -not -path "*/.obsidian/*" -not -path "*/scripts/*" -not -name "AGENTS.md")

if [ "$missing" -eq 0 ]; then
  echo "  All notes have frontmatter."
fi

echo ""

# Check for orphan notes (no incoming wikilinks)
echo "=== Orphan Detection ==="
orphans=0
while IFS= read -r file; do
  basename=$(basename "$file" .md)
  # Skip index files and templates
  if [[ "$basename" == "_index" || "$basename" == "_template" || "$basename" == "AGENTS" || "$basename" == "vault-map" ]]; then
    continue
  fi
  # Search for wikilinks pointing to this note
  if ! grep -rq "\[\[$basename" "$VAULT_DIR" --include="*.md" 2>/dev/null; then
    echo "  ORPHAN: $file"
    orphans=$((orphans + 1))
  fi
done < <(find "$VAULT_DIR" -name "*.md" -not -path "*/.obsidian/*" -not -path "*/scripts/*")

if [ "$orphans" -eq 0 ]; then
  echo "  No orphan notes found."
fi

echo ""
echo "Done."
