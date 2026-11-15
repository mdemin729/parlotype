---
title: "HuggingFace file digests for download verification"
area: "speech-engines"
summary: "The HuggingFace tree API's lfs.oid is the file's SHA-256; non-LFS files expose only a git blob SHA-1 and must be downloaded and hashed."
verified: 2026-11-14
---

# HuggingFace file digests for download verification

- `GET https://huggingface.co/api/models/{repo}/tree/{revision}[/{path}]`
  returns one entry per file; for **LFS files** `lfs.oid` **is the SHA-256**
  of the content — authoritative and matches what
  `/resolve/{revision}/{file}` serves.
- **Non-LFS files** (small text like `tokens.txt`) only expose a git blob
  `oid` (SHA-1 of the git object, *not* the content hash) — download the file
  once and hash it yourself.
- Read the digest at the commit the downloader uses, not at `main`: every
  catalog (`WhisperModelInfo`, `ParakeetModelInfo`, `Gemma4ModelInfo`,
  `SpeechCleanupModelInfo`) carries a `HuggingFaceRevision` commit SHA beside
  its digests, and the two change together
  ([ADR-076](../decisions/076-pin-model-downloads-to-a-commit.md)).
  `ModelDownloadIntegrityTests` requires a digest on every entry and
  `ModelDownloadRevisionTests` a commit-pinned URL.
- A `ModelIntegrityException` (expected vs actual digest in the message) on a
  pinned URL means the bytes at a fixed commit differ: a corrupted or
  tampered download, not an upstream update. Don't refresh the catalog value
  to make it pass and don't disable verification (ADR-046). Upstream's own
  rewrites are covered in
  [the note on in-place rewrites](huggingface-repos-rewrite-files-in-place.md).
