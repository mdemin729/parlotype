#!/usr/bin/env python3
"""Builds the `long-audio-ladder` benchmark dataset.

Parlotype's shipped datasets top out at 15 s per sample, which cannot answer two
questions the push-to-talk rework raises (ADR pending):

1. How do decode time, RSS and accuracy scale with utterance length, per engine?
   That sets the buffer ceiling that replaces `MaxBatchBufferSamples`.
2. Does suppressing the mid-recording flush actually help, or does accumulated
   context stop paying off past some length?

Both need audio far longer than a LibriSpeech utterance, with *known* reference
text and *controlled* pause lengths -- the pause is what trips the silence flush,
so it has to be a dial, not an accident of the recording.

So we synthesise a duration ladder by concatenating LibriSpeech test-other
utterances separated by digital silence of a fixed length. Reference text is the
concatenation of the source references, which stays exact because concatenation
of speech is still just speech: no word is created or destroyed at a seam.

Two gap widths are emitted:
  * `gap12` (1.2 s) -- trips the 0.5 s and 1.0 s flush thresholds, not the 3 s one.
  * `gap35` (3.5 s) -- trips every threshold the UI offers, including Very Long.

Audio is written as 16 kHz mono 16-bit WAV (the pipeline's native format), so the
benchmark loader reads it directly with no FFmpeg conversion hop.

A second, narrower dataset pins an engine's *hard* decode limit:

    python datasets/build-long-audio-ladder.py --probe LOW HIGH STEP

That emits exact-length files into `long-audio-probe`. Run them with
`vad.enabled: false` so decoded length equals file length, one sample per
invocation (a native crash aborts the whole run). Only pass/fail is meaningful
there -- the reference text is a placeholder, so probe WER is noise. This is how
the sherpa-onnx Parakeet ceiling was located: 400 s decodes, 405 s throws
SEHException out of ONNX Runtime.

Usage:
    python datasets/build-long-audio-ladder.py
    python datasets/build-long-audio-ladder.py --probe 380 440 5
"""

from __future__ import annotations

import json
import struct
import subprocess
import sys
from pathlib import Path

SAMPLE_RATE = 16_000
BYTES_PER_SAMPLE = 2  # s16le
SILENCE_BYTE = b"\x00"

DATASETS = Path(__file__).parent
SOURCE = DATASETS / "libri-speech-test-other"
OUT = DATASETS / "long-audio-ladder"

# Ladder rungs in seconds. 15/30 straddle the current 30 s force-flush ceiling;
# 600 is well past any plausible push-to-talk hold and exists to expose
# superlinear cost curves, quality knees, or hard limits if the engine has them.
TARGET_SECONDS = [15, 30, 60, 120, 300, 600]

# (suffix, gap seconds, which rungs to build)
GAP_VARIANTS = [
    ("gap12", 1.2, TARGET_SECONDS),
    ("gap35", 3.5, [60, 300]),
]


def silence(seconds: float) -> bytes:
    """Returns `seconds` of digital silence as raw s16le PCM."""
    return SILENCE_BYTE * (int(seconds * SAMPLE_RATE) * BYTES_PER_SAMPLE)


def decode_to_pcm(path: Path) -> bytes:
    """Decodes any FFmpeg-readable file to raw 16 kHz mono s16le PCM."""
    result = subprocess.run(
        [
            "ffmpeg", "-v", "error",
            "-i", str(path),
            "-f", "s16le", "-acodec", "pcm_s16le",
            "-ar", str(SAMPLE_RATE), "-ac", "1",
            "-",
        ],
        capture_output=True,
        check=True,
    )
    return result.stdout


def write_wav(path: Path, pcm: bytes) -> None:
    """Writes raw s16le mono PCM as a canonical 44-byte-header WAV."""
    byte_rate = SAMPLE_RATE * BYTES_PER_SAMPLE
    header = b"".join([
        b"RIFF", struct.pack("<I", 36 + len(pcm)), b"WAVE",
        b"fmt ", struct.pack("<IHHIIHH", 16, 1, 1, SAMPLE_RATE, byte_rate, BYTES_PER_SAMPLE, 16),
        b"data", struct.pack("<I", len(pcm)),
    ])
    path.write_bytes(header + pcm)


def load_sources() -> list[tuple[dict, bytes]]:
    """Decodes every source utterance once; callers reuse them by cycling."""
    manifest_path = SOURCE / "manifest.json"
    if not manifest_path.exists():
        raise FileNotFoundError(f"source dataset not found at {manifest_path}")

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    print(f"Loaded {len(manifest['samples'])} source utterances from {manifest['name']}")
    print("Decoding source audio...")
    return [(s, decode_to_pcm(SOURCE / s["file"])) for s in manifest["samples"]]


