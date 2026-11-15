---
status: accepted
date: 2026-11-14
amends: [007, 029, 041, 046, 073]
---

# 076. Model downloads are pinned to an upstream commit

## Context

Every model the app downloads comes from a Hugging Face repo and is checked against a SHA-256
in the catalog ([ADR-046](046-security-hardening-batch.md)). The URLs named a branch:
`resolve/main/{file}` for Parakeet, Gemma 4 and S1-mini, `resolve/v3/classic/{file}` for
Whisper. ADR-046 accepted the consequence: when upstream changes a file, the download fails
closed and someone refreshes the digest.

That consequence arrived, twice, and it is worse than a stale digest:

- **Parakeet.** On 2026-11-07 the sherpa-onnx export workflow ran again and pushed new
  `encoder`, `decoder` and `joiner` graphs to `main` of both
  `csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` and `…-v3`, under the same file
  names, with the commit message `first commit`. Every new download of the default engine has
  failed with `ModelIntegrityException` since then, on every platform.
- **Gemma 4.** On 2026-07-16 the `ggml-org/gemma-4-E2B-it-GGUF` and `…-E4B-it-GGUF` repos
  renamed `…-bf16.gguf` to `…-BF16.gguf`, deleted `gemma-4-E4B-it-Q4_K_M.gguf` (the default
  entry) and re-converted `Q8_0`. Five of the seven catalog files now return 404 on `main`,
  and the other two fail the integrity check. No catalog entry has been downloadable since.

Whisper (`sandrohanea/whisper.net`, branch `v3`, unchanged since 2024-10-07) and S1-mini
(`superwhisper/s1-mini-GGUF`) still match, but nothing keeps them that way: `v3` is a branch,
not a tag.

A digest answers "are these the bytes we chose", and a branch name cannot answer "give me the
bytes we chose". The two halves of the rule pointed at different things.

Refreshing the Parakeet digests to the new files would also change the model under every
measurement in `docs/research/` with no evidence the new export is as good. Upstream says
nothing about it: the commit message is empty, the model card added in the same commit
reports numbers measured before the re-export, and the export toolchain changed again four
days later (onnx 1.17 to 1.23, onnxruntime 1.17 to 1.31), so the next run will rewrite `main`
again. The fp32 `encoder.weights` file is byte-identical across the change, so the
weights are the same checkpoint; what differs is the exported graphs and the int8
quantisation, which is exactly the part that can move accuracy.

## Decision

- **A model download URL names a full commit SHA, never a branch or tag.** Core's
  `HuggingFaceUrl.Resolve(repo, revision, path)` builds every one and throws on a revision
  that is not 40 lowercase hex characters, so `main` cannot come back by accident.
- **The revision lives in the catalog next to the digests it belongs to**:
  `ParakeetModelInfo.HuggingFaceRevision`, `Gemma4ModelInfo.HuggingFaceRevision`,
  `SpeechCleanupModelInfo.HuggingFaceRevision`, and `WhisperModelInfo.HuggingFaceRevision`
  with `HuggingFaceRepo` (one repo for all Whisper models). The catalog entry builds its own
  URL (`GetDownloadUrl` / `DownloadUrl`); the download services no longer compose one.
- **Pinned revisions** are the last upstream commits whose files match the digests the catalog
  already carried, so no digest and no file name changes:

  | Catalog | Repo | Commit | Date |
  |---|---|---|---|
  | Parakeet int8 | `csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` | `2bda32ec70b097a55adaa07d9a7173915b43cc78` | 2025-08-16 |
  | Parakeet fp32 | `csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3` | `1a468a35cbba69418f126de829e75261dea4a4e4` | 2025-08-16 |
  | Gemma 4 E2B | `ggml-org/gemma-4-E2B-it-GGUF` | `a1dac71d3ab220618f5a7573a52acdc4baf3ae3b` | 2026-04-12 |
  | Gemma 4 E4B | `ggml-org/gemma-4-E4B-it-GGUF` | `2714b5519c6c3516b1000e7c5e1eba998dfe1fe8` | 2026-04-12 |
  | S1-mini | `superwhisper/s1-mini-GGUF` | `34add00a48a2e5d24e5a4ee5405a99620a3a240c` | 2026-08-28 |
  | Whisper | `sandrohanea/whisper.net` | `a28c1f379c0359332f8d101184249cba28b1ff53` | 2024-10-07 |

- **Moving to newer upstream files is a deliberate change**: a new revision and new digests in
  one commit, after a benchmark run shows the new files are at least as good. For Parakeet
  that comparison has not been made (it cannot run on the machine this was written on), so the
  2025-08-16 export stays. For Gemma 4 the newer files are also a different set of
  quantisations (`Q4_0` instead of `Q4_K_M`), which is a catalog decision of its own.

This amends the URL shapes in ADR-007 and ADR-029, the download source in ADR-041 and
ADR-073, and the "digests must be updated whenever upstream model files change" consequence
of ADR-046. Verification itself (fail closed on mismatch, warn on a missing digest) is
unchanged.

## Consequences

- New Parakeet and Gemma 4 downloads work again, with the files the benchmarks were measured
  on. Installs that already have their models are untouched: cached files are not
  re-downloaded or re-verified.
- An upstream rewrite of `main` no longer reaches users. A digest mismatch now means the bytes
  at a fixed commit changed, which is corruption or tampering, not routine upstream activity.
- The app does not pick up upstream improvements on its own. That is intended, and it is now
  somebody's job to notice them.
- A pinned commit lives only as long as upstream keeps its history. If a repo owner squashes
  or deletes it, the download fails with 404, again for new downloads only. The remedy then is
  a mirror we control, which would be a new download host and a new ADR.
- Already released builds still download from `main` and stay broken for these two engines
  until users update.
- `ModelDownloadRevisionTests` asserts that every catalog URL is commit-pinned and that the
  Parakeet service requests exactly those URLs.
  [The knowledge note](../knowledge/huggingface-repos-rewrite-files-in-place.md) records how to
  find the commit that matches a set of digests.
