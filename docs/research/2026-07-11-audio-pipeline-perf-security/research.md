# Research: Audio Pipeline Performance + Security Audit

Analysis date: 2026-07-11. All line numbers refer to the current worktree
(branch `claude/audio-pipeline-analysis-237b47`, clean tree at eb54234).

Pipeline under analysis:

```
WASAPI capture (NAudio) → resample 16 kHz mono → AudioPipelineService
  → Silero VAD → segment extraction → recognizer (Parakeet / Whisper / llama.cpp / cloud)
  → TranscriptionTextProcessor → text injection (clipboard paste)
```

---

## 1. Performance / allocation findings

Ordered by estimated impact. "LOH" = Large Object Heap (objects ≥ 85,000 bytes;
collected only with expensive gen2 GCs, prone to fragmentation).

### P1 — Capture callback allocates an oversized `float[]` every callback (HIGH)

`WasapiAudioCaptureService.OnCaptureDataAvailable`,
`src/Parlotype.Platform/Audio/WasapiAudioCaptureService.cs:105`:

```csharp
var floatBuffer = new float[e.BytesRecorded]; // oversize is fine
```

`e.BytesRecorded` counts **bytes of the device-native format** but is used as a
**float count**, so the allocation is 4× the byte size. WASAPI shared mode is
typically IEEE-float32 stereo 48 kHz (384,000 B/s): a 50–100 ms callback carries
19,200–38,400 bytes → allocates a 76.8–153.6 KB `float[]`, crossing the LOH
threshold at the 100 ms cadence. The actual resampled 16 kHz mono output for
that window is ~800–1,600 floats (≈ 3–6 KB) — the buffer is ~24× oversized.
Net effect: **~1.5 MB/s of garbage, much of it LOH, for the entire duration of
every recording.**

**Fix:** rent from `ArrayPool<float>.Shared` (or hold one persistent buffer —
NAudio raises `DataAvailable` sequentially from its capture thread) and return
it after raising the event. The only subscriber
(`AudioPipelineService.OnAudioDataAvailable`) copies out of
`AudioDataEventArgs.Buffer` synchronously, so the lifetime contract "buffer is
only valid during the event" must be documented on
`IAudioCaptureService.DataAvailable` / `AudioDataEventArgs` in Core. Sizing can
stay conservative (`BytesRecorded` floats) — pooled oversize costs nothing
after the first rent.

### P2 — Sample accumulation is a per-sample `List<float>.Add` loop (MEDIUM)

`AudioPipelineService.OnAudioDataAvailable`,
`src/Parlotype.Platform/Audio/AudioPipelineService.cs:183`:

```csharp
foreach (var s in floatSamples)
    _sampleBuffer.Add(s);
```

16,000 `Add` calls/second (bounds check + version bump each) while the capture
thread holds the buffer lock. The list also grows by doubling up to
`MaxBatchBufferSamples` (480,000 floats → 1.92 MB backing array on the LOH);
`Clear()` keeps capacity so growth is one-time per session, but the staged
doubling copies ~2× the final size in aggregate on first use.

**Fix:** `_sampleBuffer.EnsureCapacity(MaxBatchBufferSamples)` at `StartAsync`,
and bulk-append with the .NET span overload `_sampleBuffer.AddRange(floatSamples)`
(`List<T>.AddRange(ReadOnlySpan<T>)`) — one vectorized copy instead of 16 k
calls/s.

### P3 — Streaming mode copies each window twice (MEDIUM, streaming mode only)

`AudioPipelineService.ProcessStreaming`,
`src/Parlotype.Platform/Audio/AudioPipelineService.cs:283`:

```csharp
var window = _sampleBuffer.GetRange(0, StreamingWindowSamples).ToArray();
```

`GetRange` allocates an intermediate `List<float>` (192 KB backing, LOH) and
`ToArray` copies again — 384 KB allocated per 3-second window where 192 KB
would do. `RemoveRange(0, N)` then shifts the remainder (O(n) but acceptable).

**Fix:** single copy via `CollectionsMarshal.AsSpan(_sampleBuffer)[..StreamingWindowSamples].ToArray()`.

### P4 — Parakeet re-copies the whole utterance (MEDIUM, default engine)

`ParakeetSpeechRecognizer.TranscribeAsync`,
`src/Parlotype.Platform/Speech/ParakeetSpeechRecognizer.cs:130`:

```csharp
stream.AcceptWaveform(SampleRate, samples.ToArray());
```

`ExtractSpeechSamples` already produced a dedicated `float[]` for the utterance;
`ToArray()` duplicates it (10 s of speech = 640 KB → LOH) on every
transcription, on the default engine.

