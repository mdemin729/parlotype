---
title: Agent Skills & Per-Session Protocols
type: knowledge
status: active
tags: [agent, skills, claude, copilot, session-management]
created: 2026-04-30
last_updated: 2026-05-04
summary: How Claude/Copilot skill auto-discovery actually behaves, and why per-session protocols belong in CLAUDE.md rather than description-triggered skills.
---

# Agent Skills & Per-Session Protocols

## Skill discovery layout

Claude/Copilot CLI discovers skills only when they live as
`.claude/skills/<kebab-name>/SKILL.md` (directory per skill).

Each `SKILL.md` must include YAML frontmatter with at minimum:

```yaml
---
name: <kebab-name>          # must match the directory name
description: Use when ...   # one-line trigger sentence
---
```

The `description` is what the agent matches against to decide whether to
auto-invoke the skill. Phrasing it as "Use when …" keeps the trigger
behavioural and concrete.

## When description-triggered skills fail

Description-based auto-discovery only fires when the agent's *current
reasoning* matches the trigger. It does **not** reliably activate at moments
the agent does not explicitly think about — most notably session boundaries.
The agent rarely reasons "I am starting a session" or "I am ending a session"
unless prompted, so a session-lifecycle skill cannot be trusted to fire on its
own.

Consequence: **always-on per-session protocols belong in `CLAUDE.md`**, which
is loaded unconditionally on every turn. Description-triggered skills work
well for *topic*-driven activation (e.g. "I am debugging the audio pipeline")
but not for *temporal* activation (e.g. "the session is starting / ending").

## Recommended split

| Where it lives | What goes there |
|----------------|-----------------|
| `CLAUDE.md` | Invariants and protocols that must apply every session/turn (Definition of Done, Session Lifecycle summary, hard architectural rules). |
| `.claude/skills/<x>/SKILL.md` | Topic-triggered workflows the agent should auto-load when the user's request matches (debug-pipeline, implement-feature, obsidian-markdown, release-notes). |

`release-notes` is the clearest case of the topic-triggered pattern working: "we
are cutting a release" is something the agent *does* reason about explicitly, and
the skill carries editorial rules (what counts as user-facing, which words are
banned in a bullet) that would be dead weight in `CLAUDE.md` on every other turn.

## See also
- `.claude/skills/session-management/SKILL.md`
- `CLAUDE.md` → "Session Lifecycle"
