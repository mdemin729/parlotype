---
title: "Hugging Face model repos rewrite files in place"
area: "speech-engines"
summary: "The sherpa-onnx Parakeet and ggml-org Gemma 4 repos replace, rename and delete files on main without notice; download from a commit SHA and find it with the commits and tree APIs."
verified: 2026-11-14
---

# Hugging Face model repos rewrite files in place

A file name on `main` of a Hugging Face model repo is not a stable identity. Both third-party
sources below changed under the app, which is why every download is pinned to a commit
([ADR-076](../decisions/076-pin-model-downloads-to-a-commit.md)).

- **`csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` and `…-v3`** are written by a
  GitHub workflow in `k2-fsa/sherpa-onnx`
  (`.github/workflows/export-nemo-parakeet-tdt-0.6b-v2.yaml`, which exports v2, v3 and the
  newer variants). Each run re-exports from the `.nemo` checkpoint and replaces `encoder`,
  `decoder` and `joiner` under the same names. A run does not reproduce the previous bytes: the
  2026-11-07 run produced different ones for all three graphs in both repos (fp32
  `encoder.weights` and `tokens.txt` stayed identical). Every commit in these repos is titled
  `first commit`, so the history says nothing about why. To learn what a run changed, read the
  workflow's and `scripts/nemo/parakeet-tdt-0.6b-v3/`'s history in the sherpa-onnx repo around
  the same date.
- **`ggml-org/gemma-4-E2B-it-GGUF` and `…-E4B-it-GGUF`** are re-uploaded as whole folders
  (`Upload folder using huggingface_hub`). On 2026-07-16 the quantisation suffix changed case
  (`bf16` to `BF16`; paths are case-sensitive, so the old names return 404), `Q4_K_M` was
  deleted in favour of `Q4_0`, and `Q8_0` was re-converted under its old name.
- **`sandrohanea/whisper.net`** keeps its GGML files on a branch named `v3`; `v4` and `v5`
  are tags, `v3` is not. It has not moved since 2024-10-07.

## Working with a pinned revision

- `GET https://huggingface.co/api/models/{repo}/commits/main` lists the commits, newest
  first. `GET …/api/models/{repo}/tree/{commit}` gives each file's `lfs.oid` at that commit
  (see [the digest note](huggingface-lfs-digests.md)). To find the commit matching a set of
  digests, walk the list and compare.
- `HEAD https://huggingface.co/{repo}/resolve/{commit}/{file}` answers 302 with
  `X-Linked-ETag` (the SHA-256, quoted), `X-Linked-Size` and `X-Repo-Commit`, so a pinned URL
  can be checked against the catalog without downloading the file. A deleted or renamed file
  answers 404. For a non-LFS file such as `tokens.txt` the answer is 307 and `X-Linked-ETag`
  holds the 40-character git blob id instead, which is not a content hash: download that file
  and hash it.
- Old commits stay downloadable after `main` moves on: all pinned files still resolved on
  2026-11-14, up to 15 months after their commit (25 for Whisper). A repo owner can still
  squash history, and then the pinned URL returns 404.
- `refs` (`GET …/api/models/{repo}/refs`) tells a branch from a tag. Neither is safe to
  download from; only a full 40-character commit SHA is, and `HuggingFaceUrl.Resolve` accepts
  nothing else.
