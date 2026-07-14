# Parlotype Security Audit — 2026-07-11

Auditor: AI-assisted code review (full `src/` sweep), plan
[2026-07-11-audio-pipeline-perf-security](../../plans/2026-07-11-audio-pipeline-perf-security/task.md).
Remediations landed 2026-07-13 on the same plan; per-finding disposition below.

## Threat model

Parlotype is a single-user desktop dictation app whose core promise is
**local-by-default privacy** ("audio never leaves the machine in local mode").
Assets, in order of importance:

1. **The user's voice and dictated text** — routinely contains names,
   addresses, message drafts, occasionally credentials.
2. **BYOK cloud API keys** (OpenAI-compatible, xAI Grok) — bearer credentials
   with billing attached.
3. **Integrity of downloaded artifacts** loaded into the process: Whisper GGML
   / Parakeet ONNX / Gemma GGUF models (parsed by native code) and the
   llama-server sidecar binary (executed).

Out of scope: OS-level compromise by malware already running as the user
(DPAPI's boundary), physical access, and the third-party model weights'
behavior itself.

## Findings summary

| # | Severity | Finding | Disposition |
|---|----------|---------|-------------|
| S1 | **High** (privacy) | Transcribed text persisted to plaintext rolling logs at default `Debug` level | **Fixed** — transcripts removed from all log sites; file sink capped at `Information` |
| S2 | **High** (supply chain) | Model downloads (Whisper, Parakeet, Gemma) never integrity-checked | **Fixed** — SHA-256 verified during download where the catalog has a hash; fail-closed typed error |
| S3 | Medium | Cloud base URL accepts `http://` — key + audio in cleartext if misconfigured | **Fixed** — HTTPS required unless loopback; validated at save and at init |
| S4 | Medium (privacy) | Injected text enters Windows Clipboard History / Cloud Clipboard sync | **Fixed** — exclusion formats set alongside `CF_UNICODETEXT` |
| S5 | Low–Medium | llama-server sidecar unauthenticated on `127.0.0.1`; adoption trusts any listener on the port | **Accepted / deferred** — see rationale |
| S6 | Low | llama-server arguments built by string concatenation of settings-derived paths | **Fixed** — `ProcessStartInfo.ArgumentList` |
| S7 | Low | `settings.json` / `secrets.json` written non-atomically; corruption silently resets (secret loss) | **Fixed** — temp-file + atomic replace |
| S8 | Info | DPAPI `CurrentUser` decryptable by any same-user process; non-Windows fallback is base64 plaintext | **Accepted** (documented; keychain integration tracked in ADR-043 deferred list) |
| S9 | Info | Text injection targets "last non-Parlotype foreground window" — can land in an unintended window on focus change | **Accepted** (follow-up candidate: re-check foreground window at injection time) |

## Finding details

### S1 — Transcripts in plaintext logs (High, fixed)

`App.axaml.cs` set a global `SetMinimumLevel(LogLevel.Debug)` feeding both the
console and the rolling file sink (`%LOCALAPPDATA%/parlotype/logs/`, 10 MB ×
daily files). `AudioPipelineService` logged the full transcription result at
Debug, so **every dictated utterance was persisted unencrypted to disk**,
surviving in rolled files, backups, and logs shared for support. This directly
contradicts the product's privacy positioning.

Fix: transcript content removed from all log statements (lengths/durations/
segment counts logged instead); repo-wide sweep for content-bearing log sites;
rolling-file sink minimum level raised to `Information` (console remains
`Debug` for development); cloud provider error bodies truncated before
logging. Regression test asserts the pipeline path emits no transcript text
into captured logs.

Residual risk: a future log statement could reintroduce the leak — convention
recorded in `CLAUDE.md`-adjacent vault conventions: *never log recognizer
output text*.

### S2 — No integrity verification on model downloads (High, fixed)

- Whisper GGML files were downloaded from HuggingFace over HTTPS but never
  hash-verified, although `WhisperModelInfo.Sha` metadata existed in the
  catalog (evidently intended, never wired).
- Parakeet ONNX files (the **default engine**, auto-downloaded silently on
  first use per ADR-042) had no hash metadata at all.
- Gemma GGUF files: same.
- Contrast: `LlamaServerInstaller` already verified SHA-256 for the
  llama-server binary archives — the in-repo precedent.

TLS protects transit only; a compromised upstream account/CDN or a tampered
cache file would feed attacker-controlled bytes to native ONNX/GGML parsers
(a real RCE surface) or, for the sidecar, execute an attacker binary.

Fix: `StreamingFileDownloader` now hashes the stream (`IncrementalHash`,
SHA-256) while writing and compares against an expected digest before the
atomic move; mismatch deletes the temp file and throws a typed
`ModelIntegrityException` surfaced through the existing download-dialog error
path. Catalog digests are wired for Whisper and Parakeet files; files without
a catalog digest log a warning and skip verification (fail-open on *missing
metadata*, fail-closed on *mismatch*) so new catalog entries cannot brick
downloads by omission.

Residual risk: already-cached files are not re-verified at load time (cost
/ benefit decision — a local attacker who can rewrite the cache can also
rewrite the catalog hash inside the app directory); catalog digests must be
maintained when models are updated upstream.

### S3 — Cloud base URL scheme not validated (Medium, fixed)

`OpenAiCompatibleSpeechRecognizer` used the `settings.json` string verbatim.
A typo'd or tampered `http://` URL (settings.json is plaintext and unsigned)
would send the Bearer API key plus recorded speech in cleartext.

Fix: shared `CloudBaseUrlValidator` (Core) — `https` required unless the host
is loopback (`localhost` / `127.0.0.0/8` / `::1`), preserving self-hosted
OpenAI-compatible servers (LM Studio, llama.cpp). Enforced both at save time
(inline validation message in Cloud providers settings) and at recognizer
initialization (actionable typed error).

### S4 — Clipboard injection leaks into history/cloud sync (Medium, fixed)

`ClipboardTextInjectionService` wrote bare `CF_UNICODETEXT`; Windows Clipboard
History (Win+V) retains it and Cloud Clipboard can sync it cross-device —
silently exfiltrating dictations on machines with those features enabled.

Fix: alongside the text, the service now sets the standard exclusion formats
`ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory`
(=0) and `CanUploadToCloudClipboard` (=0). The *restore* path intentionally
does not set them — the user's original clipboard content keeps its normal
behavior. **Manual verification pending** (dictate on a live machine and
confirm Win+V shows no entry for the injected text) — headless tests cannot
exercise the Win32 clipboard.

Known adjacent issue (robustness, not fixed here): only text is saved/restored
around injection — a pre-existing image/file clipboard is lost. Tracked as a
follow-up candidate.

### S5 — Unauthenticated llama-server sidecar (Low–Medium, accepted/deferred)

The spawned `llama-server` listens on `127.0.0.1:8321` with no auth; any local
process can use the loaded model, and the recognizer's "adopt an existing
server" path will send audio to whatever passes the health probe on that port.

Considered fix: spawn with `--api-key <random per-session token>`. Deferred
because it breaks two supported flows: (a) adopting a server that survived an
app crash (new session's token ≠ old server's token → port appears
conflicted until manually killed), and (b) user-managed external servers,
which are keyless by design. The exposure requires malicious code already
running as the user — at which point DPAPI secrets, hooks, and the clipboard
are equally available — so the added failure modes outweigh the marginal gain.
Revisit if the sidecar ever binds beyond loopback or handles secrets.

### S6 — Argument-string quoting (Low, fixed)

`ProcessStartInfo.Arguments` interpolation with hand-quoted settings-derived
paths breaks on embedded quotes. Switched to `ArgumentList` (correct escaping
delegated to the runtime). Primarily robustness; exploitability was limited to
the user misconfiguring their own machine.

### S7 — Non-atomic settings/secrets writes (Low, fixed)

`File.WriteAllTextAsync` on `settings.json`/`secrets.json` could leave a
truncated file on crash/power loss; both loaders silently fall back to an
empty store, so the user would lose API keys and settings without any notice.
Fixed with the temp-file + atomic-replace pattern already used for model
downloads.

### S8 — DPAPI scope & non-Windows fallback (Info, accepted)

`DataProtectionScope.CurrentUser` without extra entropy is the practical
ceiling for a no-prompt desktop app (entropy stored alongside the ciphertext
adds obfuscation, not security). On non-Windows platforms secrets are base64
plaintext with a one-time warning — already a documented ADR-043 deferred item
(OS keychain integration).

### S9 — Injection target window race (Info, accepted)

`Win32TargetWindowTracker` pastes into the last non-Parlotype foreground
window; if focus changes between hotkey release and paste (~200 ms), text can
land in the wrong app. Low likelihood, user-visible when it happens. Follow-up
candidate: compare the foreground window at injection time with the tracked
target and abort on mismatch.

## Checked and found sound

- llama-server binary downloads verified against GitHub-published SHA-256
  digests before extraction (`LlamaServerInstaller`, `GitHubLlamaServerCatalog`).
- Whisper model URLs hardcoded `https://huggingface.co/...`; no scheme or path
  injection from settings.
- API keys live only in `ISecretStore` (DPAPI at rest), never `settings.json`;
  the Authorization header is never logged (verified all call sites, and
  `CloudSpeechHttpError`'s contract states it).
- Cloud requests go directly to the configured provider — no Parlotype
  intermediary (matches ADR-032).
- Model downloads use an atomic temp-file pattern — a half-written model can't
  be loaded.
- No `HttpClient` certificate-validation overrides anywhere in the codebase.
- Settings values are parsed defensively (`Enum.TryParse`/`bool.TryParse` with
  defaults) — malformed `settings.json` cannot crash startup.

## Re-audit triggers

Re-run this audit (or a scoped delta) when: a new cloud provider or download
source is added; the sidecar binds to a non-loopback address; a
crash-reporting/telemetry pipeline is added (S1-class risk); secrets gain a
non-DPAPI backend; or an auto-update mechanism is introduced.
