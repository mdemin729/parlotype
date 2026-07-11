---
status: accepted
date: 2026-07-09
---

# 043. Cloud Speech Providers v1 — OpenAI-Compatible Client + xAI Grok STT

## Context

ADR-032 committed Parlotype to "Local by default. Cloud by choice." and deferred all
technical decisions. Research in `docs/research/2026-07-05-online-transcription/`
compared seven providers and recommended starting with (1) a single OpenAI-protocol
client that covers OpenAI, Groq, and any compatible host via a configurable base URL,
and (2) a dedicated xAI Grok STT client (cheapest full-featured provider, but a custom
API shape). This ADR records the v1 implementation decisions.

## Decision

### Engine model

Cloud providers are ordinary `SpeechEngine` members (`OpenAiCompatible`, `XaiGrok`)
resolved by `SpeechRecognizerFactory` exactly like the local engines — no parallel
"cloud provider" registry. Both are batch clients: the buffered utterance (16 kHz mono
float) is encoded as WAV (`WavEncoder`, extracted from `LlamaCppSpeechRecognizer`) and
POSTed as multipart/form-data; no streaming transport in v1 (push-to-talk utterances
are short; see research README finding 3).

- `OpenAiCompatibleSpeechRecognizer` → `POST {OpenAiCompatBaseUrl}/audio/transcriptions`
  with `file`/`model`/`response_format=json`/`temperature=0` (+ `language` when a
  concrete source language is selected). Defaults: `https://api.openai.com/v1`,
  `gpt-4o-mini-transcribe`. Pointing the base URL at Groq (or any compatible host) is
  the supported way to use other OpenAI-protocol providers.
- `XaiGrokSpeechRecognizer` → `POST {XaiGrokBaseUrl}/stt` with `file`/`model`/`format=json`
  (+ `language`). Defaults: `https://api.x.ai/v1`, `grok-stt`. Kept as a separate class
  (not a parameterized variant) because endpoint path, field names, and response
  fallback (`text` → `transcript`) all differ.

Shared cloud plumbing lives in two internal helpers: `CloudSpeechLanguageResolver`
(same keyboard-layout/auto-detect source policy the pipeline applies for Whisper) and
`CloudSpeechHttpError` (401/403 ⇒ "API key rejected", else status + trimmed body;
never logs the Authorization header).

Language capabilities: both engines are transcribe-only (`TranslationForm.None`).
OpenAI-compatible uses the curated Whisper language list (the models are
Whisper-family); xAI Grok uses the full catalog (no curated list).

Text post-processing (punctuation/profanity) stays centralized in
`AudioPipelineService` — cloud recognizers deliberately do not call
`TranscriptionTextProcessor` themselves.

### API key storage (`ISecretStore`)

New Core contract `ISecretStore` (get/set; null-or-empty value removes), implemented by
`DpapiSecretStore` in Platform:

- Keys live in `%LOCALAPPDATA%/parlotype/secrets.json` — deliberately **outside**
  `settings.json` so settings backups/sync/diagnostics never carry credentials.
- **Windows:** values encrypted with DPAPI (`ProtectedData`, `CurrentUser` scope). New
  dependency: `System.Security.Cryptography.ProtectedData` 10.0.9 (Platform only).
- **Non-Windows:** base64 plaintext with a one-time logged warning (OS keychain
  integration deferred). This is an OS-conditional behaviour divergence.
- Undecryptable values (file copied across machines/users) are treated as absent and
  re-prompted, never fatal.

### Brand-commitment enforcement (ADR-032)

- Local engines stay first in the engine list; Parakeet remains the recommended
  default. Cloud engines are never auto-selected.
- A recognizer with no stored API key fails initialization with an actionable message
  pointing at Settings → Speech engine — there is no anonymous/trial path (BYOK only).
