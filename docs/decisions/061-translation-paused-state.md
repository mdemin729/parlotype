---
status: accepted
date: 2026-09-05
---

# 061. Translation Paused Is a First-Class UI State

## Context

[ADR-033](033-translation-model-capability.md) established the capability truth
(`WhisperModelInfo.SupportsTranslation`), the authoritative enforcement point
(`AudioPipelineService` gates the effective flag), and the rule that matters most:
**user intent is preserved** — selecting a model that can't translate never overwrites
the `TranslationEnabled` preference.

Its UI half did not survive [ADR-036](036-language-ux-rebuild.md). The language UX was
rebuilt around a source → connector → target relationship shared by the Settings →
Language page and the Transcribe window strip, and the model-capability flag was carried
over as a single derived boolean (`ShowTranslationPausedNote`) bound to one accent-coloured
line on the Settings page. Everything else kept rendering a working translation:

| Surface | Rendered with Medium (English) + translation on |
|---|---|
| Transcribe strip (always visible) | `English → English`, accent connector — no signal at all |
| Transcribe flyout | switch on, no note (the amber note was bound to `IsNoneForm` only) |
| Language page connector | `ConnectorState.On`, accent `→` |
| Language page summary | "You speak Russian → Parlotype types **English**." — false |
| Whisper model list | grey 50 %-opacity "no translation", no stated consequence |

So the page contradicted itself — connector, switch and summary all claimed a translation
that the pipeline would not perform — and the compact widget, the surface a user actually
watches while dictating, said nothing. The original ADR-033 symptom ("enabled translation,
saw it silently do nothing") had returned by a different route.

The obvious fix — making `SpeechEngineCapabilities.For` model-aware so a non-translating
model collapses the engine to `TranslationForm.None` — was rejected: `ApplyEngine` forces
`TranslationEnabled = false` on the None form, which destroys the preserved intent that is
the point of ADR-033.

## Decision

**Model-blocked translation is a distinct, reversible state rendered on every surface**,
never a silent no-op and never a forced-off preference.

1. **`ConnectorState.Paused`** joins `On` / `Off` / `Locked` in
   `LanguageRelationshipViewModel`. It is separate from `Locked` because the cause differs
   and so does the cure: `Locked` means the *engine* can't translate (change engine),
   `Paused` means *this model* can't (change model). Derived from
   `IsTranslationPaused` = toggle form + translation on + `!WhisperModelSupportsTranslation`,
   replacing `ShowTranslationPausedNote`.
2. **Every derived string tells the truth.** `SummaryText` reads "You speak Russian →
   Parlotype types Russian (translation paused)"; `ConnectorGlyph` is `=`, not `→`;
   `TranscribeViewModel.TargetShort` mirrors the source, so the strip states what will
   actually be typed. The paused connector and note render in the warn (amber) palette —
   never the accent blue used for a live translation.
3. **The preference stays on and stays operable.** The toggle is not disabled and not
   flipped; the target card gains a "Paused — this model can't translate" sub-line beside
   the still-on switch. Picking a multilingual model resumes translation with no further
   input. (ADR-033 disabled the toggle; that is superseded — a disabled control cannot
   express intent for later.)
4. **The state names its cause and offers its cure.** `LanguageRelationshipViewModel` now
   holds the active `WhisperModel`, so `TranslationPausedNote` and `ConnectorTooltip` name
   the model ("Large v3 Turbo"). Both the Language page banner and the Transcribe flyout
   carry a **"Choose a model that translates"** button routing to
   `SettingsSection.EngineModel`.
5. **The pause is announced when it is caused.** `SetWhisperModel` raises the existing
   fallback toast when a switch newly pauses a live translation. Startup reconciliation is
   silent — the inline state carries it, and a toast on every launch would be noise.
6. **Supporting changes.** The model list's "no translation" hint became a bordered amber
   badge with a tooltip; `SettingsSectionViewModelBase` gained a `NavigationRequested` event
   so a section can ask the shell to navigate without reaching for a sibling section.
7. **The warn palette stays per-view, and the paused chrome uses literal colours.** The
   obvious tidy-up — hoisting the two amber brushes into `Application.Resources` now that a
   third surface needs them — was implemented, then reverted: `Parlotype.Desktop.Tests`
   hosts controls under its own `TestApp`, so an application-scoped dictionary resolves to
   nothing under the headless renderer and every amber state silently rendered as plain
   text while all assertions still passed. The same run showed `DynamicResource` inside a
   `Styles` setter not reaching the view-local dictionary either — the paused connector
   drew fully transparent. So each view declares the palette (as two already did), direct
   property bindings use `{DynamicResource}`, and the class-based paused chrome uses a
   literal `#C68A28` fill with a white glyph, exactly as the existing `.on` style uses a
   literal `#378ADD`. `WarnPaletteResourceTests` pins the palette to all three surfaces in
   both variants so the hoist cannot be re-attempted silently.

8. **Connector colour styles target the glyph's own class, never a bare descendant
   `TextBlock`.** A tooltip's content is a logical child of the control it hangs off, so
   `Button.stripConnector.paused TextBlock { Foreground: White }` painted the *tooltip's*
   text white on its light background too — an unreadable tooltip, reported against the
   first build of this change. The pre-existing `.on` styles had the same latent defect and
   are fixed with it. `ConnectorTooltipStyleTests` pins both states on both surfaces, plus
   the record button (whose white-on-accent recording style sets `Foreground` on the button
   itself and does *not* leak, since the tooltip's popup root does not inherit it).

The Core capability data and the `AudioPipelineService` gate are unchanged — this ADR is
entirely about making the UI agree with them.

## Consequences

### Easier
- The three surfaces are derived from one predicate, so they cannot drift again the way
  ADR-033's UI did through ADR-036.
- The user is told the cause (named model) and handed the cure (one button) at the moment
  of the change, on the page, and in the always-visible widget.
- Cross-section navigation from a settings section is now a supported, general mechanism.

### Harder
- `LanguageRelationshipViewModel` carries Whisper-specific state (`WhisperModel`) even
  though it is engine-agnostic elsewhere; the paused state is deliberately scoped to
  `TranslationForm.Toggle` so it cannot leak into full-form engines.
- Four connector states now need styling in two AXAML files, and the warn palette is
  declared in three views rather than one — deliberate duplication, guarded by a test.

### Risks
- `SetWhisperModel` is wired from `SettingsWindowViewModel`'s subscription to the model
  section plus one call at startup. Any future path that changes
  `SettingsKeys.SelectedWhisperModel` without going through that section would leave the
  paused state stale until the next launch.
- The paused connector shares the `=` glyph with the off state; the distinction is carried
  by colour and tooltip. Verified in both themes via the screenshot scenarios, but it is a
  colour-only distinction at strip size.
