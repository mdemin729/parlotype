---
title: Language UX rebuild (keyboard-layout source, model-driven target forms, popover pickers, transcribe quick picker)
status: completed
created: 2026-06-08
started: 2026-06-11
completed: 2026-06-11
---

# Language UX Rebuild

## Problem

The ADR-035 Language page introduced the `[Source] → [Target]` arrow-toggle row, inline
pickers, per-role MRU, and the `TranslationEnabled` master key. A fresh hi-fi prototype
(`tmp/parlotype-language.html` + `tmp/parlotype-language-spec.md`, answering the
[design brief](../2026-06-01-language-settings-redesign/design-brief.md)) pushes the UX
further than the current build and is the new target. Key gaps vs. today:

1. **System keyboard layout** is now a **first-class source** (new requirement) with detected
   layout sub-hint — not implemented.
2. The target side must be **model-driven**: a **toggle** for the 2-option case (Whisper),
   a **full picker** for many (Gemma), and a **disabled + explained** state when translation
   is unavailable — today Whisper renders a full picker and there is no "unavailable" state.
3. The connector should **swap glyph** (`→`/`=`/locked), there should be a plain-language
   **summary line**, and engine/model switches should **fall back with toasts** — none exist.
4. Pickers should be **floating popovers** with richer rows (icons, native subnames, group
   labels, empty state) — today they expand inline.
5. The **Transcribe window** needs a **quick-picker strip + flyout** for fast translation
   control — today the widget has no language control at all.

## Goal

Deliver the prototype's UX across both surfaces, engine-agnostic and capability-driven, while
keeping the two surfaces consistent through one shared relationship model.

- **Phase 1 — Settings → Language page** (authoritative control).
- **Phase 2 — Transcribe window quick picker** (compact, always-visible).

## Deliverables

- [specification.md](specification.md) — prototype behaviour restated as an engine-agnostic spec.
- [requirements.md](requirements.md) — functional / non-functional requirements + acceptance criteria.
- [implementation-plan.md](implementation-plan.md) — phased plan (P0 shared foundation → P1 → P2).

## Confirmed scope decisions

- **Keyboard-layout detection**: Windows-only now via a clean Core interface; graceful
  null/fallback on macOS/Linux.
- **"Translation unavailable" state**: built now, capability-driven, even though no shipping
  engine triggers it yet.
- **Picker presentation**: faithful floating **popover/flyout** (not inline expansion).

## Relationship to prior work

Supersedes the UX of **ADR-035** (`2026-05-31-language-settings-ux-redesign`) and builds on the
data model from **ADR-034** (`2026-05-25-language-selection`) and translation capability from
**ADR-033**. A new **ADR-036** will capture this rebuild and mark ADR-035 superseded.

## Out of scope

- New recognition engines/pipelines (the `None` capability path is built but unwired).
- Cross-platform keyboard-layout detection beyond graceful fallback.
- Settings taxonomy / model-selection UI / non-language settings; touch interaction.

## Verification (high level)

- Zero-warning `dotnet build Parlotype.slnx`; `dotnet test` green.
- All §6 states + §8 fallbacks from the spec reproduced; dark + light screenshots.
- Whisper → toggle form; Gemma → full popover; unavailable → disabled + amber + locked.
- `source = keyboard` resolves through the pipeline to the detected layout language.
- Transcribe strip mirrors the page; connector toggles translation in one click.
- ADR-036 written; memory vault + `plans/INDEX.md` updated.
