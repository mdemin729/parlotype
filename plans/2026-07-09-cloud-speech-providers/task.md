---
title: Cloud speech providers — OpenAI-compatible client + xAI Grok STT
status: in-progress
created: 2026-07-09
started: 2026-07-09
completed:
---

# Cloud speech providers — OpenAI-compatible client + xAI Grok STT

## Problem

Local engines don't deliver acceptable latency on every machine (ADR-032). Users who
choose to should be able to opt into a cloud speech provider with their own API key.
Research in `docs/research/2026-07-05-online-transcription/` recommends starting with
an OpenAI-compatible client (covers OpenAI + Groq via base URL) and xAI Grok STT.

## Approach

Follow the existing engine extension pattern (ADR-041 Parakeet as reference):

- Two new `SpeechEngine` members: `OpenAiCompatible`, `XaiGrok`.
- Two new `ISpeechRecognizer` implementations in Platform, both batch multipart
  HTTP POSTs of the buffered 16 kHz mono utterance encoded as WAV:
  - `OpenAiCompatibleSpeechRecognizer` → `{baseUrl}/audio/transcriptions`
    (OpenAI protocol; base URL configurable so one client covers OpenAI, Groq, etc.)
  - `XaiGrokSpeechRecognizer` → `{baseUrl}/stt` (xAI custom schema)
- API keys stored via a new `ISecretStore` (Core) with DPAPI protection on Windows
  (`secrets.json`, per-user scope) and plaintext-with-warning fallback elsewhere.
- Settings UI: cloud config section (base URL, model, key entry) + engine cards with
  explicit "audio leaves your machine" wording; cloud indicator per ADR-032
  transparency commitment.
- Brand commitments (ADR-032): local stays default, cloud strictly opt-in, BYOK only.

Implementation is delegated to Sonnet subagents (Core/Platform task, then Desktop
task), reviewed and integrated by the coordinating session.

## Workplan

- [ ] Task A — Core contracts + Platform recognizers + secret store + tests
- [ ] Review A (build, tests, request-shape review)
- [ ] Task B — Desktop settings UI + cloud indicator + headless/screenshot tests
- [ ] Review B (build, tests, UI review)
- [ ] ADR-043 + memory vault updates + INDEX.md
- [ ] Final: zero-warning build, full test pass