def build_ladder() -> int:
    decoded = load_sources()
    (OUT / "samples").mkdir(parents=True, exist_ok=True)
    out_samples = []

    for suffix, gap_seconds, rungs in GAP_VARIANTS:
        gap_pcm = silence(gap_seconds)

        for target in rungs:
            target_bytes = target * SAMPLE_RATE * BYTES_PER_SAMPLE

            # Lead-in silence: a real push-to-talk hold starts before speech does,
            # and the VAD's leading-edge trim should have something to trim.
            chunks: list[bytes] = [gap_pcm]
            texts: list[str] = []
            total = len(gap_pcm)
            i = 0

            while total < target_bytes:
                meta, pcm = decoded[i % len(decoded)]
                chunks += [pcm, gap_pcm]
                texts.append(meta["referenceText"].strip())
                total += len(pcm) + len(gap_pcm)
                i += 1

            pcm_all = b"".join(chunks)
            sample_id = f"ladder-{target:03d}s-{suffix}"
            rel = f"samples/{sample_id}.wav"
            write_wav(OUT / rel, pcm_all)

            out_samples.append({
                "id": sample_id,
                "file": rel,
                "referenceText": " ".join(texts),
                "language": "en",
                "tags": ["long-audio", "synthetic", "librispeech", suffix, f"{target}s"],
            })
            actual = len(pcm_all) / (SAMPLE_RATE * BYTES_PER_SAMPLE)
            print(f"  {sample_id}: {actual:6.1f}s, {i} utterances, {len(' '.join(texts).split())} words")

    write_manifest(OUT, "long-audio-ladder", out_samples, (
        "Synthetic duration ladder built by concatenating LibriSpeech test-other "
        "utterances with fixed-length silence gaps. Measures how transcription cost "
        "and quality scale with utterance length, and how the pipeline's silence-flush "
        "threshold interacts with pause width. "
        "Generated by build-long-audio-ladder.py -- do not edit by hand."
    ))

    total_mb = sum((OUT / s["file"]).stat().st_size for s in out_samples) / (1024 * 1024)
    print(f"\nWrote {len(out_samples)} samples ({total_mb:.1f} MB) to {OUT}")
    return 0


def build_probe(low: int, high: int, step: int) -> int:
    """Emits exact-length audio for bisecting an engine's hard decode limit."""
    decoded = load_sources()
    out = DATASETS / "long-audio-probe"
    (out / "samples").mkdir(parents=True, exist_ok=True)

    # One oversized master, sliced to exact lengths: every probe is then a prefix
    # of the same audio, so a pass/fail difference can only come from length.
    gap_pcm = silence(0.3)
    chunks: list[bytes] = []
    total = 0
    i = 0
    while total < (high + 10) * SAMPLE_RATE * BYTES_PER_SAMPLE:
        _, pcm = decoded[i % len(decoded)]
        chunks += [pcm, gap_pcm]
        total += len(pcm) + len(gap_pcm)
        i += 1
    master = b"".join(chunks)

    samples = []
    for target in range(low, high + 1, step):
        sample_id = f"probe-{target}s"
        rel = f"samples/{sample_id}.wav"
        write_wav(out / rel, master[: target * SAMPLE_RATE * BYTES_PER_SAMPLE])
        samples.append({
            "id": sample_id,
            "file": rel,
            "referenceText": "placeholder",
            "language": "en",
            "tags": ["probe", "exact-length"],
        })
        print(f"  {sample_id}")

    write_manifest(out, "long-audio-probe", samples, (
        "Exact-length audio for pinning an engine's hard decode limit. Run with "
        "vad.enabled=false so decoded length equals file length, one sample per "
        "invocation. Only pass/fail is meaningful -- the reference text is a "
        "placeholder, so WER from a probe run is noise. "
        "Generated by build-long-audio-ladder.py --probe -- do not edit by hand."
    ))
    print(f"\nWrote {len(samples)} probes ({low}..{high}s step {step}) to {out}")
    return 0


def write_manifest(directory: Path, name: str, samples: list[dict], description: str) -> None:
    payload = {
        "name": name,
        "description": description,
        "language": "en",
        "samples": samples,
    }
    (directory / "manifest.json").write_text(
        json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )


def main() -> int:
    if len(sys.argv) > 1 and sys.argv[1] == "--probe":
        try:
            low, high, step = (int(a) for a in sys.argv[2:5])
        except ValueError:
            print("usage: build-long-audio-ladder.py --probe LOW HIGH STEP", file=sys.stderr)
            return 2
        return build_probe(low, high, step)

    return build_ladder()


if __name__ == "__main__":
    raise SystemExit(main())
