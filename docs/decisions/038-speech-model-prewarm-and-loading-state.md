# ADR-038: Speech-Model Prewarm and On-Button Loading State

## Status

Accepted

## Context

The first time the user pressed the record button, `TranscribeViewModel.StartRecordingAsync` awaited `IAudioPipeline.StartAsync`, which performs a **cold load** of the speech model (Whisper `WhisperFactory.FromPath` / the Gemma 4 llama-server sidecar). Two problems resulted:

1. **The button appeared unresponsive.** `TogglePlayCommand` is an async `[RelayCommand]`; CommunityToolkit.Mvvm disables it (`CanExecute == false`) for the whole await, so the button could not be re-pressed while the model loaded.
2. **No visual feedback.** `WaveformView` only rendered `Disabled`/`Idle`/`Active`. During loading the state stayed `Disabled`, so the button looked exactly like an idle, untouched control. Only the secondary `StatusText` changed to "Loading model…".

## Decision

### A. On-button loading state (visual feedback)

- Added `RecordingState.Loading` to `Parlotype.Core.Audio.RecordingState` (between `Disabled` and `Idle`).
- `WaveformView` renders a rotating arc spinner for `Loading`, driven by the existing 16 ms animation timer / shared `_phase`.
- **The spinner is deferred.** `TranscribeViewModel.StartRecordingAsync` races `StartAsync` against `Task.Delay(LoadingSpinnerDelay)` (default 200 ms): the `Loading` state is entered only when the load outlasts the threshold. A hot (already-loaded) model starts almost instantly, so the spinner never appears and the button icon does not flash for a single frame. This time-based check is correct for *every* slow-load cause (cold model, changed model/runtime options, slow disk) — unlike a static `IsReady` check, which can't predict an options-triggered reload.
- **The spinner only animates if the UI thread is free.** `WhisperSpeechRecognizer.InitializeAsync` performs synchronous, CPU-bound work (`WhisperRuntimeBootstrap.Initialize`, `WhisperFactory.FromPath`, `builder.Build()`); left on the calling (UI) thread it froze the spinner. That heavy block is now wrapped in `Task.Run(...).ConfigureAwait(false)` so the model loads off the UI thread. The Gemma 4 (`LlamaCppSpeechRecognizer`) path loads via an out-of-process llama-server and never blocked the UI.
- `TranscribeViewModel` sets `RecordingState = Loading` before awaiting `StartAsync` and exposes an `IsLoading` observable. `TranscribeWindow.axaml` applies a `Button.loading` style (same blue chrome as `recording`) so the white spinner reads as "working".

### B. Background model prewarm (root-cause fix)

- Added `Task PrewarmAsync(CancellationToken)` to `IAudioPipeline` as a **default interface method** (`=> Task.CompletedTask`), so existing implementations/mocks need no change.
- `AudioPipelineService` implements `PrewarmAsync` by snapshotting settings and loading the model **without starting audio capture**. Initialisation (settings cache + `recognizer.InitializeAsync`) was extracted into `EnsureModelInitializedAsync`, guarded by a new `SemaphoreSlim _initLock` so a background prewarm and an interactive `StartAsync` never load the model — or mutate cached `WhisperOptions` — concurrently. The recognizer's `InitializeAsync(options)` is idempotent on matching options, so the heavy load happens once.
- `TranscribeViewModel.PrewarmAsync` delegates to the pipeline, silently (failures are logged, not surfaced).
- `App.OnFrameworkInitializationCompleted` kicks off `PrewarmSpeechModelAsync` on a background task after startup. **Prewarm is opt-in (off by default):** `PrewarmSpeechModelAsync` reads the `SettingsKeys.PrewarmModelOnStartup` flag and returns early unless it is `true`, so a fresh install never pre-loads. When enabled the model is typically ready before the first press; the on-button spinner remains the safety net for genuine cold starts (prewarm disabled, prewarm still running, or model/runtime changed).
- **Settings toggle:** the flag is surfaced as a `ToggleSwitch` ("Preload model on startup" / "Load model on first use") in the always-visible **Engine** section (`SpeechEngineSettingsViewModel.PreloadModelOnStartupEnabled` + `SpeechEngineSettingsView`). It only persists the preference — it takes effect on the **next launch** (no immediate warm), keeping the toggle side-effect-free.

## Consequences

- First record press is instant **when prewarm is enabled**; otherwise (the default) the first press shows the deferred spinner during the one-time load. Either way the button never looks dead.
- Prewarm is **opt-in, off by default**, so a fresh install spends no startup resources and never auto-starts the Gemma 4 sidecar. When the user enables it, prewarm pre-loads the **configured** engine — for Gemma 4 this pre-starts the llama-server sidecar at launch (extra idle memory), an explicit trade the user opts into for a fast first press.
- Changing the Whisper model or runtime after prewarm causes a normal reload on the next `StartAsync` (the spinner covers it). Runtime selection remains a process-global one-shot (ADR-012, ADR-022).
- `_initLock` adds a small serialisation point around initialisation; it is released before audio capture and transcription, so steady-state throughput is unaffected.
