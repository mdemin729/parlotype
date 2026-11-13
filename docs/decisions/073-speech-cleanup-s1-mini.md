---
status: accepted
date: 2026-11-13
amends: [027]
---

# 073. Speech cleanup with a local model (S1-mini)

## Context

Recognizers return what was said: filler words, false starts, "Friday, no wait, Thursday",
"twenty five dollars". Turning that into text someone would write is the feature competing
dictation apps lead with, and most of them send the transcript to a cloud model to do it.

A [review of the experimental Gemma 4 engine](../research/2026-11-13-gemma4-engine-review/review.md)
concluded that the engine itself is not worth developing, but that the `llama-server`
subsystem under it is: [ADR-027](027-llamaserver-namespace-rescope.md) had already made the
catalog, installer, registry and settings workload-agnostic in anticipation of a text
post-processor. This ADR adds that post-processor.

The model is **S1-mini by Superwhisper**: a 0.6B fine-tune of Qwen3-0.6B built only to
normalize speech-to-text output, 462 MB at Q4_K_M, Apache 2.0 plus a clause that the name is
kept. It is not a chat model. It takes a fixed system prompt and a control line with three
trained axes (styling, structure, context) and nothing else.
[Measured on a 16-core desktop](../research/2026-11-13-gemma4-engine-review/s1-mini-measurements.md)
it loads in under a second and cleans an ordinary dictation in 0.2–0.6 s on the CPU alone.
It covers English only.

## Decision

**Speech cleanup is an opt-in step that runs after any speech engine and before the text is
pasted.** It is off by default.

- **Core (`Parlotype.Core.TextProcessing`).** `ISpeechCleanupService` (readiness, prepare,
  clean, unload); `SpeechCleanupOptions` with the enums `CleanupStyling`, `CleanupStructure`
  and `CleanupContext`; `SpeechCleanupModelInfo`, a catalog whose one entry is S1-mini;
  `S1MiniPrompt`, the published input format verbatim; `SpeechCleanupGate`; and
  `SpeechCleanupSettings` over four new settings keys (`SpeechCleanupEnabled`, `…Styling`,
  `…Structure`, `…Context`). `LlamaServerVariantPicker` joins `Core.LlamaServer`.
- **Platform.** `LlamaServerSpeechCleanupService` starts a `llama-server` child process with
  the model on first use and talks to it over `/v1/chat/completions` on loopback.
  `SpeechCleanupModelDownloadService` fetches the GGUF from `huggingface.co` and verifies its
  SHA-256. Both are registered in `PlatformServiceExtensions.cs`.
- **Pipeline.** `AudioPipelineService` calls the service in the transcription stage, before
  `TranscriptionTextProcessor`: stripping punctuation or masking profanity is the user's last
  word on the text, and the model would put the punctuation back. The model starts loading
  when a recording starts, so it is up by the time the user stops speaking.
- **Desktop.** A Speech cleanup page under Speech engine, visible for every engine.

### Cleanup may cost polish, never the dictation

The raw text is pasted, with nothing shown to the user, whenever cleanup cannot be trusted:

- the model or the server is missing, fails to start, returns an error, or takes longer than
  20 s;
- `SpeechCleanupGate.ShouldClean` rejects the input: empty, over 6000 characters (it would
  not fit the context with its own rewrite), or not plausibly English;
- `SpeechCleanupGate.IsPlausibleResult` rejects the output because it outgrew its input.
  Cleanup removes words; text that grows is the model looping. Measured: a sentence repeated
  six times made S1-mini loop until `max_tokens`, and this check caught it.

An empty result is valid and means the utterance was only filler; nothing is pasted.

"Plausibly English" is a script test, not language identification: text in another script,
or dense with diacritics, is skipped. A sentence in another Latin-script language that
happens to carry few diacritics passes and comes back damaged. The page says the feature is
English-only and to turn it off for other languages; the gate is a second line, not a
guarantee.

### One switch is the whole setup

The Gemma 4 engine asks the user to pick a model on one page and a server build on another,
and fails silently at the hotkey if either is missing. Cleanup does not repeat that. Turning
the switch on fetches whatever is missing, each download behind the existing confirm-and-
progress dialog, and only then saves the setting:

1. the model, if it is not on disk;
2. a `llama-server`: the active install if its executable exists, otherwise a managed install
   already on disk, otherwise the newest **CPU** build from the release list.

