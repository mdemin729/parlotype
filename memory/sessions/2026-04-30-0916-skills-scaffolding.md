---
title: "Session: 2026-04-30 09:16 — skills scaffolding"
type: session
status: active
tags: [skills, claude-skills, session-management, claude-md]
created: 2026-04-30
summary: "Mirrored memory/skills as discoverable .claude/skills, promoted session-management into CLAUDE.md, adopted timestamped session-note naming."
---

# Session: 2026-04-30 09:16 — skills scaffolding

## Active Focus
- `.claude/skills/` (new): created `debug-pipeline/`, `implement-feature/`,
  `obsidian-markdown/`, `session-management/`, each with a `SKILL.md` carrying
  `name` + `description` frontmatter for auto-discovery. Bodies mirror
  `memory/skills/*.md` with `[[wikilinks]]` rewritten to relative paths and a
  "See also" pointer back to the canonical Obsidian source.
- `CLAUDE.md` — replaced the 4-line "Agent Startup Protocol" subsection with
  a fuller "Session Lifecycle" section (Start / End / Knowledge Distillation)
  plus a pointer to the session-management skill.
- Session-note naming convention reworked across three files:
  `.claude/skills/session-management/SKILL.md`,
  `memory/skills/session-management.md`, and the `CLAUDE.md` Session Lifecycle.

## Decisions Made
- **Discoverable skills live under `.claude/skills/<name>/SKILL.md`** with
  `name` + `description` frontmatter (description starting with "Use when…").
  Memory-vault `memory/skills/*.md` remain the canonical Obsidian source; the
  `.claude/skills/` copy is a discovery-friendly mirror.
- **Always-on protocols belong in `CLAUDE.md`**, not in description-triggered
  skills. The `session-management` skill exists for reference, but the
  invariant "do this every session" lives in CLAUDE.md so it loads
  unconditionally.
- **New session-note naming:** `YYYY-MM-DD-HHMM-<slug>.md`
  (e.g. `2026-04-30-0916-skills-scaffolding.md`). Lexicographic order matches
  chronological order; no collision suffix needed; the slug aids scanning.

## Facts Learned
- Description-based skill auto-discovery only fires when the agent's current
  reasoning matches the trigger phrasing. It does **not** reliably activate at
  session boundaries (the agent rarely thinks "I'm starting a session"
  explicitly), so per-session protocols cannot rely on skill auto-loading and
  must instead live in always-loaded files like `CLAUDE.md`.
- Claude/Copilot CLI skills require directory-per-skill layout
  (`.claude/skills/<kebab-name>/SKILL.md`) — a flat `*.md` collection is not
  picked up.
- → captured in `memory/knowledge/agent-skills.md`.

## Open Blockers
- None.

## Documentation Status
- ADR: none required (no Core/Platform/csproj/audio/hotkey/Whisper changes —
  pure documentation and agent-runtime config).
- Vault (services/architecture): none required (no public symbols changed).
- Knowledge (non-derivable facts): done — `memory/knowledge/agent-skills.md`.

## Next Action
No in-flight work. When resuming, start from the next user request. If touching
session lifecycle or skill scaffolding, reread `.claude/skills/session-management/SKILL.md`
and the "Session Lifecycle" section of `CLAUDE.md` — they were updated this session.