**Fix:** `MemoryMarshal.TryGetArray(samples, out var seg)` and pass
`seg.Array` directly when the memory spans the whole array (always true for
pipeline-produced buffers); fall back to `ToArray()` otherwise.

### P5 — WavEncoder: per-sample `BinaryWriter` writes + duplicate buffer (MEDIUM, cloud + llama.cpp paths)

`src/Parlotype.Platform/Speech/WavEncoder.cs`. Three costs per utterance:

1. `bw.Write((short)…)` per sample → a virtual 2-byte stream write 16,000×/s
   of audio.
2. `MemoryStream` created with exact capacity, but `ms.ToArray()` at the end
   duplicates the entire WAV (10 s utterance: 2 × ~320 KB, both LOH).
3. Scalar clamp/scale loop.

**Fix:** compute exact size `44 + samples.Length * 2`, allocate one `byte[]`,
write the header with `BinaryPrimitives`, convert samples into
`MemoryMarshal.Cast<byte, short>(span)` in a tight loop. Output must remain
byte-identical (assert in tests). Used by `OpenAiCompatibleSpeechRecognizer`,
`XaiGrokSpeechRecognizer`, and `LlamaCppSpeechRecognizer`.

### P6 — Transcription loop polls with `Task.Delay(50)` (LOW-MEDIUM)

`AudioPipelineService.ProcessQueueAsync`,
`src/Parlotype.Platform/Audio/AudioPipelineService.cs:397`. Adds up to 50 ms
latency before transcription of each utterance begins and wakes the thread pool
20×/s even when idle (Parlotype is a resident tray app — idle cost matters).

**Fix:** replace `ConcurrentQueue<float[]>` + polling with a single-reader
`Channel<float[]>`; `await foreach (reader.ReadAllAsync(...))` gives immediate
wakeup and a natural drain-on-complete shutdown (writer completed in
`StopAsync`), removing the cancellation-token dance.

### P7 — VAD inference + segment extraction run on the audio capture thread (MEDIUM, robustness)

`OnAudioDataAvailable` → `ProcessBatch` runs Silero VAD inference (every
≥ 500 ms of new audio) and segment extraction **inside the buffer lock on
NAudio's capture callback thread**
(`src/Parlotype.Platform/Audio/AudioPipelineService.cs:181-195`). If
VAD + extraction ever exceed the WASAPI buffer duration,
`BufferedWaveProvider.DiscardOnBufferOverflow = true`
(`WasapiAudioCaptureService.cs:65`) **silently drops audio**. Works today on
fast machines; it is a latent quality bug on slow ones and couples capture
cadence to inference time.

**Fix (structural, Phase 2):** capture handler only copies samples into a
channel; a dedicated segmenter task owns the buffer and runs VAD/extraction;
transcription remains on its own task. Three stages, each single-threaded, no
shared-buffer lock.

### P8 — Minor / optional

| Item | Location | Note |
|------|----------|------|
| `new AudioLevelEventArgs` per callback | `AudioPipelineService.cs:212` | ~10–20 alloc/s, tiny; fold into P1 change if convenient |
| RMS scalar loop | `AudioPipelineService.cs:203-207` | `Vector<float>` SIMD is a nice-to-have; measure first |
| `timestamps.Select(...).ToList()` | `SileroVadService.cs:36` | small per-VAD-call allocs; clean up in passing |
| `string.Join` + LINQ in Whisper result assembly | `WhisperSpeechRecognizer.cs:214-218` | negligible next to inference — **deliberately not touching** |
| `SpeechSegmentExtractor.Extract` output array | `SpeechSegmentExtractor.cs:40` | the utterance buffer crosses threads and outlives the call; keep as a plain allocation |

### Benchmark strategy

BenchmarkDotNet is not currently referenced anywhere in the solution. Add
`src/Parlotype.MicroBenchmarks` (console, net10.0, `MemoryDiagnoser`,
referencing Core + Platform; excluded from `dotnet test`):

| Benchmark | Measures | Covers |
|-----------|----------|--------|
| `WavEncoderBenchmarks` | current vs rewritten encoder, 1 s / 10 s / 30 s buffers | P5 |
| `SampleBufferingBenchmarks` | per-sample `Add` vs span `AddRange` (+ pre-sized) over simulated 30 s of 100 ms chunks | P2 |
| `StreamingWindowBenchmarks` | `GetRange().ToArray()` vs span slice copy | P3 |
| `CaptureBufferBenchmarks` | `new float[n]` vs `ArrayPool` rent/return at callback sizes | P1 (synthetic) |
| `RmsBenchmarks` (optional) | scalar vs `Vector<float>` | P8 |

