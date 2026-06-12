---
status: accepted
date: 2026-06-11
---

# 036. Language UX Rebuild — keyboard source, target forms, shared relationship model

## Context

ADR-035 unified translation control on the Language page as a
`[Source] → [Target]` row with inline-expanding pickers. A subsequent hi-fi
prototype (plans/2026-06-08-language-ux-rebuild, answering the
2026-06-01 design brief) pushed the UX further and exposed gaps:

1. **No keyboard-layout source.** Most users speak the language they type; the
   OS keyboard layout is a better default signal than forcing auto-detection or
   an explicit pick.
2. **Engine asymmetry still leaked.** Whisper (one fixed target) rendered the
   same full target picker as Gemma (arbitrary targets), and there was no state
   at all for a future transcribe-only engine.
3. **No glanceable relationship.** The arrow didn't change glyph, there was no
   plain-language summary, and engine/model switches silently invalidated
   selections.
4. **The Transcribe window had no language surface**, so checking or flipping
   translation meant opening Settings.
5. The inline-expand pickers pushed page content around instead of overlaying it.

## Decision

### Domain (Core)

1. **`LanguageCatalog.KeyboardLayoutCode` (`"keyboard"`) sentinel** +
   `IsKeyboardLayout()`. Unlike auto-detect, blank codes never mean keyboard —
   it is an explicit opt-in. The sentinel persists as-is under
   `SelectedSourceLanguage` and never enters MRU lists.
2. **`TranslationForm` enum (`None | Toggle | Full`)** derived on
   `LanguageCapabilities`: arbitrary translation ⇒ `Full`; exactly one fixed
   target ⇒ `Toggle`; otherwise `None`. The `None` path is built now,
   capability-driven, even though no shipping engine triggers it (confirmed
   scope decision) — `SpeechEngineCapabilities`' fallback branch documents the
   future transcribe-only shape.
3. **`IKeyboardLayoutService`** (Core) → `KeyboardLayoutInfo? Detect()` with
   `(LanguageCode, FriendlyName)`. Never throws; null means "unavailable".
4. **`SourceLanguageResolver`** — pure resolution of the keyboard sentinel to
   the detected layout language with auto-detect fallback (detection missing,
   or detected language absent from an optional supported list). One tested
   policy for both pipeline call sites.

### Platform

5. **`Win32KeyboardLayoutService`** — P/Invoke `GetForegroundWindow` →
   `GetWindowThreadProcessId` → `GetKeyboardLayout(threadId)`. Keyboard layouts
   are per-thread on Windows and Parlotype is a background tray app, so the
   foreground window's thread holds the layout the user is typing with. The
   HKL low word (LANGID) maps through `CultureInfo.GetCultureInfo(int)` to an
   ISO code + English display name; transient/custom LANGIDs without culture
   data degrade to null. **`NoOpKeyboardLayoutService`** registers on
   non-Windows (same OS-conditional pattern as the GPU environment providers).
6. **Pipeline resolution.** `AudioPipelineService.CacheSettingsAsync` resolves
   `source == keyboard` via the resolver (validated against the Whisper
   language set) before building `WhisperOptions`;
   `LlamaCppSpeechRecognizer.BuildPromptTextAsync` does the same for the Gemma
   prompt (unrestricted — the LLM accepts any language).

### Desktop

7. **Shared `LanguageRelationshipViewModel`** (DI singleton) — the single
   source of truth for the source → target relationship, consumed by the
   Settings page *and* the Transcribe window so the surfaces never drift
   (spec §7 state machine). Owns: engine capabilities, source state
   (keyboard / auto / explicit), target `{on, code}` with the resting target
   preserved across off→on (restore without re-asking), per-role MRU,
   persistence, keyboard-layout detection state, and all shared derivations
   (connector state/glyph, form booleans, summary sentence, `TranslationSwitch`
   two-way adapter, toggle/none strings).
8. **Fallback-on-switch with toasts (spec §8).** When an engine switch
   invalidates a selection: unsupported source → keyboard layout; form `None`
   → translation off; `Toggle` with a different resting target → forced to the
   single option (silent when translation is off — the change isn't visible);
   `Full` with an unknown target → reset to default. Each user-visible fallback
   raises a one-line toast that auto-clears (~4 s, UI-thread dispatched).
   Startup reconciliation applies the same corrections silently.
9. **Floating popover pickers.** `LanguagePickerView` is popover content
   (search auto-focus, Escape/light-dismiss, empty state naming the query)
   hosted in `Popup`s; chrome lives in a shared `Border.popoverChrome` app
   style (page 300 px, widget flyout 268 px). Rows carry icon tiles
   (upper-cased language code; ⌨ / ✦ / ⊘ for specials), native subnames, and
   group headers `Recent` / `All languages`. Search and grouping appear only
   for lists longer than 8 entries.
10. **Model-driven target side.** Toggle ⇒ labelled `ToggleSwitch`; Full ⇒
    picker field + popover with a pinned "Off — no translation" row (picking a
    language also turns translation on); None ⇒ disabled card + amber note
    naming the model + locked connector (`=` at 50 %).
11. **Transcribe quick picker.** A strip under the record button
    (`source chip · connector · target chip`); the connector toggles
    translation in one click; chips open a flyout above the widget that leads
    with the target control (reusing the shared picker) and shows the source
    as a read-only row routing to Settings. While translation is off the
    target chip mirrors the source ("Auto = Auto" reads "typed as spoken").
    `TranscribeViewModel` owns recording-stop on `RelationshipChanged`
    (replacing the page VM's former direct dependency on it).

## Consequences

**Easier**
- Both surfaces render one mental model from one VM; adding a third surface
  (e.g. a tray flyout) is wiring, not reimplementation.
- New engines get correct UI for free by declaring capabilities — including
  transcribe-only engines (form `None`).
- Selections survive engine switches with explanations instead of silent loss.

**Harder / explicit trade-offs**
- Keyboard detection is Windows-only; macOS/Linux degrade to auto-detect via
  the null path (confirmed scope decision).
- The `None` form ships untriggerable by real engines; it is exercised in
  tests via an out-of-range `SpeechEngine` value.
- Headless window capture cannot see the popup layer, so popover screenshots
  render the picker content directly rather than the open popup.
- `LanguageRelationshipViewModel` is dense (state machine + derivations), but
  it replaces equivalent logic previously smeared across the page VM.

## References

- ADR-021 — Whisper translation to English
- ADR-033 — Translation capability per Whisper model
- ADR-034 — Source & target language selection (data model, unchanged)
- **ADR-035 — superseded by this ADR** (data model + migration remain)
- plans/2026-06-08-language-ux-rebuild — spec, requirements, phased plan
