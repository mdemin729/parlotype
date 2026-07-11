---
title: "Session: 2026-07-10 — Cloud-not-configured error popup"
type: session
status: complete
tags: [cloud-providers, ux, error-handling]
created: 2026-07-10
summary: "Record-start with an unconfigured cloud engine now shows a ConfirmationDialog with an Open-settings deep link instead of failing silently (ADR-043 amendment)."
---

# Session: 2026-07-10 — Cloud-not-configured error popup

## Active Focus
Follow-up to the cloud providers feature (fb0926d): pressing Record with a cloud
engine selected but no API key failed silently (log-only, button popped back).
Added a typed failure + popup + Settings deep link.

## Decisions Made
- New Core `CloudProviderNotConfiguredException : InvalidOperationException`
  (carries `SpeechEngine`) — mirrors the `RuntimeUnavailableException` precedent;
  deriving keeps generic handlers/tests working. Thrown by both cloud recognizers
  for the missing-key case.
- `TranscribeViewModel.StartRecordingCoreAsync` catches it **before** the generic
  catch; dialog is **fire-and-forget** because PTT stop awaits `_startTask`
  (ADR-039) — awaiting a modal there would hang key-release.
- Reusable dialog infra: `ConfirmationDialog` window + `ConfirmationDialogViewModel`
  + `IUserDialogService`/`UserDialogService` (owner/UI-thread handling copied from
  `ModelDownloadDialogService`). Result via `ShowDialog<bool?>`; close ⇒ cancel.
- `SettingsSection.CloudProviders` added; `SettingsWindowViewModel.NavigateTo`
  maps it. Deep link is a documented no-op if a local engine is active (section
  hidden) — irrelevant for this flow, tested anyway.

## Facts Learned
- xUnit `Assert.ThrowsAsync<T>` demands the **exact** exception type — switching a
  throw site to a derived type breaks existing `ThrowsAsync<InvalidOperationException>`
  asserts; they must be updated alongside (done in both recognizer test files).
- New `ConfirmationDialog` deliberately omits `SystemDecorations` (obsolete,
  AVLN5001) that `ModelDownloadDialog` still sets — 3 pre-existing AVLN5001
  warnings remain in older views on full rebuilds only.

## Open Blockers
- None. Manual end-to-end click-through (record button → popup → Open settings →
  Cloud providers page selected) not performed in-session (headless env); VM/dialog
  flows are covered by tests — worth a quick manual check on next app run.

## Documentation Status
- ADR: done — amendment section in `docs/decisions/043-cloud-speech-providers-v1.md`
- Vault: done — `services/core.md`, `services/desktop.md` (also backfilled the
  deferred cloud-UI symbols), `decisions/_index.md`; plan folder completed +
  `plans/INDEX.md` moved to Completed
- Knowledge: xUnit ThrowsAsync exactness noted here; not vault-worthy standalone

## Next Action
Uncommitted changes are on `claude/cranky-sammet-d148a2` awaiting a commit request.
Candidate next work: manual click-through of the popup flow, or the ADR-043
deferred list (key-validation ping at save time, unreachable-host fallback,
Linux/macOS keychain).
