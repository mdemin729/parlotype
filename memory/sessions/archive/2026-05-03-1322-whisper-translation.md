---
title: "Session: 2026-05-03 — Whisper translation & topmost window"
type: session
status: complete
tags: [whisper, translation, settings, topmost, focus]
created: 2026-05-03
summary: "Added Whisper translation-to-English setting; fixed recognizer reinitialization; made TranscribeWindow topmost without focus stealing"
---

# Session: 2026-05-03 — Whisper Translation & Topmost Window

## Active Focus

### Topmost window (committed)
- `src/Parlotype.Desktop/Views/TranscribeWindow.axaml` — `Topmost="True"`
- `src/Parlotype.Desktop/Views/ModelDownloadDialog.axaml` — `Topmost="True"` (code review catch)
- `src/Parlotype.Desktop/Services/IWindowManager.cs` — `ShowTranscribe(bool activate = true)`
- `src/Parlotype.Desktop/Services/WindowManager.cs` — conditional `ShowActivated` / `Activate()`
- `src/Parlotype.Desktop/Services/HotkeyCoordinator.cs` — `ShowTranscribe(activate: false)`

### Translation feature (uncommitted)
- `src/Parlotype.Core/Settings/SettingsKeys.cs` — added `TranslateToEnglish`
- `src/Parlotype.Core/Speech/WhisperOptions.cs` — added `TranslateToEnglish` property
- `src/Parlotype.Platform/Speech/WhisperSpeechRecognizer.cs` — conditional `WithTranslate()`, tracks `_currentOptions` for reinit on change, restored `WithLanguage(options.Language)` (was hardcoded to "ru")
- `src/Parlotype.Platform/Audio/AudioPipelineService.cs` — builds `WhisperOptions` from settings (model, translate, runtime) in `CacheSettingsAsync()`, passes to `InitializeAsync(WhisperOptions)`
- `src/Parlotype.Desktop/ViewModels/Settings/SpeechSettingsViewModel.cs` — `TranslateToEnglishEnabled` toggle
- `src/Parlotype.Desktop/Views/Settings/SpeechSettingsView.axaml` — translation toggle in Speech settings UI

## Decisions Made
- **ADR-021**: Translation to English via settings — `TranslateToEnglish` on `WhisperOptions`, conditional `WithTranslate()`, settings-driven pipeline, recognizer reinit on options change
- `WhisperSpeechRecognizer` uses record value equality (`WhisperOptions` is `sealed record`) to detect options changes and only reloads the model when necessary
- `AudioPipelineService` always builds `WhisperOptions` from settings now, eliminating drift between no-args and options init paths
- `RuntimePreference` is included in `WhisperOptions` built by the pipeline to avoid regression (rubber-duck catch)

## Facts Learned
- **Whisper translation only works with multilingual models** (Medium, Large). English-only models (*En) don't support it. Base/Small produce mixed results. Captured in `memory/knowledge/whisper-translation-models.md`.
- **`WhisperSpeechRecognizer.InitializeAsync(WhisperOptions)` `IsReady` guard prevented reinitialization** — if the model was already loaded (e.g. from a previous recording via no-args path), new options were silently ignored. Fixed by tracking `_currentOptions` and unloading when options change.
- Avalonia `Window.ShowActivated = false` prevents `Show()` from stealing focus — standard API.

## Open Blockers
- None

## Documentation Status
- ADR: done — `docs/decisions/021-whisper-translation-to-english.md`
- Vault (services/architecture): done — updated `memory/services/desktop.md`, `memory/decisions/_index.md`
- Knowledge (non-derivable facts): done — `memory/knowledge/whisper-translation-models.md` + index row

## Next Action
- Commit the translation feature changes
- Test translation end-to-end: enable toggle in Settings → Speech, speak Russian, verify English output
- Consider disabling translation toggle for English-only models (`*En`) in the UI
- Consider consolidating the two `InitializeAsync` overloads to share a private builder method (eliminates future drift)
