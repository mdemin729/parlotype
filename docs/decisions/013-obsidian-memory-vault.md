---
status: accepted
date: 2026-03-28
---

# 013. Obsidian-Based Persistent Memory Vault for AI Agents

## Context

AI coding agents (Claude Code, Copilot, Cursor) are stateless — every session starts from scratch. In practice this means:

- Agents repeatedly re-discover the same architectural patterns and conventions
- Session-to-session continuity requires re-explaining context manually
- Learned facts about the codebase (quirks, constraints, debugging insights) are lost between sessions
- Cross-cutting knowledge (how subsystems interact, why decisions were made) must be re-derived each time

The project already has CLAUDE.md for agent instructions and ADRs in `docs/decisions/` for design rationale, but lacks a structured system for episodic memory (session continuity), semantic memory (learned facts), and progressive knowledge retrieval.

## Decision

Create an Obsidian vault in `memory/` at the project root that serves as the persistent cognitive substrate for AI agents. The vault uses:

- **Three-tier progressive disclosure**: root router (`CLAUDE.md`, ~60 lines) → directory indexes (`_index.md`) → full documents. Agents read Tier 1, decide which Tier 2 index to consult, then pull specific Tier 3 documents. This keeps token budgets lean (~500-2000 tokens for orientation vs. loading everything).

- **Five memory layers**:
  - **Procedural** (`conventions/`, `skills/`): how to do things — coding standards, agent skills
  - **Semantic** (`architecture/`, `services/`, `knowledge/`): facts about the codebase
  - **Episodic** (`sessions/`): session handoffs with active focus, decisions, blockers, next actions
  - **Decision** (`decisions/`): links to ADRs explaining why things are the way they are
  - **Identity** (`CLAUDE.md`): project overview, constraints, navigation

- **Obsidian-native features**: YAML frontmatter on every note (type, status, tags, last_updated, summary), `[[wikilinks]]` for internal linking, callouts for important information.

- **Maintenance scripts**: index generator with orphan detection, staleness checker for notes older than N days.

- **Agent skills**: specialized instruction files for session management, debugging the audio pipeline, implementing features, and using Obsidian markdown correctly.

Alternatives considered:
- **Vector database (Mem0, ChromaDB)**: rejected — adds infrastructure complexity, not human-readable, overkill for a single-repo project
- **Flat CLAUDE.md expansion**: rejected — monolithic files degrade agent performance past ~200 instructions ("lost-in-the-middle" effect)
- **External wiki/Notion**: rejected — violates local-first principle, requires API access, not version-controlled

## Consequences

**Easier:**
- Agent sessions have continuity via session handoffs — pick up exactly where the last session left off
- Stable facts persist across sessions without re-discovery
- New agents/contributors can orient quickly via progressive disclosure
- Human developers can browse the vault in Obsidian for architecture overview
- Knowledge graph (`[[wikilinks]]`) reveals relationships between subsystems
- Version-controlled alongside code — no external dependencies

**More difficult:**
- Vault requires periodic maintenance (staleness checks, pruning, index updates)
- Risk of memory staleness if vault is not updated alongside code changes
- Additional files in the repo (~30 markdown notes initially)
- Agents must be instructed to follow the startup/handoff protocol
