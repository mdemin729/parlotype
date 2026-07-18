---
status: accepted
date: 2026-07-13
---

# 046. Security Hardening Batch (2026-07 Audit)

## Context

A full-application security audit
(docs/security/2026-07-11-security-audit.md) found two high-severity issues —
every transcript persisted to plaintext rolling logs at the default `Debug`
level, and no integrity verification on any model download despite
`WhisperModelInfo` carrying (unused, SHA-1) hash metadata — plus a series of
medium/low findings around cloud base URLs, clipboard exposure, process
argument quoting, and non-atomic settings writes.

## Decision

- **Transcript log hygiene (S1):** recognizer output text is never logged at
  any level (lengths/counts only) — this is a standing convention, not a
  one-off fix. The rolling-file sink is capped at `Information` via a
  provider-scoped filter; console keeps `Debug` for development.
  Provider-controlled error bodies are truncated (500 chars) before logging.
- **Download integrity (S2):** `StreamingFileDownloader` and the
  Parakeet/Gemma download loops hash while streaming (`IncrementalHash`,
  SHA-256) and compare before the atomic move. Mismatch ⇒ new Core
  `ModelIntegrityException` (file name + expected/actual digests), temp file
  deleted, destination never touched. `WhisperModelInfo.Sha` (SHA-1, never
  consumed) became `Sha256`; `ParakeetModelInfo.FileSha256` and
  `Gemma4ModelInfo.{Gguf,Mmproj}Sha256` added. Digests sourced from
  HuggingFace LFS metadata of the exact repo/revision each downloader uses.
  Policy: **fail-closed on mismatch, fail-open (warn) on missing digest** so
  a future catalog entry without a hash degrades to the old behaviour instead
  of bricking downloads. Cached files are not re-verified at load time.
- **Cloud base URL (S3):** new Core `CloudBaseUrlValidator` — HTTPS required
  unless the host is loopback (keeps LM Studio / llama.cpp self-hosting).
  Enforced at recognizer initialisation (throws
  `CloudProviderNotConfiguredException` → existing popup + Settings deep
  link) and surfaced as an inline hint in Cloud providers settings (the
  setting still persists while typing; the recognizer is the gate).
- **Clipboard exclusion (S4):** injected text additionally sets
  `ExcludeClipboardContentFromMonitorProcessing`,
  `CanIncludeInClipboardHistory`=0, `CanUploadToCloudClipboard`=0 within the
  same clipboard session, keeping dictation out of Win+V history and
  cross-device Cloud Clipboard. The restore path intentionally leaves the
  user's own content unflagged.
- **Hardening (S6/S7):** llama-server spawned via
  `ProcessStartInfo.ArgumentList`; `settings.json`/`secrets.json` written via
  new `AtomicFileWriter` (temp + `File.Move` overwrite) shared by both JSON
  stores.
- **Accepted/deferred:** llama-server sidecar auth (`--api-key`) deferred —
  it breaks crash-orphan adoption and user-managed external servers, and the
  threat requires same-user malware which already crosses the DPAPI boundary
  (S5); DPAPI scope + non-Windows base64 fallback remain as documented
  ADR-043 deferred items (S8); injection-target focus race documented (S9).

## Consequences

- Users' dictation no longer persists in logs or OS clipboard
  history/cloud sync; a tampered model download cannot reach the cache or be
  loaded; a typo'd `http://` provider URL can no longer leak the API key.
- Catalog digests must be updated whenever upstream model files change —
  the failure mode is a clear `ModelIntegrityException` with both digests,
  and tests assert every current catalog entry carries a digest.
- Support bundles (logs) contain less detail by default; if deep pipeline
  debugging is ever needed from users, an explicit opt-in verbose setting
  should be designed rather than reverting the file-sink cap.
- Manual verification still pending on a live desktop: Win+V exclusion
  behaviour and the settings inline URL hint.
