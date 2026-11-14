# Research

Each folder here is a dated snapshot: research, surveys or measurements written at the time
a decision was being made. They are never updated afterwards. Prices, versions, benchmark
numbers and descriptions of the code reflect the folder's date, not today. For the current
design, read [`docs/architecture/`](../architecture/) and the ADRs in
[`docs/decisions/`](../decisions/).

New research goes in a new folder named `YYYY-MM-DD-kebab-topic/`: lowercase, no spaces,
no double extensions.

| Folder | What it is | Fed |
|---|---|---|
| [2026-02-12-initial-landscape](2026-02-12-initial-landscape/) | Two early surveys of local voice-to-text architecture and the market | No single ADR. Background for [ADR-002](../decisions/002-solution-architecture.md), [ADR-003](../decisions/003-audio-pipeline.md), [ADR-006](../decisions/006-text-injection-clipboard-vs-sharphook.md) |
| [2026-02-12-product-name](2026-02-12-product-name/) | Product name deliberation ("Parlotype") | The product name. Cited by [ADR-032](../decisions/032-online-speech-providers-positioning.md) |
| [2026-03-01-audio-pipeline-review](2026-03-01-audio-pipeline-review/) | Read-only review of the February 2026 audio pipeline | [ADR-045](../decisions/045-audio-pipeline-allocation-threading.md) |
| [2026-03-04-benchmark-requirements](2026-03-04-benchmark-requirements/) | Requirements for the benchmark CLI | [ADR-009](../decisions/009-benchmark-cli-design.md) |
| [2026-05-09-gemma4-runtimes](2026-05-09-gemma4-runtimes/) | Two deep-research reports (Claude, Gemini) on running Gemma 4 audio locally | [ADR-025](../decisions/025-gemma4-llamacpp-desktop.md) |
| [2026-05-23-librispeech-cuda-comparison](2026-05-23-librispeech-cuda-comparison/) | Whisper vs Gemma 4 on LibriSpeech test-other, CUDA everywhere | No ADR. Benchmark evidence; the Whisper CUDA runtime was later removed by [ADR-049](../decisions/049-drop-whisper-cuda-runtime.md) |
| [2026-07-05-online-transcription](2026-07-05-online-transcription/) | Survey of cloud speech-to-text providers | [ADR-043](../decisions/043-cloud-speech-providers-v1.md) |
| [2026-07-06-parakeet-v3-engine](2026-07-06-parakeet-v3-engine/) | Parakeet TDT v3 via sherpa-onnx as a speech engine | [ADR-041](../decisions/041-parakeet-v3-sherpa-onnx.md) |
| [2026-07-11-audio-pipeline-perf-security](2026-07-11-audio-pipeline-perf-security/) | Pipeline allocation and threading research, micro-benchmark results | [ADR-044](../decisions/044-microbenchmark-project.md), [ADR-045](../decisions/045-audio-pipeline-allocation-threading.md) |
| [2026-07-30-dictation-hotkey-bindings](2026-07-30-dictation-hotkey-bindings/) | Hold, double-tap and chord gestures for dictation | [ADR-047](../decisions/047-multi-binding-dictation-hotkeys.md) |
| [2026-08-18-hold-scoped-push-to-talk](2026-08-18-hold-scoped-push-to-talk/) | Per-engine long-utterance measurements | [ADR-060](../decisions/060-hold-scoped-push-to-talk.md) |
| [2026-09-13-transcribe-window-auto-hide](2026-09-13-transcribe-window-auto-hide/) | When the dictation widget should hide itself | [ADR-068](../decisions/068-transcribe-window-auto-hide.md) |
| [2026-11-13-gemma4-engine-review](2026-11-13-gemma4-engine-review/) | Review of the experimental Gemma 4 engine and the uses of a local model in a dictation app | [ADR-073](../decisions/073-speech-cleanup-s1-mini.md) |
