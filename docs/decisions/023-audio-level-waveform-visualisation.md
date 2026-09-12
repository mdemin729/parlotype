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

#### Smooth state transitions

Since `WaveformView` uses `DrawingContext` rendering (not AXAML properties), Avalonia's built-in `Transitions` cannot be used for bar heights. Instead, a `_activeBlend` factor (0.0 = idle, 1.0 = active) is animated per frame at 0.06 per tick (~300ms full transition at 60 fps). Every frame computes both idle and active bar heights and lerps between them:

```
barH = idleH + (activeH - idleH) * _activeBlend
```

Phase speed also interpolates between the idle rate (0.015) and active rate (0.06), so the animation tempo ramps smoothly alongside the bar heights.

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

## Amendment — 2026-09-11: continuous audio response and quiet rest

The original renderer substituted amplitude `0.6` whenever RMS fell below `0.01`,
creating a discontinuity at that boundary. Combined with slow RMS decay and a
1200ms state hold, this could keep a large decorative wave moving after speech.

- `WaveformAnimation` now maps RMS through a continuous logarithmic response,
  with a 65ms attack / 85ms release envelope and 45ms smoothing of each bar.
  These are exponential time constants. Elapsed monotonic time replaces
  frame-count increments; short integration steps keep the response consistent
  at different frame rates. There is no fallback amplitude.
- Seventeen fine rounded bars form a tapered silhouette with softer edges.
  Positive sine lobes avoid the sharp corners introduced by absolute values.
  Silence settles into stationary dots; it has no breathing animation.
- The ViewModel uses raw RMS hysteresis (0.005 to enter, 0.0035 to sustain),
  with a 180ms hold measured from the last above-threshold input. A 40ms timer
  expires that hold even if capture delivers no further callbacks. Below-threshold
  input immediately targets zero visual amplitude while the state hold bridges
  short pauses. Visual smoothing lives in the renderer, independently of state.
- Queued levels from stopped/previous recordings and stale callbacks are ignored.
  Stopping or loading resets the animation envelope.

Regression tests cover threshold continuity, onset, settling, frame-rate independence,
missing callbacks, queued events across stop/restart, and real-control rendering in
both themes. `PARLOTYPE_WAVEFORM_PREVIEW_DIRECTORY` optionally saves a six-second
sequence from the headless rendering test for animation review.
