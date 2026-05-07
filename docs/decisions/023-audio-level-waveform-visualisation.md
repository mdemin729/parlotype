# ADR-023: Audio-Level Provider and Waveform Visualisation

## Status

Accepted

## Context

The Transcribe window's record button showed a static microphone icon that toggled a CSS class on record. Users need visual feedback distinguishing three states:

1. **Disabled** — speech recognition off (microphone icon)
2. **Idle** — recording active, user silent (breathing bars at rest)
3. **Active** — recording active, speech detected (audio-reactive animated bars)

The existing audio pipeline already captures 16 kHz mono float samples via WASAPI and processes them through VAD and Whisper. Real-time amplitude data was not exposed to the UI layer.

## Decision

### Core: `IAudioLevelProvider` interface

A new `IAudioLevelProvider` interface in `Parlotype.Core.Audio` exposes:
- `float CurrentLevel` — latest RMS amplitude (0.0–1.0)
- `event EventHandler<AudioLevelEventArgs> LevelChanged` — fired on each audio chunk

A `RecordingState` enum (`Disabled`, `Idle`, `Active`) defines the visual states.

### Platform: RMS computation in `AudioPipelineService`

`AudioPipelineService` now also implements `IAudioLevelProvider`. On each `OnAudioDataAvailable` callback, it computes RMS of the incoming float samples and fires `LevelChanged`. The computation is O(n) with no allocations and runs outside the sample-buffer lock.

DI forwards `IAudioLevelProvider` to the same `AudioPipelineService` singleton via `sp.GetRequiredService<IAudioPipeline>()` cast.

### Desktop: `WaveformView` custom control

A `WaveformView` Avalonia `Control` renders with `DrawingContext`:
- **Disabled**: microphone icon via `StreamGeometry`
- **Idle**: 13 vertical bars with gentle sine-wave breathing animation (white on blue background)
- **Active**: 13 bars with decorative multi-frequency sine wave animation (white on blue background, amplitude 0.6 default)

A `DispatcherTimer` at ~60 fps drives animation, attached/detached with the visual tree. Bars are white; the recording button background is blue `#378ADD` when recording. Theme-aware brushes are resolved via `TryFindResource` with hardcoded fallbacks.

### ViewModel: state machine in `TranscribeViewModel`

- Pipeline start → `RecordingState.Idle`
- `LevelChanged` event → dispatched to UI thread → EMA-smoothed RMS (attack 0.4 / decay 0.05) compared against 0.005 threshold
- Smoothed RMS above threshold → `RecordingState.Active` with timestamp
- Smoothed RMS below threshold → holds `Active` for 1200ms hold-off before transitioning to `Idle`
- Pipeline stop → `RecordingState.Disabled`, `AudioLevel = 0`, smoothed RMS reset

## Consequences

- Audio amplitude data is available to any future UI consumer via `IAudioLevelProvider`
- The 60 fps timer only runs while the control is in the visual tree — no resource leak
- EMA smoothing prevents jitter from noisy per-chunk RMS values; quiet speech gradually ramps up above threshold
- The 1200ms hold-off accommodates natural pauses between words without dropping to Idle mid-sentence
- The control uses `StreamGeometry` for the mic icon rather than an SVG dependency
