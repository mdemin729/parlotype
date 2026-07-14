# Implementation Plan: Audio Pipeline Performance + Security Audit

Phases are ordered so each lands independently green (build, tests, benchmark
run). Finding IDs (P1…P8, S1…S9) refer to [research.md](research.md).

---

## Phase 0 — Benchmark scaffolding + baselines

**Goal:** measurement infrastructure exists *before* any optimization, and the
current numbers are recorded.

1. New console project `src/Parlotype.MicroBenchmarks` (net10.0):
   - Packages: `BenchmarkDotNet` (MemoryDiagnoser on every benchmark class).
   - References: `Parlotype.Core`, `Parlotype.Platform`.
   - Added to `Parlotype.slnx`; **not** a test project (no xUnit), so
     `dotnet test` is unaffected. Run manually:
     `dotnet run -c Release --project src/Parlotype.MicroBenchmarks -- --filter *`.
   - `EnableCuda=false`-friendly: benchmarks must not touch GPU runtimes.
2. Baseline benchmarks (current code, no product changes):
   - `WavEncoderBenchmarks` — 1 s / 10 s / 30 s of 16 kHz float samples.
   - `SampleBufferingBenchmarks` — 30 s of simulated 100 ms capture chunks:
     per-sample `Add` (current) vs span `AddRange` vs pre-sized + `AddRange`.
   - `StreamingWindowBenchmarks` — `GetRange().ToArray()` vs span slice copy.
   - `CaptureBufferBenchmarks` — `new float[38400]` vs `ArrayPool` rent/return.
   - Deterministic synthetic audio (seeded sine + noise), no model loads.
3. Record results to `plans/2026-07-11-audio-pipeline-perf-security/benchmarks-baseline.md`.
4. **ADR:** new project + BenchmarkDotNet dependency (trigger: new `.csproj`
   dependency). Next free number after 043 at time of writing.

**Exit:** solution builds zero-warning; baseline table committed.

---

## Phase 1 — Allocation fixes (low risk, behavior-preserving)

Order within the phase = independent commits, each benchmarked.

### 1.1 Capture callback pooling (P1)
- `WasapiAudioCaptureService.OnCaptureDataAvailable`: rent
  `ArrayPool<float>.Shared` buffer (size `e.BytesRecorded` — oversize is free
  once pooled), return in `finally` after `DataAvailable` dispatch.
- Document the lifetime contract on `Parlotype.Core.Audio.AudioDataEventArgs`
  (`Buffer` valid only during the event; subscribers must copy synchronously) —
  XML doc change in Core, no signature change.
- Fold P8a in: reuse a mutable `AudioLevelEventArgs` is *not* worth API risk —
  instead skip RMS event allocation when `LevelChanged` has no subscribers.
- Tests: existing `Parlotype.Tests` capture/pipeline tests must stay green; add
  a test asserting samples survive the event dispatch copy (mock subscriber
  stores contents, then buffer is mutated → stored copy unaffected).

### 1.2 WavEncoder rewrite (P5)
- Exact-size `byte[44 + n*2]`, header via `BinaryPrimitives`, sample loop
  writing into `MemoryMarshal.Cast<byte, short>` (little-endian fast path;
  `BinaryPrimitives.WriteInt16LittleEndian` fallback keeps big-endian
  correctness).
- Tests: byte-identical output vs the old implementation for edge cases
  (empty, 1 sample, clipping values ±1.5, exact ±1.0, 10 s buffer) — keep the
  old code in the test as a reference oracle or use golden bytes.

### 1.3 Parakeet zero-copy (P4)
- `MemoryMarshal.TryGetArray(samples, out ArraySegment<float> seg)`; pass
  `seg.Array!` when `Offset == 0 && Count == Array.Length`, else `ToArray()`.
- Test: transcription path exercised via existing recognizer tests; add a unit
  test on the helper if extracted.

### 1.4 Streaming single-copy + buffer pre-size (P3, P2)
- `ProcessStreaming`: `CollectionsMarshal.AsSpan(_sampleBuffer)[..N].ToArray()`
  then `RemoveRange`.