P4/P6/P7 are not micro-benchmarkable in isolation; they are verified by
(a) `dotnet-counters monitor` GC allocation rate during a live 60 s dictation
before/after, and (b) the existing `Parlotype.Benchmark` smoke config for
WER/RTF regression.

---

## 2. Security audit findings

Threat model: single-user desktop app; assets are (1) the user's voice/dictated
text — the product's core privacy promise ("audio never leaves the machine in
local mode"), (2) BYOK cloud API keys, (3) integrity of downloaded native
models/binaries that get loaded into the process. In scope: everything in
`src/`. Out of scope: third-party model quality, OS-level compromise by
same-user malware (noted where it lowers a severity).

| # | Severity | Finding | Location |
|---|----------|---------|----------|
| S1 | **High (privacy)** | Transcribed text persisted to plaintext rolling logs | `AudioPipelineService.cs:369`, `App.axaml.cs:176` |
| S2 | **High (supply chain)** | Model downloads never integrity-checked | `HttpModelDownloadService.cs`, `ParakeetModelDownloadService.cs`, `Gemma4ModelDownloadService.cs` |
| S3 | Medium | Cloud base URL: no HTTPS enforcement — Bearer key + audio over plaintext if misconfigured | `OpenAiCompatibleSpeechRecognizer.cs:79` |
| S4 | Medium (privacy) | Injected text enters Windows Clipboard History / cross-device Cloud Clipboard | `ClipboardTextInjectionService.cs:124` |
| S5 | Low-Medium | llama-server sidecar unauthenticated on 127.0.0.1; adoption path trusts any listener on the port | `LlamaCppSpeechRecognizer.cs:79-104` |
| S6 | Low | llama-server args built by string concatenation of settings-derived paths | `LlamaCppSpeechRecognizer.cs:132` |
| S7 | Low | settings.json / secrets.json written non-atomically; corrupt file silently resets (secret/settings loss) | `DpapiSecretStore.cs:177`, `JsonFileStore.cs:92` |
| S8 | Info (accepted) | DPAPI CurrentUser scope: any same-user process can decrypt; non-Windows fallback is base64 plaintext (known, ADR-043 deferred) | `DpapiSecretStore.cs` |
| S9 | Info | Injection targets "last non-Parlotype foreground window" — text can land in an unintended window if focus changes | `Win32TargetWindowTracker` |

### S1 — Transcripts in plaintext logs (High)

`logging.SetMinimumLevel(LogLevel.Debug)` is the production default
(`src/Parlotype.Desktop/App.axaml.cs:176`) and both sinks (console + rolling
file in `%LOCALAPPDATA%/parlotype/logs/`, 10 MB × daily) receive Debug.
`AudioPipelineService.ProcessQueueAsync` logs the full transcription:

```csharp
_logger.LogDebug("Transcription result: {Text}", result.Text);   // :369
```

Dictated text routinely contains names, addresses, messages, occasionally
passwords. For a product whose brand is "your speech stays private", persisting
every transcript unencrypted to disk (surviving in rolling files, backups,
crash dumps shared for support) contradicts the positioning. Related:
`CloudSpeechHttpError.BuildAsync` logs full provider response bodies at
Warning (`CloudSpeechHttpError.cs:32`) — error envelopes are usually benign but
provider-controlled; cap and keep.

**Remediation:** never log transcript content at any level — log lengths,
durations, segment counts instead; sweep all `{Text}`-style log sites
(pipeline, view models, recognizers); raise the rolling-file sink minimum to
`Information` (keep console Debug for dev if desired). Decision needed
(task.md Q2): plain removal vs opt-in verbose-diagnostics setting.

### S2 — No integrity verification on model downloads (High)

- **Whisper:** `WhisperModelInfo.Sha` exists in the catalog
  (`src/Parlotype.Core/Speech/WhisperModelInfo.cs:8`) but is *never checked* —
  `HttpModelDownloadService.DownloadModelAsync` streams to disk and moves into
  place, no hash. The metadata being present suggests verification was always
  intended.
- **Parakeet (default engine, silent auto-download per ADR-042):**
  `ParakeetModelInfo` has no hash fields at all; 4 files (~670 MB–2.6 GB)
  fetched from HuggingFace unverified.
- **Gemma 4 GGUF:** same pattern, unverified.
- **Contrast:** `LlamaServerInstaller` *does* verify SHA-256 of downloaded
  llama-server archives (`LlamaServerInstaller.cs:82,230`) — precedent and
  helper code exist in-repo.

