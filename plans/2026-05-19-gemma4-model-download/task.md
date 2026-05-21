---
title: Gemma 4 model download (E2B + E4B)
status: in_progress
created: 2026-05-19
started: 2026-05-19
completed:
---

# Gemma 4 model download

## Problem

`LlamaCppSpeechRecognizer` requires a Gemma 4 GGUF + mmproj on disk but
there is no UI to download them. The recognizer throws `"Download it first in
Settings"` against a settings section that does not exist. Only one variant
(`E4B Q4_K_M`) is hardcoded — no support for `E2B`, no choice of variant, no
progress feedback.

## Approach

Curated catalog of two variants (E2B and E4B, both `Q4_K_M`) downloaded
on-demand through a new Settings section. Architecture mirrors the existing
Whisper-model download pattern — same `ModelDownloadDialog`, same coordination
with recording state and recognizer hot-swap. Section auto-hidden when the
active engine is not Gemma 4 via the `RestrictToEngine` mechanism added in
ADR-028.

Deliberately **not** delegating to llama-server's `-hf` flag (surveyed in
`C:\projects\ggml-org\llama.cpp\common\arg.cpp`): that would lose the progress
bar for a 6 GB download and turn errors into stderr text. Our C# downloader
already exists ([Gemma4ModelDownloadService](../../src/Parlotype.Platform/Speech/Gemma4ModelDownloadService.cs)) — we extend it for a 2-entry catalog.

Detailed design in [implementation-plan.md](implementation-plan.md).

## Workplan

- [ ] Promote `Gemma4ModelInfo.Default` → catalog with `E2B` and `E4B`
- [ ] Add `Gemma4Variant` enum + `SelectedGemma4Variant` settings key
- [ ] Extend `Gemma4ModelDownloadService` to take a `Gemma4ModelInfo` arg
- [ ] Add `DeleteModelAsync` to the downloader
- [ ] New `Gemma4ModelDownloadDialogService` + `ForGemma4Model(...)` factory
- [ ] New `Gemma4ModelSettingsViewModel` + view (Category=SpeechEngine, RestrictToEngine=Gemma4)
- [ ] Register in DI ([App.axaml.cs:194](../../src/Parlotype.Desktop/App.axaml.cs:194)) and add to `SettingsWindowViewModel`
- [ ] `LlamaCppSpeechRecognizer` reads selected variant instead of `.Default`
- [ ] Tests: catalog, downloader, viewmodel, headless screenshot, nav-ordering update
- [ ] Write ADR-029, update memory vault, capture knowledge note on llama-server `-hf` survey
