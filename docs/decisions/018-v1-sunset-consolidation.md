---
status: accepted
date: 2026-05-01
---

# 018. Sunset Parlotype.Desktop (V1) — Consolidate on V2

## Context

ADR-015 introduced `Parlotype.Desktop.V2` (Avalonia 12, tray-based UX) alongside the original `Parlotype.Desktop` (Avalonia 11, floating toolbar). Both frontends shared `Parlotype.Core` and `Parlotype.Platform`. Maintaining two desktop frontends created sync overhead with no user benefit — V2's architecture (per-section ViewModels, tray-first lifetime, multi-window) is strictly superior.

Before sunsetting V1, a feature-parity audit was performed. V1 had four "features" that were declared as ViewModel properties but never wired to the pipeline or persisted:
- `WaitTimeOption` (silence detection timeout) — UI picker existed but value was never sent to the audio pipeline
- `AutomaticPunctuationEnabled` — dead toggle, never read by any service
- `FilterProfanityEnabled` — dead toggle, never read by any service
- `VoiceTypingLauncherEnabled` — dead toggle with no defined behavior

Additionally, V1 performed NVIDIA/CUDA environment detection logging on startup, which V2 lacked.

## Decision

1. **Implement the three viable features end-to-end** in V2 before removing V1:
   - **Wait time (silence detection):** Added settings key `WaitTime` to `SettingsKeys`. `AudioPipelineService` now reads `WaitTimeOption` from settings at `StartAsync` and uses it as a configurable silence threshold (replacing the hardcoded 8000-sample / 500ms constant). New `SpeechSettingsViewModel` + `SpeechSettingsView` provide a picker in the V2 Settings window.
   - **Automatic punctuation:** Added settings key `AutomaticPunctuation`. When disabled, `TranscriptionTextProcessor` strips sentence-level punctuation (periods, commas, semicolons, etc.) from Whisper output while preserving intra-word punctuation (apostrophes, hyphens, decimal points).
   - **Profanity filter:** Added settings key `FilterProfanity`. When enabled, `TranscriptionTextProcessor` masks profanity with asterisks using whole-word, case-insensitive matching from an embedded word list (`Resources/profanity-words.txt`). Word-boundary matching prevents false positives (e.g., "class" is not censored despite containing "ass").

2. **Port NVIDIA logging** to V2's `App.axaml.cs`.

3. **Drop `VoiceTypingLauncherEnabled`** — no defined behavior, no implementation path.

4. **Remove `Parlotype.Desktop` and `Parlotype.Desktop.Tests`** from the solution and filesystem.

5. **Keep `Parlotype.Desktop.V2` naming** for now — a future rename to `Parlotype.Desktop` can be done when convenient but is cosmetic.

### Key implementation choices:

- **Settings cached at `StartAsync`**, not read per-transcription — avoids file I/O contention from `JsonSettingsService`'s `SemaphoreSlim` on the pipeline thread. Changes take effect on the next recording session.
- **`TranscriptionTextProcessor`** lives in `Parlotype.Platform.Speech` as a self-contained class, not a Core interface — two toggles don't warrant a new abstraction layer.
- **Profanity word list** stored as an embedded resource in Platform, loaded once into a `HashSet<string>` — easy to maintain, testable.
- **Sentence punctuation regex** uses positional matching (`(?<=\s|^)` and `(?=\s|$)`) plus explicit ASCII-ellipsis handling (`\.{2,}`) to avoid corrupting intra-word punctuation like apostrophes and decimal points.

## Consequences

**Positive:**
- Solution drops from 9 projects to 7, eliminating sync overhead.
- Three features now work end-to-end (wait time, punctuation, profanity) with settings persistence and UI.
- NVIDIA diagnostic logging is available in V2.
- 15 new unit tests cover the text post-processor.

**Negative:**
- Users of V1 must switch to `dotnet run --project src/Parlotype.Desktop.V2`.
- The `.V2` suffix in the project name is now a misnomer (it's the only desktop frontend), but renaming has downstream cost (namespace changes, test project rename) and can be deferred.

## References

- `src/Parlotype.Core/Settings/SettingsKeys.cs` — new keys: `WaitTime`, `AutomaticPunctuation`, `FilterProfanity`
- `src/Parlotype.Platform/Audio/AudioPipelineService.cs` — configurable silence threshold, post-processing
- `src/Parlotype.Platform/Speech/TranscriptionTextProcessor.cs` — punctuation + profanity post-processor
- `src/Parlotype.Platform/Resources/profanity-words.txt` — embedded word list
- `src/Parlotype.Desktop.V2/ViewModels/Settings/SpeechSettingsViewModel.cs` — new settings section
- `src/Parlotype.Desktop.V2/Views/Settings/SpeechSettingsView.axaml` — new settings UI
- ADR-015 (superseded in part — V1 coexistence no longer applies)
