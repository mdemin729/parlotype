# Amazon — Amazon Transcribe (AWS)

**Date:** 2026-07-05 · **API style:** custom AWS API (SigV4-signed, event-stream protocol) — **not** OpenAI-compatible

## Summary

AWS's transcription product is **Amazon Transcribe**, a mature service with two sharply
different modes: **batch** (asynchronous jobs over files in S3) and **streaming**
(real-time over HTTP/2 or WebSocket). Everything is AWS-idiomatic: IAM credentials,
SigV4 request signing, S3 for batch I/O, and AWS's binary event-stream encoding for
streaming. Nothing resembles the OpenAI API.

(AWS also ships **Amazon Nova 2 Sonic** on Bedrock — a speech-to-speech conversational
model, token-priced at $3/M speech input tokens. It is a voice-agent building block,
not a transcription API, so it's out of scope here.)

## Modes

| Mode | API | How it works |
|---|---|---|
| **Batch** | `StartTranscriptionJob` | Input file **must already be in an S3 bucket**; job runs asynchronously; poll `GetTranscriptionJob`; JSON transcript lands in S3 (your bucket, or a service-managed one with a 15-minute temporary download URI, 90-day expiry) |
| **Streaming** | `StartStreamTranscription` | Bidirectional **HTTP/2** event stream or **WebSocket** (presigned URL); audio chunks in, partial + final results out in real time |

There is **no synchronous "POST a file, get text back" endpoint** — this is the single
biggest structural difference from every other provider surveyed. Even a 5-second clip
must go through S3-plus-job-polling (seconds to tens of seconds of overhead) or be
pushed through the streaming API.

## Features

- **Word-level timestamps + confidence scores** on every word by default (batch output
  JSON: `transcripts` / `items` / `audio_segments`).
- **Speaker partitioning (diarization)** and **channel identification** (max 2
  channels; media with more than two channels is unsupported).
- **Custom vocabularies**, **vocabulary filtering** (profanity masking — analogous to
  Parlotype's existing profanity setting), and trainable **custom language models**.
- **Automatic language identification**; 100+ languages/variants for batch, a subset
  for streaming.
- **PII redaction** (extra cost) and automatic PHI identification; HIPAA-eligible.
- **Partial results stabilization** for streaming (reduces flicker of interim text).
- **Sample rates:** streaming requires the sample rate declared in the request; 8 kHz
  telephony and 16–48 kHz hi-fi supported — Parlotype's 16 kHz mono pipeline fits.
- **Formats:** batch accepts AMR, FLAC, M4A, MP3, MP4, Ogg, WebM, WAV; streaming
  accepts raw PCM (16-bit signed LE), FLAC, and Ogg-Opus. Lossless (WAV/FLAC) recommended.
- **Regions:** the broadest footprint surveyed — 20+ regions including several EU
  options (relevant for data-residency-conscious users, unlike xAI's single us-east-1).

## Auth & Access Model

- IAM user or role with `transcribe:*` (and S3 for batch) permissions; requests signed
  with **SigV4** using an access-key-ID/secret pair. WebSocket streaming uses presigned
  URLs.
- BYOK friction is **medium-high**: users must create an IAM user, attach a policy, and
  paste two values (key ID + secret) plus a region — easier than Google's
  service-account JSON, harder than a single bearer token.
- Official .NET SDKs: `AWSSDK.TranscribeService` (batch) and
  `AWSSDK.TranscribeStreaming` (streaming) — both managed-code, no native binaries.
  Hand-rolling SigV4 + event-stream framing without the SDK is not practical.

## Privacy Note (matters for ADR-032 transparency docs)

AWS documentation states Amazon Transcribe "may temporarily store your content to
continuously improve the quality of its analysis models"; deletion requires a support
case, and opting out service-wide requires an AWS Organizations AI-services opt-out
policy. This is a weaker default privacy posture than OpenAI's API (no training on API
data by default) and must be disclosed in the provider transparency docs ADR-032 mandates.

## Pricing (indicative, July 2026 — verify)

- Batch and streaming: **$0.024/min** (~$1.44/hr) tier 1, tiering down to $0.0078/min
  at 5M+ min. Billed per second with a **15-second minimum per request**.
- Free tier: 60 min/month for 12 months. PII redaction and custom language models cost
  extra. Regional price variations apply.

The most expensive standard-tier provider surveyed (~14× xAI Grok batch).

## Differences vs. the other providers

- Only provider with **no synchronous file-upload endpoint** — batch is S3 + async job.
- Only provider requiring **request signing** (SigV4) rather than a bearer/subscription
  key — SDK effectively mandatory.
- Streaming uses AWS's proprietary binary event-stream framing over HTTP/2 (or
  WebSocket), vs. JSON-over-WS (OpenAI/xAI) or gRPC (Google).
- Feature depth comparable to Azure (custom models, vocabulary, redaction) with the
  widest region coverage.

## Fit for Parlotype

- **Best-fit mode:** streaming (`AWSSDK.TranscribeStreaming`), paradoxically — even for
  push-to-talk utterances — because the batch path's S3-upload-plus-poll overhead is
  unacceptable for dictation latency. That makes the *minimum viable* AWS integration
  the most complex of all providers (SDK dependency + event-stream lifecycle + IAM
  credential entry).
- **BYOK:** medium friction (IAM key pair + region), acceptable but not great.
- **Recommendation:** lowest priority alongside Google. Highest integration complexity,
  highest standard price, and a training-data default that complicates the transparency
  story. Worth adding only if AWS-committed enterprise users ask for it.

## Sources

- [What is Amazon Transcribe? — AWS docs](https://docs.aws.amazon.com/transcribe/latest/dg/what-is.html)
- [How Amazon Transcribe works — AWS docs](https://docs.aws.amazon.com/transcribe/latest/dg/how-it-works.html)
- [Data input and output — AWS docs](https://docs.aws.amazon.com/transcribe/latest/dg/how-input.html)
- [Amazon Transcribe pricing — AWS](https://aws.amazon.com/transcribe/pricing/)
- [Introducing Amazon Nova 2 Sonic — AWS News Blog](https://aws.amazon.com/blogs/aws/introducing-amazon-nova-2-sonic-next-generation-speech-to-speech-model-for-conversational-ai/)
