# Online Speech Transcription Providers — Research Overview

**Date:** 2026-07-05
**Status:** Research
**Related:** [ADR-032 — Online Speech Providers: Brand & Positioning](../../decisions/032-online-speech-providers-positioning.md)

## Purpose

Parlotype plans to add opt-in cloud speech providers alongside the local Whisper.net and
Gemma 4 engines ("Local by default. Cloud by choice." — ADR-032). This research surveys
the transcription APIs of the candidate providers, comparing API style
(OpenAI-compatible vs. custom), features, and fit for Parlotype's push-to-talk dictation
use case.

One naming clarification up front: **Grok** (xAI's model/API brand) and **Groq** (the
LPU inference company that hosts open-source Whisper models) are different companies.
ADR-032 mentions Groq; the task brief mentions Grok. As of April 2026, xAI ships a real
standalone Grok Speech-to-Text API, so **both** are researched here in separate documents.

## Documents

| Provider | Document | API style |
|---|---|---|
| OpenAI | [openai.md](openai.md) | The de-facto standard others copy |
| Google Cloud | [google-cloud-speech.md](google-cloud-speech.md) | Custom (gRPC/REST, Speech-to-Text V2) |
| Microsoft Azure | [azure-speech.md](azure-speech.md) | Custom (Speech SDK + several REST APIs) |
| xAI (Grok) | [xai-grok.md](xai-grok.md) | Custom (REST + WebSocket) |
| Groq | [groq.md](groq.md) | OpenAI-compatible |
| Amazon | [amazon-transcribe.md](amazon-transcribe.md) | Custom (SigV4 + S3 batch / event-stream streaming) |

## Comparison at a Glance

| Capability | OpenAI | Google Cloud | Azure Speech | xAI Grok | Groq | Amazon |
|---|---|---|---|---|---|---|
| **API style** | Multipart REST (`/v1/audio/transcriptions`) + Realtime WS/WebRTC | Custom gRPC + REST (V2), recognizer resources | Speech SDK (WS-based) + Fast/Batch REST | Custom REST (`/v1/stt`) + WS | OpenAI-compatible multipart REST | Custom AWS API (SigV4, S3 batch, event-stream streaming) |
| **OpenAI-compatible** | — (is the reference) | No | No (Azure OpenAI variant partially) | No (chat API is; STT is not) | **Yes** | No |
| **Batch (file upload)** | Yes, ≤ 25 MB | Yes (sync < 1 min; BatchRecognize via GCS for long audio) | Yes (Fast transcription: sync, faster than real-time; Batch: async) | Yes, ≤ 500 MB | Yes, ≤ 25 MB free / 100 MB dev tier | Async only — file must be in S3, poll for job result (**no sync upload endpoint**) |
| **Streaming (live audio)** | Yes (Realtime API, WS/WebRTC, 24 kHz PCM) | Yes (gRPC `StreamingRecognize`) | Yes (Speech SDK, interim results) | Yes (WS, binary frames, interim/final events) | **No** (but ~200x real-time batch) | Yes (HTTP/2 or WS, partial-results stabilization) |
| **Word timestamps** | whisper-1 only (`verbose_json`) | Utterance-level (streaming); word-level limited on Chirp 3 | Yes | Yes | Yes (`verbose_json`) | Yes (default, + confidence) |
| **Diarization** | `gpt-4o-transcribe-diarize` | Yes (batch/sync only, 14 languages) | Yes (up to 35 speakers) | Yes | No | Yes (+ 2-channel ID) |
| **Language auto-detect** | Yes | Yes (language-agnostic Chirp 3) | Yes (language ID) | Yes (25+ languages) | Yes (Whisper multilingual) | Yes (100+ languages batch) |
| **Prompt/vocabulary biasing** | Prompt (gpt-4o models) | Speech adaptation + custom prompts | Phrase lists, custom speech models | Not documented | Prompt (224 tokens) | Custom vocabularies + custom language models |
| **Official .NET SDK** | Yes (`OpenAI` NuGet) | Yes (`Google.Cloud.Speech.V2`) | Yes (`Microsoft.CognitiveServices.Speech`) | No (plain HTTP/WS) | No (OpenAI SDK with base-URL override works) | Yes (`AWSSDK.TranscribeStreaming`, effectively mandatory) |
| **Auth model** | Bearer API key | Service account / OAuth (API key limited) | Resource key or Entra ID | Bearer API key | Bearer API key | IAM key pair + SigV4 signing |
| **Indicative price** * | $0.003–0.006/min ($0.18–0.36/hr) | ~$0.96/hr (V2 standard) | ~$1/hr (standard real-time) | $0.10/hr batch, $0.20/hr streaming | $0.04–0.111/hr | $0.024/min (~$1.44/hr), 15 s min/request |

