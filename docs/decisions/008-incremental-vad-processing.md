---
status: accepted
date: 2026-03-01
---

# 008. Incremental VAD Processing

## Context

In batch recording mode, Silero VAD was called on the entire accumulated audio buffer every 100ms to check for end-of-speech. As the buffer grew, processing time increased linearly with total recording duration: O(total_buffer). A 60-second recording re-scanned all 960,000 samples every tick.

## Decision

Introduce a VAD cursor to process only new audio since the last call, achieving O(chunk_size) per tick.

Key changes:

- Added `_vadCursorPosition` field to SileroBatchVadService tracking how far VAD has processed
- Each VAD call processes only samples from cursor to current buffer end
- `CollectionsMarshal.AsSpan<float>()` provides zero-allocation access to the underlying List buffer
- VAD state (speech probability) carries forward naturally since Silero maintains internal hidden state
- Cursor resets to 0 when recording starts or buffer is cleared
- VadDecision tracks cumulative silence duration across incremental calls

Performance characteristics (from benchmarks):

- Before: ~170ms per VAD call at 60 seconds of audio (growing linearly)
- After: ~2ms per VAD call regardless of total audio length (constant)
- 85x improvement at 60 seconds, growing with longer recordings

## Consequences

- **Easier:** Long recordings no longer degrade in real-time responsiveness. VAD overhead is effectively constant.
- **Easier:** The incremental approach uses the same Silero model, no new dependencies.
- **Harder:** Cursor management adds state complexity. Must ensure cursor resets correctly on recording restart.
- **Harder:** Cannot re-evaluate old audio segments without rewinding the cursor (acceptable tradeoff since VAD only needs recent context for silence detection).
