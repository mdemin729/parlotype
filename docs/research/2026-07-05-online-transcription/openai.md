# OpenAI — Speech-to-Text API

**Date:** 2026-07-05 · **API style:** the de-facto industry standard (others copy it)

## Summary

OpenAI offers two distinct transcription surfaces:

1. **Batch REST** — `POST /v1/audio/transcriptions` (and `/v1/audio/translations` for
   translate-to-English). Multipart file upload, synchronous response. This request shape
   is the one cloned by Groq, Deepgram, Fireworks, and most local inference servers.
2. **Realtime API** — WebSocket (server-side) or WebRTC (browser) sessions with
   `type: "transcription"` for live streaming transcription with incremental deltas.

## Models (as of July 2026)

| Model | Notes |
|---|---|
| `whisper-1` | Original hosted Whisper. Only model with `verbose_json`/`srt`/`vtt` output and `timestamp_granularities[]` (word/segment timestamps). |
| `gpt-4o-transcribe` | Better WER than Whisper across benchmarks; `json`/`text` output only; supports prompting. |
| `gpt-4o-mini-transcribe` | Cheaper/lighter variant of the above. |
| `gpt-4o-transcribe-diarize` | Speaker diarization (`diarized_json` format); `/v1/audio/transcriptions` only, needs `chunking_strategy` for audio > 30 s; up to 4 reference clips for speaker mapping. |
| `gpt-realtime-whisper` | Natively streaming model for the Realtime API; tunable latency. |

## Batch Endpoint Details

- **Request:** multipart form — `file`, `model`, optional `language` (ISO-639-1),
  `prompt`, `response_format`, `temperature`, `timestamp_granularities[]`, `stream`.
- **Auth:** `Authorization: Bearer <api-key>`.
- **File limit:** 25 MB. **Formats:** mp3, mp4, mpeg, mpga, m4a, wav, webm.
- **Languages:** ~98 supported.
- **Streaming of results:** `stream=true` returns server-sent-event deltas *while a
  completed recording is being transcribed* — this is progressive result delivery, not
  live-microphone streaming.
- **Prompting:** on gpt-4o models, a free-text prompt biases spelling of acronyms,
  names, and technical terms (analogous to Whisper's `initial_prompt` that Parlotype
  already exposes via `WhisperOptions`).

## Realtime API (live streaming)

- **Transport:** WebSocket for server-side pipelines; WebRTC for browsers.
- **Session:** created with `type: "transcription"`; config selects model, optional
  language hint, and a `delay` latency/accuracy knob (`minimal` … `xhigh`).
- **Audio in:** base64 chunks via `input_audio_buffer.append`; **24 kHz mono PCM**
  (Parlotype's 16 kHz pipeline would need resampling).
- **Turn detection:** optional server-side VAD auto-commits at turn boundaries, or the
  client commits manually with `input_audio_buffer.commit` (Parlotype's own Silero VAD
  and push-to-talk key-up are natural commit signals).
- **Events out:** `conversation.item.input_audio_transcription.delta` (incremental) and
  `…completed` (final per committed segment). Deltas can be *corrective* — UI must
  tolerate rewrites.
- Optional input noise reduction.

## Pricing (indicative, July 2026 — verify)

- `whisper-1` / `gpt-4o-transcribe`: ~$0.006/min
- `gpt-4o-mini-transcribe`: ~$0.003/min
- `gpt-realtime-whisper` (live): ~$0.017/min

## Fit for Parlotype

- **Best-fit mode:** batch POST of the buffered utterance WAV after key-up.
  `gpt-4o-mini-transcribe` is the price/latency sweet spot; `whisper-1` if word
  timestamps are ever needed.
- **BYOK:** single bearer key — ideal for ADR-032's model. Official `OpenAI` NuGet
  package exists, but a hand-rolled `HttpClient` multipart POST is ~50 lines and keeps
  the implementation provider-portable (base-URL swap → Groq etc.).
- **Caveat:** 25 MB cap is irrelevant for dictation-length clips; the Realtime API's
  24 kHz requirement only matters if live streaming is adopted later.

## Sources

- [Speech to text guide — OpenAI API](https://developers.openai.com/api/docs/guides/speech-to-text)
- [Realtime transcription guide — OpenAI API](https://developers.openai.com/api/docs/guides/realtime-transcription)
- [Introducing next-generation audio models — OpenAI](https://openai.com/index/introducing-our-next-generation-audio-models/)
- [GPT-4o Transcribe model page](https://developers.openai.com/api/docs/models/gpt-4o-transcribe)
- [OpenAI transcription pricing overview (third-party tracker)](https://costgoat.com/pricing/openai-transcription)
