---
title: "Session: 2026-06-08 — Language UX Rebuild (planning)"
type: session
status: active
tags: [language, translation, settings, transcribe-window, ux, planning, adr-036]
created: 2026-06-08
summary: "Planned the Language UX rebuild from the new hi-fi prototype: keyboard-layout source, model-driven target forms (toggle/full/none), floating popover pickers, summary + switch-fallback toasts (Phase 1) and a Transcribe-window quick-picker strip + flyout (Phase 2). Artifacts in plans/2026-06-08-language-ux-rebuild/. Next ADR = 036. Build to start on autopilot."
---

# Session: 2026-06-08 — Language UX Rebuild (planning)

## Active Focus

Authored the planning artifacts for a full Language UX rebuild driven by the new hi-fi
prototype (`tmp/parlotype-language.html` + `tmp/parlotype-language-spec.md`, the designer's
answer to `plans/2026-06-01-language-settings-redesign/design-brief.md`).

**New plan folder:** [`plans/2026-06-08-language-ux-rebuild/`](../../plans/2026-06-08-language-ux-rebuild/)
- `task.md` — problem/goal/scope/confirmed decisions/verification
- `specification.md` — prototype JS state machine + spec restated engine-agnostically
- `requirements.md` — FR/NFR + acceptance criteria with traceability
- `implementation-plan.md` — phased plan (P0 shared foundation → P1 page → P2 widget)

No production code written yet — this was a plan-mode session.

## Decisions Made

- **Evolve, don't fork.** Build on the ADR-035 implementation rather than from a blank slate;
  a new **ADR-036** supersedes ADR-035.
- **Keyboard-layout source = Windows-only now.** New Core interface `IKeyboardLayoutService`
  with a Win32 implementation; macOS/Linux return null and degrade gracefully. (User choice.)
- **Build the "translation unavailable" (None) state now**, capability-driven, even though no
  shipping engine triggers it. (User choice.)
- **Pickers become floating popovers**, not inline-expand. (User choice.)
- **Model-driven target forms:** add `TranslationForm` (None/Toggle/Full) to
  `LanguageCapabilities`; Whisper → Toggle (Switch), Gemma → Full (popover), future
  transcribe-only → None (disabled + amber + locked connector).
- **Shared `LanguageRelationshipViewModel`** (new) owns source/target/translation/MRU/fallback
  so the Settings page and Transcribe window stay consistent — the linchpin refactor (P0).
- **Connector glyph swap** `→`/`=`/locked + **summary line** + **switch-fallback toasts**.

## Facts Learned

- Current ADR-035 build already has: arrow-toggle row, inline source/target pickers, per-role
  MRU (`RecentSourceLanguages`/`RecentTargetLanguages`), `TranslationEnabled` master key,
  `LanguageSettingsMigrator`, and the "translation paused" note (ADR-033 model capability).
- `LanguageCatalog` already has `AutoDetectCode`/`NoTranslationCode`/`EnglishCode` sentinels and
  `GetDisplayLabel` (the "English — Native" formatter). Need a new `KeyboardLayoutCode`.
- `TranscribeViewModel` owns recording/audio only; no language surface today — Phase 2 adds it.
- `SpeechEngine` enum has just `Whisper` + `Gemma4`; both support translation, so the None state
  is forward-looking (Parakeet/mono in the prototype are illustrative).
- Highest ADR on disk is **035** → next is **036**.
- `tmp/` is **not** gitignored; it holds the prototype HTML/spec + reference screenshots (scratch,
  left uncommitted). The `plans/2026-06-01-language-settings-redesign/` design brief was untracked.

## Open Blockers

None. Plan approved-for-build; user asked to commit the planning artifacts and start the build
on autopilot.

## Documentation Status

- ADR: **pending** — `docs/decisions/036-language-ux-rebuild.md` (to be written during the build;
  triggers: new Core interface, P/Invoke, PlatformServiceExtensions entry, audio-pipeline/Whisper).
- Vault (services/architecture): **pending** — update on build (core/platform/desktop services +
  Language & Translation subsystem + decisions index for ADR-036).
- Knowledge (non-derivable facts): **pending** — capture Win32 keyboard-layout→culture quirks and
  any Avalonia popover-above-topmost-widget behaviour discovered during the build.

## Next Action

Start the build on **autopilot** following
[`plans/2026-06-08-language-ux-rebuild/implementation-plan.md`](../../plans/2026-06-08-language-ux-rebuild/implementation-plan.md),
beginning with **Phase 0** (Core: `KeyboardLayoutCode` + `TranslationForm`; new
`IKeyboardLayoutService`; Win32 impl + DI; shared `LanguageRelationshipViewModel`; pipeline
keyboard resolution) with tests, then Phase 1 (Settings page) and Phase 2 (Transcribe widget).
SQL todo board is seeded (8 todos with deps).