- Engine card descriptions state explicitly that audio is sent to the provider; a
  persistent cloud indicator is shown while a cloud engine is active (Desktop, ADR-032
  commitment #3).

## Consequences

### Easier

- Any OpenAI-compatible host (Groq, Deepgram-compat, self-hosted whisper servers)
  works today via one setting — no code per provider.
- The engine abstraction proved out: cloud engines needed zero changes to the audio
  pipeline, factory pattern, or `ISpeechRecognizer` contract.
- Key rotation is safe: `UnloadAsync` → `InitializeAsync` re-reads settings and
  secrets, matching the existing engine-switch lifecycle.

### Harder

- Two more engines to document/test per change to the speech subsystem; enum switches
  with explicit arms (`SpeechEngineCapabilities.For`, factory) gain cases.
- The non-Windows plaintext fallback is a known gap — Linux/macOS keychain support
  needs a follow-up before those platforms are first-class cloud citizens.
- No streaming transport: unusable for future live-caption features without a second
  implementation round (accepted; research shows batch is right for dictation).

## Amendment (2026-07-10) — Not-configured error surfacing

Pressing Record with a cloud engine selected but no API key stored used to fail
silently (log-only; the button popped back to "Ready"). Now:

- The missing-key failure is a typed Core exception,
  `CloudProviderNotConfiguredException` (derives from `InvalidOperationException`,
  carries the `SpeechEngine`) — same pattern as `RuntimeUnavailableException`.
- `TranscribeViewModel.StartRecordingCoreAsync` catches it specifically, sets a
  "Cloud provider not configured" status, and shows a modal dialog with the
  provider's message and an **Open settings** action. The dialog is deliberately
  fire-and-forget so a push-to-talk key release (which awaits the in-flight start
  task per ADR-039) is never blocked on a modal.
- Confirming opens Settings deep-linked to the Cloud providers section via a new
  `SettingsSection.CloudProviders` member (`SettingsWindowViewModel.NavigateTo`).
- New reusable Desktop dialog infrastructure: `ConfirmationDialog` window +
  `IUserDialogService`/`UserDialogService` (UI-thread marshaling and owner-window
  resolution follow `ModelDownloadDialogService`).

## Amendment (2026-07-10) — Provider error surfacing

Cloud transcription failures (429 quota/rate-limit, 5xx, rejected key) previously
died in `AudioPipelineService.ProcessQueueAsync`'s catch (log-only) with no user
signal. Now:

- `CloudSpeechHttpError` parses the provider's error envelope (OpenAI
  `{"error":{"message":…,"code":…}}` and variants) instead of dumping raw JSON,
  and classifies the failure into `CloudSpeechErrorKind` (KeyRejected /
  QuotaExceeded / RateLimited / ProviderUnavailable / Other) from the HTTP status
  + error code (e.g. 429 `insufficient_quota` ⇒ QuotaExceeded vs. plain 429 ⇒
  RateLimited). It throws a typed `CloudSpeechTranscriptionException` (Core,
  derives from `InvalidOperationException`) carrying the kind, provider name, and
  a user-presentable message.
- `IAudioPipeline` gains a `TranscriptionFailed` event
  (`TranscriptionErrorEventArgs`), raised by the pipeline when a queued utterance
  fails. Recording keeps running (the pipeline is unaffected) — the event only
  informs subscribers.
- `TranscribeViewModel` subscribes and, for `CloudSpeechTranscriptionException`,
  sets a concise `StatusText` and shows a dialog: a rejected key offers "Open
  settings" (deep link to Cloud providers, reusing the not-configured flow);
  other kinds show an informational message (`IUserDialogService.ShowMessageAsync`,
  a single-button variant of the confirmation dialog). A single-flight guard
  (`_isCloudErrorDialogOpen`) prevents one dialog stacking per failed utterance.
  Non-cloud (local engine) failures keep their prior log-only behaviour.

### Out of scope (deferred)

- Azure / Google / Amazon providers (research: higher BYOK friction; add on demand).
- Key validation ping at save time; per-provider usage/cost display.
- Fallback-to-local when the cloud host is unreachable (currently surfaces the error).
- Linux/macOS secret encryption (libsecret / Keychain).
