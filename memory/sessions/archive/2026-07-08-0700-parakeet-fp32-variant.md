---
title: "Session: Parakeet fp32 variant"
type: session
status: completed
tags: [parakeet, fp32, quantization, model-catalog]
created: 2026-07-08
summary: "Added the full-precision Parakeet TDT v3 as a second selectable catalog entry (ADR-041 amendment): ONNX external-data weights support, smoke WER 1.9 % vs INT8's 5.6 %."
---

# Session: Parakeet fp32 variant

## Active Focus

User asked which other Parakeet v3 quantizations exist, then chose to ship the
fp32 variant as a user-selectable option. Implemented in
`plans/2026-07-08-parakeet-fp32-variant/`.

## Decisions Made

- **fp32 from the same publisher** (`csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3`)
  — same encoder/decoder/joiner layout as INT8, so `ParakeetSpeechRecognizer`
  needed zero changes. Rejected `istupakov/parakeet-tdt-0.6b-v3-onnx` (merged
  decoder_joint file, onnx-asr layout — incompatible with sherpa's API) and the
  community SmoothQuant conversion (unproven third party)
- **No new settings window needed** — the ADR-041 Parakeet model section
  (Whisper/Gemma list pattern) shows the second entry automatically
- **INT8 stays default**; fp32 is opt-in for accuracy on accented speech
- `EncoderWeightsFileName` is an optional record param (null for INT8) rather
  than a generalized file list — keeps the four named roles explicit

## Facts Learned

Distilled into [[sherpa-onnx-quirks]] §5: the fp32 export is a 42 MB graph +
2.44 GB ONNX external-data file; onnxruntime resolves it by relative path next
to the graph — so downloads must place both in one directory and delete must
remove both.

## Measured (smoke set, 16-core CPU)

| Variant | WER | CER | RTF | Load | Peak RAM |
|---------|----:|----:|----:|-----:|---------:|
| INT8 (default) | 5.6 % | 2.5 % | 0.072 | 3.3 s | 918 MB |
| fp32 | 1.9 % | 1.1 % | 0.121 | 5.8 s | 2 635 MB |

The Russian-accented sample drives the gap (INT8's 16.7 % outlier).

## Open Blockers

None. 737 tests green, zero code warnings.

## Documentation Status

- ADR: done — amendment section in `041-parakeet-v3-sherpa-onnx.md` (no new
  ADR: same decision, extended catalog)
- Vault: done — core profile, decisions index row, knowledge note
- Knowledge: done — external-data quirk in [[sherpa-onnx-quirks]]

## Next Action

Still outstanding: manual in-app smoke test (fresh profile → record press →
download dialog → dictation; now also worth flipping to fp32 in Settings →
Parakeet model and confirming the 2.6 GB download + hot-swap works in the UI).
Consider whether the fp32 accuracy gain justifies re-evaluating the default
after more real-world dictation feedback.
