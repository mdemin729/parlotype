# Final results summary

All phases landed 2026-07-13 (commits `1ff6d9b` → `99d89a7` → `38e9f3a` → `7a2a3a0` + docs).
Full tables: [benchmarks-baseline.md](benchmarks-baseline.md), [benchmarks-after-phase1.md](benchmarks-after-phase1.md).

## Headline numbers (BenchmarkDotNet, MemoryDiagnoser)

| Hot path | Before | After | Delta |
|----------|--------|-------|-------|
| Capture callback buffer (100 ms @ 48 kHz stereo f32, ×100) | 15.4 MB alloc, Gen2 collections | **0 B** (ArrayPool) | −100 % alloc |
| WAV encode, 10 s utterance | 1,493 µs / 625 KB | **257 µs / 313 KB** | 5.8× faster, ½ alloc |
| Sample buffering, 30 s of 100 ms chunks | 1,449 µs / 4.20 MB | **385 µs / 1.92 MB** (pre-size + span AddRange) | 3.8× faster, −54 % alloc |
| Streaming window extraction | 247 µs / 1.15 MB | **115 µs / 0.58 MB** | 2.2× faster, ½ alloc |
| Parakeet utterance hand-off | full-utterance copy per transcription | zero-copy | (inspection + tests) |
| Utterance dispatch latency | up to 50 ms polling delay + 20 wakeups/s idle | event-driven (channels) | (design) |

## Security remediation status

See [docs/security/2026-07-11-security-audit.md](../../docs/security/2026-07-11-security-audit.md):
S1–S4, S6, S7 fixed with tests; S5 deferred with rationale; S8, S9 accepted.

## Test status at close

`dotnet build Parlotype.slnx`: 0 errors, 3 pre-existing AVLN5001 warnings
(older views, full-rebuild only — noted in the 2026-07-10 session note).
`dotnet test`: **870 passed / 0 failed** (463 core/platform + 297 desktop +
110 benchmark), 32 of them new this plan.

## Verification still pending (needs an interactive desktop session)

1. Live dictation with `dotnet-counters monitor` — confirm allocation-rate /
   Gen2 drop end-to-end (benchmarks + code inspection say yes; the plan's
   acceptance criterion 3 asks for the live numbers).
2. Win+V after dictation — injected text must not appear in clipboard
   history (S4; headless tests cannot exercise the Win32 clipboard).
3. Cloud providers settings page — inline red hint appears for an
   `http://` non-loopback base URL (VM logic is tested; the AXAML binding
   render is not).
4. `Parlotype.Benchmark` smoke run for WER/RTF regression — not run here
   (requires dataset/model downloads in this environment); pipeline
   segmentation thresholds and WavEncoder bytes are unchanged/identical by
   test, so risk is low.