If a step is declined or fails, the switch returns to off, so the feature is never on in a
state where it cannot work. CPU is chosen deliberately: the model is small enough for it, a
CPU build is a few tens of megabytes with no driver or CUDA version to match, and it starts
on every machine. Someone who wants GPU inference installs that build on the llama.cpp server
page, which appears in the navigation once cleanup is on.

### Each consumer runs its own `llama-server` process

This amends ADR-027, which expected the post-processor to share one spawned process with the
speech recognizer through a `LlamaServerHost`. A `llama-server` hosts one model, and the two
consumers need different ones, so there is nothing to share but the installed binary.

- The cleanup server gets a free loopback port from the OS at each launch. There is no port
  setting, no conflict to resolve and no adoption: nothing outside the service talks to it.
- `ILlamaCppServerLifecycle` is now a `CompositeLlamaServerLifecycle` over both consumers, so
  the installer and the Data page stop every process holding the executable before touching
  it.
- `LlamaServerPathResolver` holds the "which executable" rule for both. `LlamaServerProcess`
  holds spawn, health polling and stop for the cleanup service; the Gemma 4 recognizer keeps
  its own copy of that logic, because the engine is expected to be removed rather than
  refactored.

### Server flags that are not free choices

- Thinking is disabled with `--jinja --chat-template-kwargs {"enable_thinking":false}`, and
  again per request. The template is Qwen3's and thinks by default; the model card reports
  that `--reasoning-budget 0` degrades the output.
- `temperature: 0` is sent on every request. The GGUF's metadata carries Qwen3's 0.6 and
  llama.cpp defaults to 0.8; the model is trained for greedy decoding.
- `-np 1`. The server divides the context between its slots and defaults to several.
- No `-ngl`: the default uses a GPU when the build has one.

### Privacy

No text leaves the machine. The server's stdout and stderr are copied to the app log only
until it is healthy; once requests carry dictated text the output is drained and discarded.
An HTTP error is logged by status code, never by body, which can quote the request
([ADR-046](046-security-hardening-batch.md)).

### Network

No new host and nothing on the app's own initiative. Enabling cleanup can download the model
from `huggingface.co` and a server build from `github.com`, and reads the llama.cpp release
list from `api.github.com` when no install is on disk. Each follows the user turning the
switch on, and each download asks first. [`network-requests.md`](../network-requests.md)
lists them.

## Consequences

**Easier**

- Any engine, including the default CPU-only Parakeet, gets cleaned-up output with no cloud
  service and no GPU.
- A second cleanup model is a catalog entry plus, if its input format differs, a second
  prompt builder beside `S1MiniPrompt`. The service, the gate, the pipeline hook and the
  setup flow do not change.

**Harder / trade-offs**

- English only. Parlotype ships in 25 interface languages and its default engine recognizes
  25; for most of them this feature does nothing. A multilingual model is the obvious next
  catalog entry.
- A second child process to manage, and about half a gigabyte of memory while cleanup is on.
  It is stopped at exit and when the switch goes off, but a crash of the app can orphan it,
  as it can the Gemma 4 sidecar.
- Cleanup adds its latency to every dictation: measured 0.2–0.6 s on a desktop CPU, 2.4 s
  for an 860-character dictation. Slower CPUs were not measured.
- When cleanup falls back to the raw text the user is not told why. The page shows a
  "not working" status when the model or the server has gone missing, but a runtime failure
  is visible only in the log.
- Cleanup has no hotkey of its own: it is on for every dictation or for none.
- `--chat-template-kwargs` for `enable_thinking` prints a deprecation warning in llama.cpp
  b10330. See the [knowledge note](../knowledge/s1-mini-quirks.md).
- The Gemma 4 recognizer and the cleanup service duplicate process-management code until the
  engine is removed.

## Verification

- `dotnet build Parlotype.slnx`: zero warnings. `dotnet test`: all projects pass.
- New tests: `SpeechCleanupCoreTests` (input format, gate, settings, catalog, build picker),
  `LlamaServerSpeechCleanupServiceTests` (readiness, command line, wire format),
  `AudioPipelineSpeechCleanupTests` (cleaned text, every fallback, nothing logged),
  `SpeechCleanupSettingsViewModelTests` (the setup flow and each way it stops),
  `SpeechCleanupScreenshotTests`.
- End to end: the real services downloaded S1-mini, verified its SHA-256, started
  llama.cpp b10330 and cleaned the model card's examples with the published results. The
  numbers are in the measurements note. The Settings page was not exercised in a running app.
