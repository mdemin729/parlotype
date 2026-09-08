---
title: "Session: master diff review"
type: session
status: completed
tags: [code-review, parakeet, desktop]
created: 2026-07-09
summary: "Reviewed and fixed a startup race in the active-engine badge."
---

# Session: master diff review

## Active Focus

Reviewed the complete `master...HEAD` change set, then fixed the Transcribe
window active-engine state race.

## Decisions Made

- Serialized startup initialization and live engine selections with a private
  lock, so a user-initiated selection always takes precedence when it occurs
  during the settings read.

## Facts Learned

- `TranscribeViewModel` initializes the active engine asynchronously while live
  engine switches synchronously update the same UI state; the ordering is now
  covered by a delayed-settings regression test.

## Open Blockers

- None.

## Documentation Status

- ADR: none required
- Vault (services/architecture): none required
- Knowledge (non-derivable facts): none

## Next Action

None.
