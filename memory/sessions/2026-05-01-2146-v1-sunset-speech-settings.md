---
title: "Session: 2026-05-01 — V1 Sunset & Speech Settings"
type: session
status: complete
tags: [desktop-v1, sunset, speech-settings, post-processing, adr-018]
created: 2026-05-01
summary: Removed Parlotype.Desktop (V1); implemented WaitTime, punctuation, and profanity features end-to-end in V2; ported NVIDIA logging; wrote ADR-018.
---

# Session: 2026-05-01 — V1 Sunset & Speech Settings

## Active Focus
- `src/Parlotype.Core/Settings/SettingsKeys.cs` — added `WaitTime`, `AutomaticPunctuation`, `FilterProfanity` keys
- `src/Parlotype.Platform/Audio/AudioPipelineService.cs` — injected `ISettingsService`, configurable silence threshold (replaces hardcoded 8000 samples), post-processing via `TranscriptionTextProcessor`
- `src/Parlotype.Platform/Speech/TranscriptionTextProcessor.cs` — **new** post-processor: token-aware punctuation stripping + whole-word profanity masking
- `src/Parlotype.Platform/Resources/profanity-words.txt` — **new** embedded resource word list
- `src/Parlotype.Desktop.V2/ViewModels/Settings/SpeechSettingsViewModel.cs` — **new** settings section (wait time picker, punctuation toggle, profanity toggle)
- `src/Parlotype.Desktop.V2/Views/Settings/SpeechSettingsView.axaml` — **new** UI with ItemsControl + ToggleSwitches
- `src/Parlotype.Desktop.V2/App.axaml.cs` — registered `SpeechSettingsViewModel`, ported `LogNvidiaEnvironmentAsync` from V1
- `src/Parlotype.Desktop.V2/ViewModels/SettingsWindowViewModel.cs` — added Speech section (5 sections total)
- `src/Parlotype.Desktop.V2/Views/SettingsWindow.axaml` — added `SpeechSettingsView` DataTemplate
- `src/Parlotype.Tests/TranscriptionTextProcessorTests.cs` — **new** 15 tests for post-processor
- `src/Parlotype.Desktop/` — **deleted** (V1)
- `src/Parlotype.Desktop.Tests/` — **deleted** (V1 tests)
- `Parlotype.slnx` — removed V1 projects (9 → 7 projects)
- `docs/decisions/018-v1-sunset-consolidation.md` — **new** ADR

## Decisions Made
- **Settings cached at `StartAsync`** — avoids `JsonSettingsService` `SemaphoreSlim` contention on the pipeline thread; changes apply on next recording session
- **`TranscriptionTextProcessor` in Platform** (not a new Core interface) — two toggles don't warrant a new abstraction layer
- **Token-aware punctuation regex** — positional matching preserves apostrophes (`don't`), hyphens (`co-op`), decimals (`3.14`); ASCII ellipsis (`...`) handled separately from Unicode `…`
- **Whole-word profanity matching** via `\b` word boundaries — prevents Scunthorpe-style false positives (`class` contains `ass` but is not censored)
- **Profanity word list as embedded resource** — loaded once into `HashSet<string>`, compiled into regex at construction
- **Dropped `VoiceTypingLauncherEnabled`** — dead code with no defined behavior; user confirmed skip
- **V1 features were dead code** — WaitTime picker existed in UI but value was never sent to pipeline; punctuation/profanity/launcher were undeclared toggles with no bindings or persistence

## Facts Learned
- V1's `SettingsViewModel` had 4 `[ObservableProperty]` fields (`_voiceTypingLauncherEnabled`, `_automaticPunctuationEnabled`, `_filterProfanityEnabled`, `_selectedWaitTime`) that were never bound in AXAML views, never persisted to settings, and never read by any Platform service — pure dead code
- `AudioPipelineService` silence threshold was hardcoded to `8_000` samples (500ms at 16kHz) — now configurable via `WaitTimeOption`
- `TranscriptionResult.Text` is `init`-only — use `result with { Text = ... }` for post-processing
- Whisper.net 1.9.0 has `WithSuppressTokens()` for token-level filtering, but post-processing regex is more reliable for profanity (token IDs vary by model)

## Open Blockers
- None

## Documentation Status
- ADR: done — `docs/decisions/018-v1-sunset-consolidation.md`
- Vault (services/architecture): done — updated `memory/services/_index.md`, `memory/services/desktop-v2.md`, `memory/decisions/_index.md`, `memory/AGENTS.md`; removed `memory/services/desktop.md`, `memory/services/desktop-tests.md`
- Knowledge (non-derivable facts): captured in this session note; Whisper.net `WithSuppressTokens` availability is notable but not yet critical

## Next Action
- Consider renaming `Parlotype.Desktop.V2` → `Parlotype.Desktop` (cosmetic but reduces confusion; involves namespace + test project rename)
- Update `CLAUDE.md` dependency graph in the "Coding Conventions" section (still references `Desktop → Platform → Core`)
- Add a `WinUI` project reference update if applicable (ADR-014 references `Parlotype.Desktop`)
