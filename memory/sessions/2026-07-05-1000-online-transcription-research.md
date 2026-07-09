---
title: "Session: 2026-07-05 — Online transcription provider research"
type: session
status: complete
tags: [research, cloud-providers, speech]
created: 2026-07-05
summary: "Researched cloud STT APIs (OpenAI, Google, Azure, xAI Grok, Groq) and wrote per-provider docs under docs/research/2026-07-05-online-transcription/."
---

# Session: 2026-07-05 — Online transcription provider research

## Active Focus
Research-only session (no code changes). Created
`docs/research/2026-07-05-online-transcription/` with a comparison README plus one
document per provider: OpenAI, Google Cloud Speech-to-Text V2, Azure AI Speech,
xAI Grok STT, Groq. Groundwork for the cloud `ISpeechRecognizer` implementations
deferred by ADR-032.

## Decisions Made
- Covered **both** xAI Grok and Groq: the task brief said "Grok", ADR-032 says "Groq",
  and as of April 2026 both are real, distinct STT providers.
- Recommended implementation order in the README: OpenAI-compatible client first
  (covers OpenAI + Groq via base-URL setting), then xAI Grok, then Azure Fast
  Transcription REST, Google last (service-account BYOK friction).

## Facts Learned
- OpenAI's multipart `POST /v1/audio/transcriptions` is the de-facto standard; Groq is
  byte-for-byte compatible with it. Google/Azure/xAI are custom APIs.
- xAI launched standalone Grok STT/TTS APIs on 2026-04-18: `POST /v1/stt` + WebSocket,
  $0.10/hr batch, $0.20/hr streaming, 25+ languages, diarization, us-east-1 only.
- Groq has **no streaming**, but whisper-large-v3-turbo runs ~216× real-time at
  $0.04/hr with a 10-second minimum bill per request.
- Azure Speech REST `v3.x` retired March 2026; current GA API version is `2025-10-15`
  (fast transcription: `/speechtotext/transcriptions:transcribe`).
- OpenAI Realtime API requires 24 kHz mono PCM — Parlotype's 16 kHz pipeline would need
  resampling if streaming is ever adopted; all providers' batch endpoints accept
  16 kHz WAV as-is.
- Amazon Transcribe (added on request) has **no synchronous file-upload endpoint** —
  batch requires S3 + async job polling, so the only dictation-viable AWS path is the
  streaming API via `AWSSDK.TranscribeStreaming` (SigV4 makes raw HTTP impractical).
  AWS may retain audio for model improvement by default (org-level opt-out) — a
  transparency-doc concern under ADR-032. Nova 2 Sonic is speech-to-speech, not STT.

## Open Blockers
- None. Pricing/limits are July-2026 snapshots and must be re-verified at
  implementation time.

## Documentation Status
- ADR: none required (research only, no Core/Platform/dependency changes)
- Vault (services/architecture): none required
- Knowledge (non-derivable facts): captured in the research docs themselves; distill
  into `memory/knowledge/` when the cloud-provider implementation work actually starts

## Next Action
When cloud-provider work begins: write the follow-up ADR for the
`ISpeechRecognizer` cloud extension shape + secure key storage (DPAPI), starting from
`docs/research/2026-07-05-online-transcription/README.md` (suggested order: OpenAI-compatible
client → xAI Grok → Azure → Google).