HTTPS protects transit; it does not protect against a compromised upstream
repo/CDN account or a tampered local cache file being loaded into the process
as native-parsed model data (ONNX/GGML parsers are a real attack surface).

**Remediation:** post-download SHA-256 verify before the atomic move (extend
`StreamingFileDownloader` or verify in callers), fail closed with an actionable
dialog; wire Whisper's existing `Sha` values; add hash fields + known values to
`ParakeetModelInfo` and the Gemma catalog. Optionally re-verify cached files
once per model load (cheap for ≤ 3 GB files relative to load time; decide in
implementation).

### S3 — Cloud base URL accepts any scheme (Medium)

`OpenAiCompatibleSpeechRecognizer.InitializeAsync` uses the settings string
verbatim (`:79`); `CloudProviderSettingsViewModel` performs no validation.
An `http://` URL (typo'd, or edited into the plaintext, unsigned
`settings.json` by anything running as the user) sends the Bearer API key and
recorded audio in cleartext.

**Remediation:** validate with `Uri.TryCreate` at save time (inline UI hint)
and again at init time (throw `CloudProviderNotConfiguredException`-style
actionable error): require `https` unless the host is loopback (permits local
OpenAI-compatible servers like LM Studio/llama.cpp on `http://localhost`).
Apply to both OpenAI-compatible and xAI Grok recognizers.

### S4 — Clipboard injection leaks into history / cloud sync (Medium, privacy)

`ClipboardTextInjectionService.SetClipboardText` writes bare `CF_UNICODETEXT`.
Windows Clipboard History (Win+V) and Cloud Clipboard sync will happily retain
and sync every dictated utterance — silently undoing the local-only promise on
machines with those features enabled.

**Remediation:** additionally register and set the standard exclusion formats
when injecting: `ExcludeClipboardContentFromMonitorProcessing`,
`CanIncludeInClipboardHistory` = 0, `CanUploadToCloudClipboard` = 0
(RegisterClipboardFormat + a 4-byte DWORD payload).
Adjacent robustness note: the restore path only preserves *text* — a non-text
clipboard (image, files) present before injection is destroyed
(`RestoreClipboard` → empty). Documented; fixing full-format save/restore is
out of scope here.

### S5–S7 — Hardening batch (Low)

- **S5:** `llama-server` runs unauthenticated on `127.0.0.1:8321`; any local
  process can use the model, and the "adopt existing server" path
  (`LlamaCppSpeechRecognizer.cs:88`) would hand base64 audio to whatever
  answers the health probe on that port. Same-user boundary → low severity.
  Cheap fix: spawn with `--api-key <random per-session token>` and send it;
  adoption of *externally started* servers stays keyless by design (document).
  Decision needed (task.md Q3).
- **S6:** `ProcessStartInfo.Arguments` string interpolation with quoted paths
  breaks on paths containing `"` — use `ArgumentList` (correctness ≥ security;
  paths are user-controlled local settings).
- **S7:** `File.WriteAllTextAsync` for settings.json/secrets.json is not
  atomic; a crash mid-write corrupts the file and both loaders silently fall
  back to empty (user loses API keys/settings without notice). Use the
  temp-file + `File.Replace`/`File.Move` pattern already used by
  `StreamingFileDownloader`.

### S8–S9 — Documented / accepted

- **S8:** DPAPI `CurrentUser` without extra entropy is the practical ceiling on
  Windows for a no-prompt app (entropy stored alongside adds nothing); the
  non-Windows base64 fallback already logs a warning and is an ADR-043 deferred
  item (OS keychain integration). Record both in the audit report as accepted.
- **S9:** text injection targets the last non-Parlotype foreground window; if
  focus changes between hotkey release and paste, text lands elsewhere
  (possibly a chat box). Optional guard — re-check the foreground window at
  injection time — recorded as a follow-up candidate, not in scope.

### Things checked and found sound

- llama-server binary downloads: SHA-256 digests from the GitHub API verified
  before extraction (`LlamaServerInstaller`, `GitHubLlamaServerCatalog`).
- Whisper model URLs hardcoded to `https://huggingface.co/...` (no scheme
  injection via settings).
- API keys: stored only via `ISecretStore` (DPAPI), never in `settings.json`;
  Authorization header never logged (`CloudSpeechHttpError` contract states it,
  and no call site passes it).
- Cloud requests go directly to the configured provider — no intermediary
  server (matches ADR-032 claim).
- Atomic temp-file pattern for model downloads prevents half-written model
  files being loaded.
- `HttpClient` default certificate validation untouched anywhere (no
  `DangerousAcceptAnyServerCertificateValidator`).