\* Pricing as of July 2026; verify before implementation — all providers change pricing frequently.

## Key Findings

1. **Two API families exist.** The "OpenAI-compatible" family (OpenAI itself, Groq, and
   many smaller hosts) uses a single multipart `POST /v1/audio/transcriptions` with
   `file`, `model`, `language`, `prompt`, `response_format`, `temperature`. The
   "hyperscaler custom" family (Google, Azure, xAI) each has its own request/response
   schema, auth model, and separate streaming protocol.

2. **Amazon is the structural outlier.** Amazon Transcribe has no synchronous
   file-upload endpoint at all: batch means "put the file in S3, start a job, poll" —
   unusable for dictation latency — so the only viable AWS path for Parlotype is the
   streaming API via the `AWSSDK.TranscribeStreaming` SDK (SigV4 signing makes
   hand-rolled HTTP impractical). It also carries the weakest privacy default (audio
   may be retained for model improvement unless opted out at the AWS-organization
   level) and the highest standard price (~$1.44/hr).

3. **One OpenAI-compatible client covers multiple providers.** An
   `ISpeechRecognizer` implementation speaking the OpenAI multipart protocol with a
   configurable base URL + API key covers OpenAI **and** Groq (and Deepgram/Fireworks/
   local servers exposing the same shape) with near-zero extra code. This is the highest
   leverage first implementation.

4. **Parlotype's use case is short-utterance batch, not streaming.** Push-to-talk
   dictation records a few seconds of audio, then wants a transcript as fast as possible.
   A simple HTTPS POST of a WAV buffer fits this perfectly; live streaming protocols
   (WebSocket/WebRTC/gRPC) add substantial complexity for marginal latency benefit at
   utterance lengths under ~30 s. Streaming becomes interesting only if Parlotype later
   adds live-caption-style incremental injection.

5. **Latency ranking for short clips (expected):** Groq (fastest inference, ~200x
   real-time) ≈ xAI Grok ("milliseconds" claim) > OpenAI gpt-4o-mini-transcribe >
   Azure Fast Transcription > Google sync Recognize > Amazon (streaming session
   overhead; batch path disqualified). All are far below local CPU Whisper latency on
   weak hardware, which is the motivating scenario in ADR-032.

6. **BYOK friction differs sharply.** OpenAI, Groq, and xAI use a single bearer API key —
   ideal for ADR-032's bring-your-own-key commitment. Azure needs a provisioned Speech
   resource (key + region/endpoint). Amazon needs an IAM user with a policy and an
   access-key pair. Google effectively requires a GCP project with a service-account
   JSON key; plain API keys are second-class for Speech-to-Text V2. This makes Google
   and Amazon the weakest BYOK fits despite strong technology.

7. **Audio format note.** Parlotype's pipeline produces 16 kHz mono float PCM. Every
   provider accepts 16 kHz WAV (PCM16) uploads; OpenAI's Realtime API wants 24 kHz PCM,
   which would need resampling if streaming were ever adopted.

## Suggested Provider Order for Implementation

1. **OpenAI-compatible client** (covers OpenAI + Groq via base URL setting) — one
   implementation, two providers, simplest protocol, bearer-key BYOK.
2. **xAI Grok STT** — cheap, fast, single API key, but custom (small) REST schema.
3. **Azure Speech (Fast Transcription REST)** — enterprise users; official .NET SDK
   exists but plain REST keeps dependencies down.
4. **Google Cloud Speech-to-Text V2** — service-account auth friction.
5. **Amazon Transcribe** — last: no sync upload endpoint means streaming-only
   integration (`AWSSDK.TranscribeStreaming` dependency + IAM credentials), highest
   standard price, and a data-retention default that complicates the ADR-032
   transparency story. Add only on demand from AWS-committed users.

Follow-up ADRs still needed per ADR-032's out-of-scope list: provider selection,
`ISpeechRecognizer` extension shape, secure key storage (DPAPI), settings UI,
cloud-active indicator UX, and unreachable-provider fallback behaviour.
