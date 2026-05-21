---
title: "Session: 2026-05-19 — Gemma 4 model download UI"
type: session
status: active
tags: [gemma4, llamacpp, download, settings, ui, huggingface]
created: 2026-05-19
summary: "Added a Settings UI to download Gemma 4 E2B/E4B (Q4_K_M) GGUFs with progress; catalog + variant selection wired through recognizer."
---

# Session: 2026-05-19 — Gemma 4 model download UI

## Active Focus

ADR-029. Gave the Gemma 4 (llama.cpp) engine a real model-download UX.

Files added:
- `src/Parlotype.Desktop/Services/Gemma4ModelDownloadDialogService.cs`
- `src/Parlotype.Desktop/ViewModels/Gemma4ModelDisplayItem.cs`
- `src/Parlotype.Desktop/ViewModels/Settings/Gemma4ModelSettingsViewModel.cs`
- `src/Parlotype.Desktop/Views/Settings/Gemma4ModelSettingsView.axaml(.cs)`
- `src/Parlotype.Tests/Gemma4ModelInfoTests.cs`
- `src/Parlotype.Tests/Gemma4ModelDownloadServiceTests.cs`
- `src/Parlotype.Desktop.Tests/Gemma4ModelSettingsViewModelTests.cs`
- `src/Parlotype.Desktop.Tests/Gemma4ModelSettingsScreenshotTests.cs`

Files modified:
- `Gemma4ModelInfo` → record + 2-entry catalog (E2B, E4B) + `Gemma4Variant` enum
- `SettingsKeys.SelectedGemma4Variant` added
- `Gemma4ModelDownloadService` — methods take `Gemma4ModelInfo`; added `DeleteModelAsync`
- `LlamaCppSpeechRecognizer` — `GetSelectedModelAsync` reads the variant from settings (was `.Default`)
- `ModelDownloadViewModel.ForGemma4Model` factory
- DI in `App.axaml.cs`; `SettingsWindowViewModel` ctor + `_allSections`
- `SettingsWindow.axaml` DataTemplate
- `SettingsWindowViewModelTests` nav-ordering (Gemma4 active now lists "Gemma 4 model")

## Decisions Made

- Own C# downloader with progress dialog, **not** llama-server `-hf` (no
  progress UX for 6 GB). See [[llama-server-hf-download]].
- **All quantizations** offered (revised from "Q4_K_M only" at user request):
  5-entry catalog (E2B Q8_0/BF16, E4B Q4_K_M/Q8_0/BF16) keyed by `ModelId`
  string. Settings key `SelectedGemma4Model` (was `SelectedGemma4Variant`).
  Default = E4B Q4_K_M.
- No migration of pre-existing downloads, no SHA256, no HF token.
- New section reuses the ADR-028 `RestrictToEngine` mechanism → visible only
  when Gemma 4 is the active engine. Whisper auto-downloads on first use, but
  Gemma requires an explicit Download click (intentional asymmetry for large
  files).
- Download dialog gained a completion **Close** state (no auto-close) and
  cumulative GGUF+mmproj progress (combined size via HEAD) so the dialog total
  matches the list label.

## Facts Learned

- llama-server's built-in HF download flags (`-hf`/`-hff`/`-hft`) and cache
  layout — captured in [[llama-server-hf-download]].

## Bugs found & fixed in testing

1. **E2B `Q4_K_M` 404** — that repo has no Q4_K_M (only Q8_0/bf16). Rebuilt the
   catalog from the repos' real file lists (HF API `/tree/main`).
2. **`File.Move` "file in use"** — `DownloadFileAsync` used `await using var`
   declarations, so the temp `FileStream` was still open at the move. Fixed by
   scoping the streams in a `using` block (+ `FlushAsync`) before the move.
   `StreamingFileDownloader` (Whisper) already did this correctly.
3. **Progress mismatch** — dialog showed per-file MiB (reset between GGUF and
   mmproj). Now reports cumulative bytes against the combined size (HEAD per
   pending file).
4. **No completion affordance** — dialog auto-closed on success. Now shows a
   **Close** button (Download/Cancel hidden) via `ModelDownloadViewModel.IsComplete`.
5. **Status line flicker** — byte counter was appended to `StatusText`, so the
   growing number reflowed the whole line. Split into a separate
   `ModelDownloadViewModel.ProgressText` on its own `NoWrap` line; `StatusText`
   is now stable during download.

## Open Blockers

- Pending: user confirmation of a successful E2B download + transcription with
  the corrected catalog (E2B Q8_0). Other quants (E4B Q8_0/BF16, E2B BF16)
  untested but follow the same verified code path.

## Documentation Status

- ADR: done — `docs/decisions/029-gemma4-model-download-ui.md`
- Vault (services/decisions/knowledge): done — `platform.md`, `desktop.md`,
  `decisions/_index.md`, `knowledge/_index.md` + `knowledge/llama-server-hf-download.md`
- Knowledge: done — [[llama-server-hf-download]]

## Next Action

**Done.** User verified transcription + Delete end-to-end (2026-05-20). Plan
`plans/2026-05-19-gemma4-model-download/` marked `completed` and removed from
`plans/INDEX.md`. Feature shipped on branch `claude/agitated-gould-c939ab`
(commits da5bb04 settings nav + 25feb94 Gemma download + this closeout). Nothing
pushed yet — next session may open a PR if desired.
