---
title: "HuggingFace file digests for download verification"
area: "speech-engines"
summary: "The HuggingFace tree API's lfs.oid is the file's SHA-256; non-LFS files expose only a git blob SHA-1 and must be downloaded and hashed."
verified: 2026-11-11
---

# HuggingFace file digests for download verification

- `GET https://huggingface.co/api/models/{repo}/tree/{revision}[/{path}]`
  returns one entry per file; for **LFS files** `lfs.oid` **is the SHA-256**
  of the content — authoritative and matches what
  `/resolve/{revision}/{file}` serves.
- **Non-LFS files** (small text like `tokens.txt`) only expose a git blob
  `oid` (SHA-1 of the git object, *not* the content hash) — download the file
  once and hash it yourself.
- Pin the revision to whatever the downloader actually uses
  (e.g. Whisper GGMLs come from `sandrohanea/whisper.net` revision `v3` under
  `classic/`; Parakeet/Gemma use `main`). Catalog digests live in
  `WhisperModelInfo` / `ParakeetModelInfo` / `Gemma4ModelInfo` and are
  enforced by `ModelDownloadIntegrityTests` (every entry must carry one).
- When a model updates upstream, the download fails closed with
  `ModelIntegrityException` (expected vs actual digest in the message) —
  refresh the catalog value from the API, don't disable verification
  (ADR-046).
