# Microsoft Azure — AI Speech (Speech to Text)

**Date:** 2026-07-05 · **API style:** custom (Speech SDK + multiple REST APIs) — **not** OpenAI-compatible

## Summary

Azure AI Speech is the broadest (and most fragmented) offering surveyed. There is no
single endpoint; instead there are **four distinct entry points**, each with its own
protocol:

| Entry point | Protocol | Mode | Typical use |
|---|---|---|---|
| **Real-time Speech to text** | Speech SDK (proprietary WebSocket protocol) or REST-for-short-audio (≤ 60 s) | Live streaming, interim results | Dictation, captions, voice UI |
| **Fast transcription API** | REST `POST /speechtotext/transcriptions:transcribe` | Synchronous batch, *faster than real-time* | "Transcript ASAP with predictable latency" |
| **Batch transcription API** | REST `POST /speechtotext/transcriptions:submit` | Asynchronous batch from URLs / Azure Blob containers | Bulk archives, call centers |
| **Speech Transcription SDK** (newer) | SDK wrapping LLM Speech + Fast Transcription | Near-real-time and file-based | Higher-level transcription apps |

The current GA REST API version is `2025-10-15` (v3.x REST APIs were retired March 2026 —
any older integration guides are stale).

## Features

- **Diarization** — up to 35 speakers (fast + batch; errors beyond 35).
- **Word-level timestamps** — `offsetMilliseconds`/`durationMilliseconds` per word.
- **Language identification** — automatic locale detection, plus a dedicated
  multilingual model that transcribes mixed-language audio without locale codes.
- **Multichannel** — transcribe stereo channels independently or merged.
- **Phrase lists** — runtime vocabulary biasing (analogue of Whisper's initial prompt).
- **Custom speech** — trainable custom models for domain accuracy (unique among the
  surveyed providers).
- **Whisper on Azure** — OpenAI's Whisper is offered as an alternate model in batch
  transcription and through Azure OpenAI, giving a partially OpenAI-shaped path inside
  Azure.
- 140+ locales for standard models.

## Fast Transcription API (most relevant to Parlotype)

- `POST {endpoint}/speechtotext/transcriptions:transcribe?api-version=2025-10-15`,
  multipart form: audio file + JSON `definition` (locales, diarization, channels…).
- Input: audio < 5 h and < 500 MB — WAV, MP3, OPUS/OGG, FLAC, WMA, AAC, ALAW/MULAW,
  AMR, WebM, SPEEX.
- Synchronous JSON response with `combinedPhrases` (full text) plus per-phrase words,
  offsets, durations, locale — returns much faster than the audio's real-time length.
- Auth: `Ocp-Apim-Subscription-Key: <resource key>` or Entra ID bearer token.

## Real-time path

The Speech SDK (`Microsoft.CognitiveServices.Speech` NuGet — first-class C# support,
Windows/Linux/macOS) streams microphone or push-stream audio over Microsoft's WebSocket
protocol and raises `Recognizing` (interim) / `Recognized` (final) events. This is the
only surveyed provider with a **mature official .NET streaming SDK**, though it brings a
native binary dependency per platform.

## Differences vs. OpenAI-style APIs

- Requires provisioning an **Azure Speech resource** (region + key) — BYOK is "key +
  region/endpoint", one step more than a bare API key.
- Response schema is Microsoft's own (`combinedPhrases`, `phrases[].words[]`), not
  `{ "text": … }`.
- Capability king: diarization scale, custom models, phrase lists, multichannel — most
  of which dictation doesn't need.

## Pricing (indicative, July 2026 — verify)

- Standard real-time / fast transcription ≈ $1/hr pay-as-you-go; free tier (F0) exists
  with 5 audio-hours/month. Commitment tiers reduce cost.

## Fit for Parlotype

- **Best-fit mode:** Fast Transcription REST — a single multipart POST per utterance,
  synchronous result, word timestamps for free. Avoids the Speech SDK native binaries.
- **BYOK:** acceptable — user pastes resource key + region. More setup than
  OpenAI/Groq/xAI (must create an Azure resource) but far less than Google.
- **Watch out:** REST API version churn (v3.x already retired); pin `api-version` and
  expect migrations.

## Sources

- [Speech to text REST API — Microsoft Learn](https://learn.microsoft.com/azure/ai-services/speech-service/rest-speech-to-text)
- [Use the fast transcription API — Microsoft Learn](https://learn.microsoft.com/azure/ai-services/speech-service/fast-transcription-create)
- [What is speech to text? — Microsoft Learn](https://learn.microsoft.com/azure/ai-services/speech-service/speech-to-text)
- [Batch transcription overview — Microsoft Learn](https://learn.microsoft.com/azure/ai-services/speech-service/batch-transcription)
- [Speech Transcription SDK — Microsoft Learn](https://learn.microsoft.com/azure/ai-services/speech-service/transcription-sdk)