- `StartAsync`: `_sampleBuffer.EnsureCapacity(MaxBatchBufferSamples)`.
- `OnAudioDataAvailable`: replace the per-sample loop with
  `_sampleBuffer.AddRange(floatSamples)` (span overload).
- Tests: existing `AudioPipelineTests` (batch + streaming) green.

### 1.5 Measure & document
- Re-run Phase 0 benchmarks → `benchmarks-after-phase1.md` with deltas.
- Live check: `dotnet-counters monitor` (alloc rate, gen0/gen2 counts, LOH
  size) during a 60 s dictation, before vs after; numbers into the results doc.
- `Parlotype.Benchmark` smoke run (`datasets/smoke-test-config.json`) — WER
  identical, RTF within noise.
- **ADR:** audio-pipeline allocation changes (trigger: touches audio pipeline).

**Exit:** acceptance criteria 1–4 of task.md met for the Phase 1 subset.

---

## Phase 2 — Threading rework (medium risk; go/no-go after Phase 1 review)

**Goal:** capture thread does nothing but copy; no polling loops (P6, P7).

1. Replace `ConcurrentQueue<float[]>` + `Task.Delay(50)` polling with
   `Channel<float[]>` (unbounded, `SingleReader = true`):
   - `StopAsync` completes the writer; reader drains naturally; keep the 30 s
     drain timeout and the "in-flight transcription completes with
     `CancellationToken.None`" behavior.
2. Introduce a raw-sample channel between capture callback and a new segmenter
   loop:
   - `OnAudioDataAvailable`: RMS publish + copy chunk into a pooled buffer +
     `TryWrite` (drop-oldest policy discussion: unbounded is fine — 16 kHz mono
     float is 64 KB/s).
   - Segmenter task owns `_sampleBuffer`, `_vadProcessedUpTo`,
     `_accumulatedSegments` — the `lock (_sampleBuffer)` disappears (single
     owner), `FlushBuffer` becomes a message/drain step in the segmenter.
   - VAD cadence and all thresholds (`VadMinChunkSamples`,
     `_silenceThresholdSamples`, `MaxBatchBufferSamples`,
     `SegmentMergeTolerance`) unchanged — behavior-identical segmentation, just
     on a different thread.
3. Tests: extend `AudioPipelineTests` — ordering of multiple utterances,
   drain-on-stop completeness (all queued audio transcribed), stop-during-VAD,
   cancellation during transcription, no events after stop. Race-condition
   style tests mirroring the existing TranscribeViewModel race coverage.
4. **ADR:** pipeline threading model (same ADR as 1.5 or a dedicated one —
   decide by size).

**Exit:** identical transcription output on the smoke benchmark; capture
callback measured < 1 ms worst case (log-based or benchmark harness).

---

## Phase 3 — Security audit report + remediations

### 3.1 Audit report (no code)
- `docs/security/2026-07-11-security-audit.md`: threat model, findings S1–S9
  with severity + disposition (fixed in this plan / accepted / deferred with
  pointer), and the "checked and sound" list from research.md §2.

### 3.2 S1 — Log hygiene (High)
- Remove transcript content from all log sites (`AudioPipelineService:369` and
  a repo-wide sweep for `{Text}` / `result.Text` in log calls, incl.
  Desktop view models and Benchmark console output stays as-is — it's a dev
  tool). Log `{Length} chars` / durations instead.
- Rolling-file sink minimum level → `Information`; console stays `Debug`.
- Cap logged cloud error bodies (`CloudSpeechHttpError`) to the existing 500
  char trim *before* logging, not only in the exception message.
- Depending on Q2 answer: optional `SettingsKeys.VerboseDiagnostics` gate
  instead of removal (default off, UI warning that transcripts hit disk).
- Test: a log-capture test asserting the pipeline path emits no transcript
  content.

### 3.3 S2 — Download integrity (High)
- `StreamingFileDownloader.DownloadAsync` gains optional `expectedSha256`
  parameter: hash while streaming (`IncrementalHash`), compare before the
  atomic move, delete + throw typed `ModelIntegrityException` on mismatch.
