# xAI — Grok Speech-to-Text API

**Date:** 2026-07-05 · **API style:** custom REST + WebSocket — **not** OpenAI-compatible (unlike xAI's chat API, which is)

## Summary

xAI launched standalone **Grok Speech to Text (STT)** and **Text to Speech (TTS)** APIs
on **April 18, 2026**, built on the stack powering Grok Voice, Tesla vehicles, and
Starlink support. Not to be confused with **Groq** (separate company, hosts Whisper —
see [groq.md](groq.md)).

Notably, while xAI's chat completions API mirrors OpenAI's, the **STT API is xAI's own
schema** at its own endpoint — an OpenAI transcription client will not work against it.

## Endpoints & Protocols

| Mode | Endpoint | Notes |
|---|---|---|
| Batch | `POST https://api.x.ai/v1/stt` | Multipart file upload (`model=grok-stt`, `format`, `language`); claims transcripts for large files "in milliseconds" |
| Streaming | `wss://api.x.ai/v1/stt` | Binary audio frames in; interim + final transcript events out |
| Voice Agent | `wss://api.x.ai/v1/realtime?model=grok-voice-latest` | Full bidirectional speech agent (STT+LLM+TTS) — beyond transcription-only scope |

Auth: `Authorization: Bearer <xai-api-key>` — single key, same console as the chat API.

## Features

- **Word-level timestamps**, **speaker diarization**, **multichannel** processing.
- **Smart Turn** end-of-turn detection for streaming (server-side VAD analogue).
- **25+ languages**.
- **12 audio input formats** including MP3, WAV, and μ-law (telephony).
- Claimed benchmark: 5.0% error rate on phone-call entity recognition vs. ElevenLabs
  12.0%, Deepgram 13.5%, AssemblyAI 21.3% (vendor-published — treat accordingly).
- Enterprise posture: SOC 2 Type II, HIPAA-eligible, GDPR, data residency, SSO/RBAC.

## Limits (published at launch)

- File size up to **500 MB**.
- REST: 600 requests/min; WebSocket: 10 connections/s; 100 concurrent streaming
  sessions per team.
- Hosted in `us-east-1` (single region at launch — EU data-residency users take note).

## Pricing (July 2026 — verify)

- **Batch: $0.10 per audio-hour** (~$0.0017/min)
- **Streaming: $0.20 per audio-hour**

This is the cheapest full-featured provider surveyed — roughly 3.5× cheaper than
OpenAI's gpt-4o-mini-transcribe for batch.

## Differences vs. OpenAI-style APIs

- Different endpoint path (`/v1/stt` vs. `/v1/audio/transcriptions`) and different
  parameter names — a small dedicated client is required (still just one multipart POST).
- Streaming uses raw binary frames over WebSocket rather than base64-JSON envelopes
  (OpenAI) or gRPC (Google) — simpler wire format.
- No official SDK for .NET; plain `HttpClient`/`ClientWebSocket` is the path.

## Fit for Parlotype

- **Best-fit mode:** batch POST per utterance — cheapest per hour, fast, word
  timestamps and language auto-detect included.
- **BYOK:** excellent — one bearer key from the xAI console, same key as Grok chat.
- **Risks:** API is ~3 months old (launched 2026-04); rate limits, schema, and pricing
  may still move. Single US region may matter to EU-privacy-minded users — worth
  surfacing in the ADR-032-mandated transparency docs.

## Sources

- [Voice overview — xAI docs](https://docs.x.ai/docs/guides/voice)
- [Grok Speech to Text and Text to Speech APIs — xAI news](https://x.ai/news/grok-stt-and-tts-apis)
- [Voice API product page — xAI](https://x.ai/api/voice)
- [xAI launches standalone Grok STT/TTS APIs — MarkTechPost](https://www.marktechpost.com/2026/04/18/xai-launches-standalone-grok-speech-to-text-and-text-to-speech-apis-targeting-enterprise-voice-developers/)
- [Grok Speech-to-Text API guide (endpoint/pricing/limits) — LaoZhang blog](https://blog.laozhang.ai/en/posts/grok-speech-to-text-api)
