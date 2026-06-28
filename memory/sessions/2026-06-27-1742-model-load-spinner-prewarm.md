---
title: "Session: 2026-06-27 — Model load spinner & opt-in prewarm"
type: session
status: active
tags: [transcribe, loading-spinner, prewarm, whisper, ui-thread, settings]
created: 2026-06-27
summary: "Added a loading spinner to the record button, off-UI-thread Whisper load, background model prewarm made opt-in via a new Engine-section settings toggle"
---

# Session: 2026-06-27 — Model load spinner & opt-in prewarm

## Active Focus
Record-button UX while the speech model loads, plus startup model prewarm. Files touched:
- **Core:** `Audio/RecordingState.cs` (new `Loading` value), `Audio/IAudioPipeline.cs`
  (default `PrewarmAsync`), `Settings/SettingsKeys.cs` (new `PrewarmModelOnStartup`).
- **Platform:** `Audio/AudioPipelineService.cs` (`PrewarmAsync`, `EnsureModelInitializedAsync`,
  `SemaphoreSlim _initLock` + drain-on-dispose), `Speech/WhisperSpeechRecognizer.cs`
  (both `InitializeAsync` overloads wrap the synchronous load in `Task.Run`).
- **Desktop:** `Views/WaveformView.cs` (rotating arc spinner), `ViewModels/TranscribeViewModel.cs`
  (`IsLoading`, deferred-spinner via `LoadingSpinnerDelay`, `PrewarmAsync`),
  `Views/TranscribeWindow.axaml` (`Button.loading` style), `App.axaml.cs` (gated background
  prewarm), `ViewModels/Settings/SpeechEngineSettingsViewModel.cs` +
  `Views/Settings/SpeechEngineSettingsView.axaml` (opt-in toggle).
- **Tests:** `TranscribeViewModelTests` (loading/prewarm + hot/cold spinner),
  `SpeechEngineSettingsViewModelTests` (toggle load/persist/default), `Mocks/MockAudioPipeline.cs`.

## Decisions Made
- New `RecordingState.Loading` + spinner in `WaveformView`; `Button.loading` reuses the blue
  recording chrome so the white spinner reads as "working".
- `IAudioPipeline.PrewarmAsync` is a **default-interface no-op** so mocks/other impls need no change.
- Whisper's synchronous CPU-bound load runs in `Task.Run(...).ConfigureAwait(false)` to keep the
  UI thread free (so the `DispatcherTimer` spinner animates).
- **Deferred spinner** (`LoadingSpinnerDelay`, default 200 ms, races `StartAsync` vs `Task.Delay`)
  chosen over an `IsReady` check — time-based is correct for *all* slow-load causes (cold model,
  changed options, slow disk) and avoids the one-frame icon flicker on a hot model.
- Prewarm is **opt-in, off by default** (`SettingsKeys.PrewarmModelOnStartup`); toggle lives in the
  always-visible **Engine** settings section and only persists (effect next launch — no immediate warm).
- `DisposeAsync` drains an in-flight prewarm via `await _initLock.WaitAsync(5s)` before disposing
  the semaphore (fix from code review — avoids a Release-after-Dispose `ObjectDisposedException`).

## Facts Learned
- `WhisperFactory.FromPath` + processor `Build()` are synchronous/CPU-bound and block the calling
  thread despite the async method → distilled to [[whisper-ui-thread-loading]].
- Gemma 4 loads out-of-process (llama-server), so it never blocked the UI thread — which is why
  the spinner froze only for Whisper.

## Open Blockers
- None.

## Documentation Status
- ADR: done — `docs/decisions/038-speech-model-prewarm-and-loading-state.md` (covers spinner,
  off-UI-thread load, deferred spinner, opt-in prewarm).
- Vault (services/architecture): done — `services/core.md`, `services/platform.md`,
  `services/desktop.md`, `architecture/subsystems.md`, `decisions/_index.md`.
- Knowledge (non-derivable facts): done — `memory/knowledge/whisper-ui-thread-loading.md` + index row.

## Next Action
Feature complete and committed. If revisited: consider whether prewarm should warm only Whisper
(skip pre-starting the Gemma 4 llama-server sidecar) even when the toggle is on, and whether to
expose `LoadingSpinnerDelay` as a setting rather than a code default.
