---
title: Knowledge Base
type: index
status: active
last_updated: 2026-03-28
summary: Semantic memory — stable facts learned across sessions that are not derivable from code
---

# Knowledge Base

This directory stores **stable facts** learned across sessions — things that are not obvious from reading the code alone.

## How to Add Knowledge
1. Create a new `.md` file in this directory with YAML frontmatter
2. Include: `type: knowledge`, `tags`, `created`, `summary`
3. Add an entry to this index

## Entries

<!-- Add entries as facts are learned across sessions -->
<!-- Format: | [[filename]] | one-line summary | date | -->

| Fact | Summary | Learned |
|------|---------|---------|
| _No entries yet_ | Knowledge accumulates as agents work on the project | — |

## Distillation Rules
- Only store facts that are **not derivable** from reading current code or git history
- Include the "why" — reasoning, context, constraints
- Update or remove entries when they become stale
- Prefer specific, actionable facts over vague observations
