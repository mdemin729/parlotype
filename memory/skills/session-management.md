---
title: Session Management Skill
type: skill
status: active
tags: [skill, session, handoff, memory]
last_updated: 2026-03-28
summary: Protocols for session startup, handoff, and knowledge distillation
---

# Session Management Skill

## Session Start Protocol

1. Read `memory/AGENTS.md` for orientation
2. Check `memory/sessions/` for the most recent session handoff
3. Read the **Next Action** from the last session to understand where to pick up
4. Check `memory/knowledge/_index.md` for recently learned facts

## Session End Protocol

1. Create a new session note from `memory/sessions/_template.md`
2. Name it `YYYY-MM-DD.md` (or `YYYY-MM-DD-N.md` for multiple sessions per day)
3. Fill in all sections:
   - **Active Focus**: files/functions/features worked on
   - **Decisions Made**: technical choices accepted
   - **Facts Learned**: new codebase or environment discoveries
   - **Open Blockers**: unresolved issues
   - **Next Action**: explicit starting point for next session

## Knowledge Distillation

After completing a session, review Facts Learned:
- If a fact is **stable and not derivable from code**: add it to `memory/knowledge/`
- If a fact updates existing knowledge: edit the relevant knowledge file
- If a fact is temporary or ephemeral: leave it only in the session note

## Memory Pruning

Periodically (weekly or when vault feels cluttered):
- Archive sessions older than 30 days to `memory/sessions/archive/`
- Remove knowledge entries that are now derivable from code
- Update `last_updated` on any modified notes
- Check for orphan notes (no incoming links)
