# Google Cloud — Speech-to-Text V2 (Chirp)

**Date:** 2026-07-05 · **API style:** custom (gRPC-first + REST, resource-oriented) — **not** OpenAI-compatible

## Summary

Google's current offering is the **Speech-to-Text V2 API** (`speech.googleapis.com`),
a Google-Cloud-style resource API: you create *recognizer* resources in a project/region
and call one of three recognition methods against them. The flagship model is **Chirp 3**,
a multilingual ASR-specific generative model available exclusively in V2. The API shape,
auth, and response schema are entirely Google's own; nothing is OpenAI-compatible.

(Google's Gemini multimodal API can also transcribe audio, but that is a general LLM
path, not the dedicated speech product — out of scope here.)

## Recognition Methods

| Method | Mode | Constraints |
|---|---|---|
| `Recognize` | Synchronous batch | Audio < 1 minute, inline bytes or GCS URI |
| `StreamingRecognize` | Bidirectional gRPC stream | Real-time mic audio, interim + final results |
| `BatchRecognize` | Asynchronous batch | Long audio (1 min–1 h; ≤ 20 min with word timestamps), input from Google Cloud Storage |

## Chirp 3 Features

- Automatic punctuation and capitalization.
- **Speaker diarization** — `BatchRecognize`/`Recognize` only (not streaming); ~14
  languages (EN variants, ES, FR, DE, ZH, JA, KO, …).
- **Timestamps:** utterance-level in streaming; word-level timestamps/confidences are a
  documented weak spot on Chirp 3 (values returned lack true confidence semantics).
- **Language-agnostic transcription** with automatic language detection.
- **Speech adaptation** — phrase-based biasing for custom vocabulary (analogue of
  Whisper's initial prompt), plus custom formatting prompts.
- Built-in **denoiser**; adjustable **endpointing sensitivity** (latency vs. accuracy).
- 24 languages GA, 70+ more in Preview.
- **Audio input:** `AutoDetectDecodingConfig` accepts common encodings without explicit
  format declaration — a 16 kHz mono WAV from Parlotype's pipeline works as-is.
- **Regions:** `us` and `eu` multi-regions at GA.

## Auth & Access Model

This is the heaviest of the surveyed providers:

- Requires a **GCP project** with the Speech-to-Text API enabled and billing attached.
- Canonical auth is a **service account** (JSON key or workload identity) via OAuth 2;
  plain API keys are limited/second-class for V2.
- .NET access via the official `Google.Cloud.Speech.V2` NuGet package (gRPC transport),
  which pulls in the sizeable Google.Api.Gax/Grpc dependency tree.

## Pricing (indicative, July 2026 — verify)

- V2 standard recognition ≈ $0.016/min (~$0.96/hr), with tiered volume discounts and a
  cheaper rate if data logging is enabled. Dynamic batch (`BatchRecognize`) is cheaper
  than streaming/sync.

## Differences vs. the OpenAI-style APIs

- Resource-oriented (create recognizers, reference them per request) vs. stateless
  one-shot POST.
- Protobuf/gRPC-first; REST exists but streaming is gRPC-only.
- Rich structured response (results → alternatives → words) vs. flat `{ "text": … }`.
- Project/IAM/service-account auth vs. single bearer key.

## Fit for Parlotype

- **Technology:** strong — good multilingual accuracy, adaptation, denoiser, and a true
  streaming path if ever needed.
- **BYOK friction: high.** Asking a dictation-app user to create a GCP project, enable
  an API, create a service account, and paste a JSON key violates the spirit of
  ADR-032's "user supplies a key" simplicity. The `Google.Cloud.Speech.V2` dependency
  tree is also heavy for Parlotype.Platform.
- **Recommendation:** technically viable but the weakest first candidate; implement
  after the bearer-key providers, if user demand exists.

## Sources

- [Chirp 3 transcription — Google Cloud docs](https://docs.cloud.google.com/speech-to-text/docs/models/chirp-3)
- [Compare transcription models — Google Cloud docs](https://docs.cloud.google.com/speech-to-text/docs/transcription-model)
- [Speech-to-Text V2 API announcement — Google Cloud blog](https://cloud.google.com/blog/products/ai-machine-learning/google-cloud-speech-to-text-v2-api)
- [Speech-to-Text product page](https://cloud.google.com/speech-to-text)
- [Speech-to-Text release notes](https://docs.cloud.google.com/speech-to-text/docs/release-notes)