- Wire `WhisperModelInfo.Sha` through `HttpModelDownloadService` (audit that
  the catalog values match current upstream files first — if stale, refresh
  them from HuggingFace metadata and note in ADR).
- `ParakeetModelInfo`: add per-file SHA-256 (new record field + catalog values
  computed from the official HF files); verify in
  `ParakeetModelDownloadService`. Same for the Gemma 4 catalog.
- Desktop: mismatch surfaces via the existing download-dialog error path with
  retry.
- Tests: temp-file downloads with correct/wrong hashes (pattern exists in
  `LlamaServerInstallerTests`).

### 3.4 S3 — Cloud base URL validation (Medium)
- Shared validator in Core (e.g. `CloudBaseUrlValidator`): `https` required
  unless loopback host; used by both recognizers at init (throw actionable
  config exception) and by `CloudProviderSettingsViewModel` at save (inline
  error string).
- Tests: recognizer init with `http://api.example.com` → typed failure;
  `http://localhost:1234/v1` → allowed; VM save-path validation.

### 3.5 S4 — Clipboard exclusion formats (Medium)
- `ClipboardTextInjectionService.SetClipboardText`: after setting
  `CF_UNICODETEXT`, set `ExcludeClipboardContentFromMonitorProcessing`,
  `CanIncludeInClipboardHistory` (DWORD 0), `CanUploadToCloudClipboard`
  (DWORD 0) via `RegisterClipboardFormat`. Restore path unchanged (restored
  *original* content must NOT carry exclusion flags).
- Headless tests can't exercise Win32 clipboard — unit-test the format-id
  plumbing where possible, verify manually on a live run (Win+V shows no
  entry). Manual step recorded in the verification notes.

### 3.6 S5–S7 — Hardening batch (Low)
- S6: `ProcessStartInfo.ArgumentList` in `LlamaCppSpeechRecognizer`.
- S7: atomic write helper (temp + `File.Replace`) used by `JsonFileStore` and
  `DpapiSecretStore`; tests for crash-simulated partial temp file.
- S5 (if Q3 = yes): random per-session token passed as `--api-key` + sent as
  Bearer by the recognizer; adopted external servers remain keyless
  (documented).
- **ADR:** security hardening batch (triggers: settings subsystem, audio
  subsystem, Whisper subsystem, new Core symbols).

**Exit:** acceptance criteria 5–6 of task.md.

---

## Phase 4 — Documentation & memory vault

- ADRs finalized and numbered (044+; exact numbers assigned at merge time).
- `memory/services/platform.md`, `memory/services/core.md`: new/changed
  symbols (`ModelIntegrityException`, validator, benchmark project entry in
  `memory/services/_index.md`).
- `memory/architecture/audio-pipeline.md` + `subsystems.md`: updated data flow
  (channels, pooling) if Phase 2 shipped.
- `memory/knowledge/`: non-derivable facts learned (e.g. "NAudio
  `BytesRecorded` is bytes of the *native* format — sizing float buffers from
  it over-allocates 4×"; clipboard exclusion format names).
- `plans/INDEX.md` row moved per workflow; session note in `memory/sessions/`.
- Final `benchmarks-final.md` consolidating before/after tables.

---

## Risks & mitigations

| Risk | Mitigation |
|------|-----------|
| Pooled capture buffer read after return (use-after-free style bug) | Lifetime documented on the Core contract; test asserting subscriber copies; only one subscriber exists today |
| Phase 2 changes utterance segmentation timing | Thresholds/cadence unchanged; smoke benchmark WER must be identical; extensive drain/ordering tests |
| Stale SHA values in `WhisperModelInfo` break downloads for everyone | Verify catalog hashes against upstream *before* enabling enforcement; typed exception + dialog with retry |
| HTTPS enforcement breaks an existing self-hosted setup | Loopback exemption; init-time error message names the exact setting to change |
| BenchmarkDotNet results noisy on dev machine | MemoryDiagnoser allocation columns are deterministic — treat allocation deltas as the primary signal, time as secondary |
