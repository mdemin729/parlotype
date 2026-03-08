---
title: Incremental VAD
status: in_progress
created: 2026-03-01
started: 2026-03-01
completed:
---

## Run VAD outside the lock / off the WASAPI thread

### Problem

Currently VAD inference runs under `lock(_sampleBuffer)` on the WASAPI
callback thread. Silero VAD with a 1 024-sample window is fast (~1 ms), but
batch mode re-scans the entire buffer each time, which grows with recording
duration.

### Options

**Option A — Dedicated VAD thread:**
Copy incoming samples into a lock-free ring buffer
(`System.Threading.Channels.Channel<float[]>`). A dedicated VAD consumer
reads chunks, runs detection, and posts speech segments to the existing
`_processingQueue`.

**Option B — Incremental VAD:**
Feed VAD only the _new_ samples since the last call and maintain state across
calls. This keeps VAD cost constant regardless of buffer size. The Silero
library supports stateful frame-by-frame processing.

## Task

Implement Option B.
