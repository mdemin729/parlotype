---
status: accepted
date: 2026-02-22
---

# 007. Whisper Model Selection and Download

## Context

The Whisper model type was hardcoded (Base). Users need to choose from 12 model variants (Tiny through Large V3 Turbo) with different quality/speed/size tradeoffs. Models range from 75 MiB to 2.9 GiB and must be downloaded on first use.

## Decision

Three-layer model management architecture following the Core → Platform → Desktop pattern.

**Core layer:**

- `WhisperModelType` enum — platform-agnostic model identifiers (Tiny, TinyEn, Base, BaseEn, Small, SmallEn, Medium, MediumEn, LargeV1, LargeV2, LargeV3, LargeV3Turbo)
- `WhisperModelInfo` record — static metadata (display name, disk size, SHA hash) for all models
- `IModelDownloadService` interface — `EnsureModelAsync(type, ct)` returns path, `IsModelCached(type)` checks existence
- Selected model persisted via `SettingsKeys.SelectedWhisperModel`

**Platform layer:**

- `WhisperModelTypeExtensions.ToGgmlType()` maps Core enum to Whisper.net's `GgmlType`
- `HttpModelDownloadService` — direct HTTP download from Hugging Face CDN (`https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-{model}.bin`) with `IProgress<ModelDownloadProgress>` reporting. Replaces Whisper.net's built-in `WhisperGgmlDownloader` for progress control.
- Models cached at `%LOCALAPPDATA%/parlotype/models/`

**Desktop layer:**

- `ModelDownloadDialogService` implements `IModelDownloadService` — shows modal confirmation dialog ("Download Base (141 MiB)?") with progress bar and cancel button
- `ModelDownloadViewModel` manages states: Confirmation → Downloading → Done/Cancelled
- `WhisperModelDisplayItem` wrapper for flyout binding (follows WaitTimeDisplayItem pattern)

Key design choices:

- `WhisperModelType` lives in Core to keep domain boundary clean; `GgmlType` stays internal to Platform
- Replaced `WhisperGgmlDownloader` entirely for download progress control and cancellation
- Model is read from settings at `InitializeAsync` time — changing model takes effect on next pipeline restart

## Consequences

- Easier: Users can trade quality for speed by selecting smaller models. Download UI provides feedback and cancellation.
- Easier: Adding new model variants only requires extending the enum and WhisperModelInfo metadata.
- Harder: Large model downloads (2.9 GiB for LargeV3) may timeout or fail — needs retry logic (not yet implemented).
- Harder: SHA verification stored in WhisperModelInfo but not yet enforced during download.
