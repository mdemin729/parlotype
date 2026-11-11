---
title: "VAD tuning lessons for Whisper"
area: "audio"
summary: "Stripping silence between VAD segments doubled Whisper WER (3.6% to 7.3%); SpeechPadMs 400, MinSilenceDurationMs 500 and a 160 ms gap are the tuned defaults."
verified: 2026-11-11
---

# VAD tuning lessons for Whisper

Benchmark-validated lessons behind the defaults in `VadOptions` (Core). The numbers are not
recorded in [ADR-008](../decisions/008-incremental-vad-processing.md) or
[ADR-011](../decisions/011-optimal-stt-pipeline-settings.md); the values themselves are in
code, the reasons are here.

> The WER figures below were carried over from a retired version of an agent definition
> (`.claude/agents/core-platform-engineer.md`, April 2026) and have not been re-measured.

## Keep silence between concatenated speech segments

Whisper relies on prosodic cues (pauses between sentences). Concatenating VAD speech segments
back to back, with the silence stripped, **doubled WER: 3.6 % → 7.3 %** (measured).
`SpeechSegmentExtractor.Extract()` (shared by `AudioPipelineService`, `BenchmarkRunner` and
`PipelineSimulator`) inserts a gap of `InterSegmentSilenceMs` between segments.
`InterSegmentSilenceMs = 160` (2,560 samples at 16 kHz) preserves enough context without an
audible gap; the gap is plain zeros.

## `SpeechPadMs` matters most

- **`SpeechPadMs = 400`** is the validated default. The initial "reasonable" 200 ms regressed
  WER on accented and paused speech; only a sweep showed 400 ms was needed.
- **`MinSilenceDurationMs = 500`** avoids splitting on natural pauses in deliberate or
  accented speech; the earlier 300 ms over-segmented.

The general lesson: do not trust a plausible-looking VAD default. Validate it with a
`sweep` over `vad.speechPadMs` / `vad.minSilenceDurationMs` / `vad.interSegmentSilenceMs`
(see `SweepExpander`).

## Use the `VadOptions` overload

`IVadService.DetectSpeech(samples)` exists for backward compatibility. Prefer
`DetectSpeech(samples, VadOptions)` so the tuned values above actually apply.
