# ADR-042: Parakeet as Default Engine & Capability-Driven Language UI Visibility

- **Status:** Accepted
- **Date:** 2026-07-07
- **Deciders:** Maksim

## Context

ADR-041 added Parakeet TDT v3 as a third engine but kept Whisper as the
default and left the language UI unchanged. Two problems surfaced:

1. **Misleading language UI.** Parakeet always auto-detects — it has no
   language-forcing parameter and no translation task. Showing a source picker
   with Spanish/Russian/German etc. suggested a choice the engine silently
   ignores, and the Transcribe widget carried a quick-picker strip that could
   do nothing.
2. **Default engine.** In real use Parakeet outperformed Whisper on this
   hardware: comparable accuracy, faster decode, lower resource usage, and no
   GPU requirement. That is the better out-of-box experience.

## Decision

### 1. Language UI hides entirely for engines with no language choice

- `LanguageCapabilities` gains `SupportsSourceSelection` (false for Parakeet —
  its `SupportedSourceLanguages` now only documents coverage) and a derived
  `HasLanguageChoices` (`SupportsSourceSelection || TranslationForm != None`).
- `SettingsSectionViewModelBase` gains `virtual bool IsVisibleFor(SpeechEngine)`
  (default: the existing `RestrictToEngine` rule). The Language page overrides
  it with `HasLanguageChoices`, so it disappears while Parakeet is active —
  chosen over showing a documentation-only page (option 1 in the discussion)
  to keep the settings surface minimal.
- The Transcribe widget's quick-picker strip binds to the same flag
  (`TranscribeViewModel.HasLanguageStrip`) and the frameless window (ADR-040)
  **compacts from 118 px to 88 px** when the strip is hidden — the window is
  fixed-size, so the height switches explicitly in code-behind.
- `LanguageRelationshipViewModel.ApplyEngine` **skips all spec-§8 fallbacks**
  when the new engine has no language choices: nothing is shown, so nothing
  needs correcting — and deliberately nothing is persisted, so a user's
  Whisper/Gemma source + translation setup survives a round trip through
  Parakeet (unlike the None-form fallback, which flips `TranslationEnabled`
  off with a toast for engines that *do* show language UI).

### 2. Parakeet is the default engine

- All unset-setting fallbacks switch from `Whisper` to `Parakeet`:
  `SpeechRecognizerFactory`, `SpeechEngineSettingsViewModel`,
  `LanguageRelationshipViewModel.InitializeAsync`. Existing installations are
  unaffected — an explicit `SpeechEngine=Whisper` in settings.json still wins.
- The engine list reorders to Parakeet ("Recommended") / Whisper / Gemma 4.
- `ParakeetSpeechRecognizer` now downloads the ~670 MB model on first use via
  a new `IParakeetModelProvider` Core contract (optional ctor dependency) — a
  default engine must not throw "download it first in Settings" on the first
  record press. The Platform implementation (`ParakeetModelDownloadService`)
  ensures headlessly for benchmark/CLI; the Desktop app re-registers the
  interface with `ParakeetModelDownloadDialogService` (last-wins DI, same
  trick as `ILlamaServerInstaller`), so the first record press opens the
  **shared model-download dialog with a progress bar and Cancel** — the exact
  pattern Whisper uses through `IModelDownloadService` /
  `ModelDownloadDialogService`. Decline/cancel surfaces as
  `OperationCanceledException` and the recording start aborts cleanly.

## Consequences

### Positive
- First-run experience: fastest engine, no GPU needed, model fetched on demand
  with visible progress and the option to cancel
- No dead UI: every visible language control now does something
- Default Transcribe widget is 30 px shorter (88 px), matching its actual content
- Engine round-trips no longer clobber stored language preferences

### Negative
- First record press on a fresh install interposes a consent dialog + ~670 MB
  download before anything is transcribed
- Users who want Whisper's 99-language coverage or translation must switch
  engines in Settings — the language features are invisible until they do
- `SettingsSectionViewModelBase` now has two visibility mechanisms
  (`RestrictToEngine` and `IsVisibleFor`); the latter subsumes the former and
  new sections should prefer it

## Related

- ADR-041: Parakeet TDT v3 via sherpa-onnx (supersedes its "Whisper remains
  default" consequence)
- ADR-036: Language UX rebuild (spec-§8 fallbacks now conditional on
  `HasLanguageChoices`)
- ADR-040: Frameless compact Transcribe window (heights 118/88)
- ADR-038: Loading spinner covers the first-use model download
