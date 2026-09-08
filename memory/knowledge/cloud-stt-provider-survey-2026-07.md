---
title: Cloud STT provider survey (July 2026)
type: knowledge
tags: [cloud, speech, providers, openai, groq, xai, azure, aws, research]
created: 2026-09-07
last_updated: 2026-09-07
summary: The provider comparison behind ADR-032/ADR-043 — why OpenAI-compatible plus xAI shipped and Azure/AWS/Realtime did not. Pricing and limits are a 2026-07-05 snapshot and must be re-verified before acting on them
---

# Cloud STT provider survey (July 2026)

Research gathered 2026-07-05 while scoping the opt-in cloud engines. The two that
shipped are covered by [[decisions/_index|ADR-032]] (positioning) and
[[decisions/_index|ADR-043]] (implementation); this note preserves the
**rejected alternatives**, which are the part no ADR records.

> [!warning] Snapshot, not current truth
> Every price, limit and API version below is as of **2026-07-05**. Cloud STT
> pricing moves fast. Re-verify against vendor docs before quoting any of it or
> reviving a rejected option.

## The de-facto standard

OpenAI's multipart `POST /v1/audio/transcriptions` is what everyone else copies.
**Groq is byte-for-byte compatible with it** — which is the whole reason
Parlotype ships one "OpenAI-compatible" engine with a configurable base URL
rather than a provider per vendor. Google, Azure and xAI are custom APIs and each
would need its own client.

## Per-provider findings

| Provider | Status | Why |
|---|---|---|
| **OpenAI / Groq** | Shipped | One client covers both. Groq: whisper-large-v3-turbo at ~216× real-time, $0.04/hr — but **no streaming**, and a 10-second minimum bill per request, which is punitive for short dictation utterances |
| **xAI Grok STT** | Shipped | Standalone STT/TTS launched 2026-04-18: `POST /v1/stt` + WebSocket, $0.10/hr batch, $0.20/hr streaming, 25+ languages, diarization. **us-east-1 only** — a latency and data-residency constraint worth surfacing |
| **Azure Speech** | Not shipped | Custom API, and the surface moved: REST `v3.x` retired **March 2026**; current GA is api-version `2025-10-15` with fast transcription at `/speechtotext/transcriptions:transcribe` |
| **Amazon Transcribe** | Not shipped | **No synchronous file-upload endpoint at all.** Batch means S3 upload plus async job polling — unusable for dictation. The only viable path is the streaming API via `AWSSDK.TranscribeStreaming`, since SigV4 makes raw HTTP impractical. Compounding it: AWS may **retain audio for model improvement by default** (org-level opt-out), which is a direct conflict with ADR-032's transparency posture. Nova 2 Sonic is speech-to-speech, not STT |

## If streaming is ever adopted

Parlotype's pipeline is 16 kHz mono float. **Every provider's batch endpoint
accepts 16 kHz WAV as-is** — no resampling needed for the shipped design. But
the **OpenAI Realtime API requires 24 kHz mono PCM**, so a streaming cloud mode
would add a resampling stage that the batch design does not need. Groq, the
cheapest batch option, has no streaming at all.

Related: [[brand-positioning]] for the local-default/cloud-by-choice framing this
survey was scoped against.
