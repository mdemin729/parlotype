---
title: Whisper.net Model Load Blocks the Calling Thread
type: knowledge
tags: [whisper, threading, avalonia, ui-thread, loading]
created: 2026-06-27
last_updated: 2026-06-27
summary: WhisperFactory.FromPath + processor Build are synchronous/CPU-bound despite living in an async method; run them in Task.Run or they freeze the UI thread (and any DispatcherTimer-driven loading animation)
---

# Whisper.net Model Load Blocks the Calling Thread

## Fact

`WhisperFactory.FromPath(modelPath)` and the `WhisperProcessor` build
(`builder.Build()`) in Whisper.net are **synchronous, CPU-bound** calls. They sit
inside `ISpeechRecognizer.InitializeAsync`, but `InitializeAsync` being `async`
does *not* make them non-blocking — without an explicit `Task.Run`, they execute
on the calling thread.

When `InitializeAsync` is reached from the Avalonia UI thread (e.g. the record
button's `AsyncRelayCommand`), the model load **freezes the UI thread** for the
whole load. The symptom that exposed this: the on-button loading spinner
(`WaveformView`, driven by a `DispatcherTimer` on the UI thread) did not animate
while a Whisper model loaded — but it *did* animate for Gemma 4, because the
llama.cpp engine loads via an out-of-process llama-server and never touches the
UI thread.

## Fix (ADR-038)

Both `WhisperSpeechRecognizer.InitializeAsync` overloads wrap the heavy
synchronous block (`WhisperRuntimeBootstrap.Initialize` + `WhisperFactory.FromPath`
+ builder `Build()`) in `Task.Run(...).ConfigureAwait(false)`. Field assignments
(`_factory`, `_processor`, `_currentOptions`, `IsReady`) are published after the
awaited `Task.Run` completes (its await provides the memory barrier), so they are
safe to read on the resuming thread.

## Why it matters

Any future work that calls speech-recognizer init from the UI thread, or adds an
on-UI loading animation, must keep the heavy load off the UI thread or the
animation will stutter/freeze. The model download (`EnsureModelAsync`) is already
genuinely async I/O; only the factory/processor construction is the blocking part.

## Related

- [[asyncrelaycommand-flicker]] — the same record button's command also disables
  while running
- [[../decisions/_index|ADR-038]] — prewarm + deferred loading spinner
