---
title: "Session: 2026-04-30 — Scrollable Settings + Model Download Dialog"
type: session
status: active
tags: [desktop-v2, ui, settings, model-download]
created: 2026-04-30
summary: Made the V2 settings content area scrollable and ported the V1 model download dialog to V2, replacing the silent download service.
---

# Session: 2026-04-30 — Scrollable Settings + Model Download Dialog

## Active Focus
- `src/Parlotype.Desktop.V2/Views/SettingsWindow.axaml` — wrapped `ContentControl` in `ScrollViewer` so all settings sections scroll when content overflows
- `src/Parlotype.Desktop.V2/ViewModels/ModelDownloadViewModel.cs` — **new** ViewModel for download dialog (progress, status, confirmation)
- `src/Parlotype.Desktop.V2/Views/ModelDownloadDialog.axaml` + `.axaml.cs` — **new** modal dialog with confirmation, progress bar, cancel
- `src/Parlotype.Desktop.V2/Services/ModelDownloadDialogService.cs` — **new** replaces `SilentModelDownloadService`; shows modal dialog when model not cached
- `src/Parlotype.Desktop.V2/App.axaml.cs` — DI registration swapped to `ModelDownloadDialogService`
- `src/Parlotype.Desktop.V2/Services/SilentModelDownloadService.cs` — **deleted**

## Decisions Made
- **ScrollViewer on the settings content area** (not per-section) — any section that grows tall will scroll, not just the model list. General-purpose fix.
- **Port V1 dialog pattern directly** — reused the same `IModelDownloadService` interception point, modal dialog UX (confirm → progress → cancel), and `HttpModelDownloadService` delegation. No new Core contracts needed.
- **Owner window resolution** — `ModelDownloadDialogService.GetOwnerWindow()` iterates `desktop.Windows` for the first visible window (settings or transcribe), falling back to `MainWindow`. V2 is tray-first so there's no guaranteed main window.

## Facts Learned
- V2's `SilentModelDownloadService` was a placeholder that downloaded models with log-only progress (no UI). The comment in its doc explained "V2 frontend is tray-first and has no always-visible main window to host a confirmation dialog."
- File lock errors from `.NET Host` processes are common when rebuilding on Windows — kill the specific PIDs listed in the error message before retrying.

## Open Blockers
- None.

## Documentation Status
- ADR: none required — no new Core interfaces, no new dependencies, no subsystem changes
- Vault (services/architecture): none required — internal V2 implementation swap only
- Knowledge (non-derivable facts): none — file lock gotcha already documented in CLAUDE.md

## Next Action
Pick up from a clean slate. Suggested follow-ups:

1. **Decide V1 sunset path** — ADR 015 leaves V1 + V2 coexisting; eventually one needs to be retired.
2. **Suppress Avalonia build telemetry noise** — the `Avalonia Accelerate Community requires telemetry...` message is noisy in build output.
3. **Test the model download dialog manually** — select an uncached model in Settings → Whisper Model and verify the dialog appears, downloads with progress, and handles cancel gracefully.
