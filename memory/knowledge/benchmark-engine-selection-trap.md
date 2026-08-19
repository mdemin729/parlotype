---
title: Benchmark Engine Selection Trap
type: knowledge
tags: [benchmark, parakeet, whisper, di, gotchas]
created: 2026-08-18
last_updated: 2026-08-18
summary: Whisper benchmark configs silently ran whatever engine settings.json selected, because the Whisper branch resolved ISpeechRecognizer through DelegatingSpeechRecognizer
---

# Benchmark Engine Selection Trap

`Parlotype.Benchmark/Program.cs` picks the recognizer per engine. The Parakeet and
llama.cpp branches resolve their **concrete** types and override `ISettingsService`
with an `InMemorySettingsService` seeded from the benchmark config. The Whisper
branch did neither — it resolved `ISpeechRecognizer`, which is
`DelegatingSpeechRecognizer`, which asks `SpeechRecognizerFactory` for the engine
named in `SpeechEngine`… read from the **user's real `settings.json`**.

So on any machine whose app engine was not Whisper, a Whisper benchmark config
silently benchmarked that other engine instead. Since Parakeet became the default
([[decisions/_index|ADR-041]], 2026-07), that is the normal case — a config saying
`"whisper": { "model": "Base" }` produced Parakeet numbers under a Whisper label.

The tell is a stack trace naming the wrong recognizer, or an implausible
metric jump between runs of the "same" config. It is otherwise silent: no warning,
and the run summary still prints the Whisper model name from the config.

Fixed 2026-08-18 — the Whisper branch now pins
`SpeechEngine`/`SelectedWhisperModel`/`RuntimePreference` in an
`InMemorySettingsService` and resolves `WhisperSpeechRecognizer` directly, matching
the other two branches.

**Consequence for old data:** Whisper results in `results/` produced between
ADR-041 and this fix are suspect and should be re-run before being cited. See
`plans/2026-08-18-hold-scoped-push-to-talk/research.md`.

**General lesson:** any benchmark or test that resolves `ISpeechRecognizer` rather
than a concrete recognizer inherits the user's settings. Related:
[[sherpa-onnx-quirks]], [[benchmark-pipeline-recommendations]].
