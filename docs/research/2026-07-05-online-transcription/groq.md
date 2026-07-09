# Groq — Speech-to-Text (hosted Whisper)

**Date:** 2026-07-05 · **API style:** **OpenAI-compatible** (drop-in) · Not to be confused with xAI's **Grok** ([xai-grok.md](xai-grok.md))

## Summary

Groq (the LPU inference company) hosts OpenAI's open-source **Whisper** models behind a
**fully OpenAI-compatible** endpoint. It is the provider explicitly named in
[ADR-032](../../decisions/032-online-speech-providers-positioning.md) and the cheapest
way to get cloud Whisper with dramatically lower latency than OpenAI's own hosting.

## Endpoints

- `POST https://api.groq.com/openai/v1/audio/transcriptions`
- `POST https://api.groq.com/openai/v1/audio/translations` (to English; whisper-large-v3 only)

Same multipart schema as OpenAI: `file`, `model`, `language`, `prompt` (≤ 224 tokens),
`temperature`, `response_format` (`json`, `verbose_json`, `text`).
Auth: `Authorization: Bearer <groq-api-key>`. Any OpenAI SDK works by overriding the
base URL.

## Models & Pricing (July 2026 — verify)

| Model | Price | Notes |
|---|---|---|
| `whisper-large-v3` | $0.111/audio-hour | Highest accuracy; supports translation |
| `whisper-large-v3-turbo` | $0.04/audio-hour | ~216× real-time speed factor; transcription only |

Minimum billing of 10 seconds per request — negligible for dictation, but note that
every short push-to-talk utterance bills as 10 s.

## Features & Limits

- **File limits:** 25 MB (free tier) / 100 MB (dev tier). Formats: flac, mp3, m4a,
  ogg, wav, webm.
- **Timestamps:** word **and** segment level via `response_format=verbose_json` +
  `timestamp_granularities` — same as OpenAI's whisper-1.
- **Quality metadata:** verbose responses include avg logprob, no-speech probability,
  and compression ratio — the same signals `WhisperSpeechRecognizer` can use for
  hallucination filtering, which transfers directly.
- **Multilingual** (Whisper's ~99 languages), `language` hint supported.
- **No diarization.**
- **No live streaming API** — batch only. In practice turbo's ~216× real-time speed
  means a 10-second clip transcribes in well under a second including network overhead,
  so short-utterance dictation still *feels* real-time.

## Differences vs. the other providers

- The only surveyed provider that is byte-for-byte OpenAI-compatible — an OpenAI client
  implementation covers Groq by changing only the base URL, key, and model name.
- Runs the same Whisper family Parlotype already uses locally via Whisper.net — cloud
  output should be stylistically consistent with local mode (same tokenizer, similar
  hallucination behaviours, same `initial_prompt` semantics).
- Fastest inference of the batch-only providers; no streaming path if live captions are
  ever wanted.

## Fit for Parlotype

- **Best-fit mode:** batch POST per utterance with `whisper-large-v3-turbo`.
- **BYOK:** excellent — free-tier key available instantly, single bearer token.
- **Strategic value:** highest — one `OpenAiCompatibleSpeechRecognizer` with a
  configurable base URL gives users OpenAI *and* Groq (and other compatible hosts) from
  a single implementation, maximizing provider choice per line of code.

## Sources

- [Speech to text — GroqDocs](https://console.groq.com/docs/speech-to-text)
- [Whisper Large v3 — GroqDocs](https://console.groq.com/docs/model/whisper-large-v3)
- [Whisper Large v3 Turbo — GroqDocs](https://console.groq.com/docs/model/whisper-large-v3-turbo)
- [API reference — GroqDocs](https://console.groq.com/docs/api-reference)
- [Whisper Large v3 Turbo on Groq — Groq blog](https://groq.com/blog/whisper-large-v3-turbo-now-available-on-groq-combining-speed-quality-for-speech-recognition)
