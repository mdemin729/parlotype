---
title: "Session: 2026-09-05 — Translation paused is a first-class UI state"
type: session
status: complete
tags: [language-ux, whisper, translation, avalonia, adr-061]
created: 2026-09-05
summary: "A Whisper model that can't translate now says so on every surface — amber connector, truthful summary, model-named banner with a one-click fix — with the user's preference left on and reversible"
---

# Session: 2026-09-05

## Active Focus

- `src/Parlotype.Desktop/ViewModels/LanguageRelationshipViewModel.cs` — `ConnectorState.Paused`,
  `IsTranslationPaused` (replaces `ShowTranslationPausedNote`), `WhisperModel` +
  `WhisperModelDisplayName`, `ConnectorTooltip`, `TranslationPausedNote`, toast in `SetWhisperModel`
- `src/Parlotype.Desktop/ViewModels/TranscribeViewModel.cs` — `TargetShort` mirrors while paused,
  `GoToModelSettingsCommand`
- `src/Parlotype.Desktop/ViewModels/Settings/SettingsSectionViewModelBase.cs` — new
  `NavigationRequested` / `RequestNavigation`, wired in `SettingsWindowViewModel`
- `Views/Settings/LanguageSelectionSettingsView.axaml`, `Views/TranscribeWindow.axaml`,
  `Views/Settings/WhisperModelSettingsView.axaml`
- Tests: `LanguageRelationshipViewModelTests`, `TranscribeViewModelTests`, new
  `WarnPaletteResourceTests`, screenshot scenarios in `SpeechSettingsScreenshotTests` +
  `TranscribeWindowScreenshotTests`

## Decisions Made

- **Paused is a state, not a note.** ADR-033's capability data and pipeline gate were intact;
  only its UI half was missing, reduced by the ADR-036 rebuild to one boolean bound to a single
  accent line. New `ConnectorState.Paused` drives the connector, the summary, the target card,
  the strip chip and the flyout from one predicate. See [[decisions/_index|ADR-061]].
- **`Paused` kept distinct from `Locked`** — cause and cure differ: locked = the engine can't
  translate (change engine), paused = this model can't (change model).
- **Rejected making `SpeechEngineCapabilities.For` model-aware** (so a non-translating model
  collapses to `TranslationForm.None`). It is the tidier change and reuses all the existing
  none-form rendering, but `ApplyEngine` force-disables translation on that form, destroying
  the preserved intent that is ADR-033's central rule.
- **The toggle stays on and operable**, superseding ADR-033's disable-the-toggle point: a
  disabled control cannot express intent for later, and greyed-but-checked was the very
  contradiction ADR-033 was trying to avoid.
- **Toast on the model switch that causes the pause; silent at startup.** The inline state
  carries the startup case; a toast on every launch would be noise.
- **Warn palette stays per-view with literal colours in class styles** — see Facts Learned;
  the app-scoped hoist was implemented and reverted on evidence.

## Facts Learned

- `Parlotype.Desktop.Tests` runs under its own `TestApp`, so `App.axaml`'s
  `Application.Resources` resolve to **null** in every headless test — colours blank out with
  no error and all 414 tests still passed. Separately, `DynamicResource` in a `Styles` setter
  does not reach the view's own `Resources`, so the styled control drew fully transparent
  (worse than no style at all). Captured in [[knowledge/avalonia-resource-scope-in-headless-tests]].
- **The green test run proved nothing about the pixels.** The regression was found by
  extracting the base64 PNGs out of `reports/*.html` and sampling the connector's region:
  4200 px of pure `(0,0,0)` where the working `on` state showed `(55,138,221)`. Subpixel
  antialiasing also produces convincing fake amber — `(239,191,111)` and its exact reverse
  `(111,191,239)` appearing together is ClearType fringing on white text, not a colour.
- ADR-033's Consequences claimed "the toggle can no longer silently fail" — accurate when
  written, untrue within two ADRs. Same shape as the ADR-047 → ADR-057 case last month: an
  accepted consequence is not a verified one, and a later UX rebuild is exactly where the
  earlier ADR's UI half goes to die. Both ADR-033 passages are now annotated as amended.
- `SettingsSection.EngineModel` already resolved to the active engine's model page (ADR-056),
  so the "Choose a model that translates" action needed no new deep link — only a way for a
  section to ask the shell to navigate.

## Follow-up within the session: invisible tooltip

The user reported the connector's tooltip rendering as unreadable light-on-light. Cause was
mine and one line wide: `Button.stripConnector.paused TextBlock { Foreground: White }` — a
tooltip's content is a **logical child of the control it hangs off**, so the descendant
selector painted the tooltip's own text with the glyph's colour. The pre-existing `.on`
styles (shipped since ADR-036) carried the identical latent defect for their
"Toggle translation" tip.

Reproduced first (`ConnectorTooltipStyleTests` hangs a probe `TextBlock` off the connector
as its tip, opens it, asserts the foreground): 2 of 2 red before the fix. Fixed by giving
the glyph an explicit `connectorGlyph` class and scoping both `.on` and `.paused` selectors
to it — 5 of 5 green after, covering both states on both surfaces.

Two things learned while doing it:

- **Setting `Foreground` on the button itself does not leak.** Probed the record button's
  white-on-accent recording style: its hotkey tooltip stays readable, because the tooltip's
  popup root does not inherit the property. Only *selector matching* crosses over. Pinned
  as a test so it stays true.
- **A tooltip left open by a test keeps its timer alive past that test**, surfacing as
  `InvalidOperationException: Cannot get KeyValueStorage on the idle test context` charged
  to whichever test ran next — i.e. it looks like flakiness somewhere unrelated. Close
  tooltips in a `finally`.

Also checked the amber pill's hover state, since the report's screenshot showed it greyish:
with `:pointerover` active the background stays `#ffc68a28` (probed directly), so Fluent's
pointerover setter does not override the class style. Nothing to fix there.

## Open Blockers

- None. Not exercised against a real microphone: the pipeline gate
  (`AudioPipelineService:843`) is unchanged by this session, so the paused UI now reports what
  that gate already did.
- `SetWhisperModel` is driven only by `SettingsWindowViewModel`'s model-section subscription
  plus one startup call. A future writer of `SettingsKeys.SelectedWhisperModel` that bypasses
  that section would leave the paused state stale until relaunch (recorded as an ADR-061 risk).

## Documentation Status

- ADR: done — `docs/decisions/061-translation-paused-state.md`; ADR-033 §4 and its Consequences
  bullet annotated as amended
- Vault (services/architecture): done — `services/desktop.md`, `architecture/subsystems.md`,
  `decisions/_index.md`; plan folder `plans/2026-09-05-translation-paused-state/` + `INDEX.md`
- Knowledge: done — `memory/knowledge/avalonia-resource-scope-in-headless-tests.md` (three
  scope surprises: app-scoped resources absent under `TestApp`, `DynamicResource` dead in
  `Styles` setters, descendant selectors leaking into tooltip content) + index row

## Next Action

Run the app against a non-translating model end to end: Settings → Whisper model → Large v3
Turbo with translation on, confirm the toast fires on selection, the strip turns amber, and
"Choose a model that translates" lands on the model page from both the flyout and the Language
page. Then dictate once and confirm the output is untranslated — matching what the UI now
claims. Worth also deciding whether the same paused treatment should cover the *cloud* engines,
whose `LanguageCapabilities` report `TranslationForm.None` today.
