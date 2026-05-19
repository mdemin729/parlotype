---
title: "Session: 2026-05-18 — llama.cpp panel UX hardening + recognizer reuse fix"
type: session
status: complete
tags: [llamacpp, settings, ux, bugfix, recognizer]
created: 2026-05-18
summary: Auto-probe on section open, banner accuracy across backends, running-vs-active install distinction, HttpClient reuse crash fix, lazy-restart hint
---

# Session: 2026-05-18 — llama.cpp panel UX hardening + recognizer reuse fix

## Active Focus

User-driven, iterative session that landed five bundled changes in one commit (`bfacdfd`):

1. `src/Parlotype.Desktop/ViewModels/SettingsWindowViewModel.cs` — auto-probe llama.cpp on section selection via `OnSelectedSectionChanged`.
2. `src/Parlotype.Desktop/ViewModels/Settings/LlamaCppSettingsViewModel.cs` — update banner rewritten to check all `Installed` builds regardless of backend; new `RunningManagedInstall` / `HasRunningManagedInstall` / `IsPendingActiveSwitch` properties; banner + running-install state recomputed inside `ReloadInstalledAndActiveAsync`.
3. `src/Parlotype.Desktop/Views/Settings/LlamaCppSettingsView.axaml` — "Active install" block renamed to "Running install" and rebound to `RunningManagedInstall`; new italic hint "Selection will take effect on the next recording" gated on `IsPendingActiveSwitch`.
4. `src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs` — `_httpClient` is now nullable and recreated per `InitializeAsync`, disposed on `UnloadAsync` / `DisposeAsync`. Fixes the `InvalidOperationException: This instance has already started one or more requests` crash that happened on the recording right after the user switched the active install.
5. Tests: `SettingsWindowViewModelTests` (+1 auto-probe), `LlamaCppSettingsViewModelManagedTests` (+2 banner accuracy, +3 RunningManagedInstall). 98/98 desktop + 242/242 core/platform pass.

## Decisions Made

- **Auto-probe is triggered at the `SettingsWindowViewModel` layer, not in `LlamaCppSettingsView.OnAttachedToVisualTree`.** Reason: `LlamaCppScreenshotTests` construct the view directly with simulated VM state; an attach-time auto-probe would clobber those scenarios and force broad rewrites. The VM-level trigger captures the actual user gesture without affecting view-level tests.
- **Update banner hides when any installed variant matches the latest build, not just when the active one matches.** User-explicit requirement: "независимо от того на чём будет запускаться сервер - cpu, vulkan, cuda".
- **"Active install" semantics split into two distinct concepts.** `ActiveManagedInstall` = user's radio choice for the next launch. `RunningManagedInstall` = best-effort match between probe's `build_info` and `Installed`. Status panel shows the running one; radio list shows the active one.
- **Backend ambiguity falls back to the `/props` Build row.** When two installs share the same build (e.g. b9221 CUDA 13 + b9221 CPU), `RunningManagedInstall` stays null rather than picking arbitrarily. The `/props` "Build:" line below already shows the running build string verbatim, so no information is lost.
- **Hot-swap stays lazy, not eager.** Rejected user's two proposed UX options (modal dialog before swap; inline progress in settings). Modal adds friction with no value (the radio click is the confirmation); eager swap wastes server spin-ups while user explores options. Kept the existing lazy unload→next-recording-restart flow and surfaced its existence with a single italic line of text — no dialog, no progress bar.
- **HttpClient is recreated per `InitializeAsync` rather than refactoring to per-request `HttpRequestMessage`.** Narrower change, single file, preserves call sites. Documented the constraint in a field-level comment so future readers don't reintroduce the bug.
- **No regression test added for the HttpClient lifecycle fix.** Exercising the bug requires `_httpClient` to actually be used between Init cycles, which only happens through the spawn path (real binary) or against a real HTTP server. A focused `HttpListener`-based test was considered and rejected — the fix is mechanically simple, the failure mode is well-documented in the field comment, and the user's reproduction is the real verification.

## Facts Learned

- `HttpClient.BaseAddress` is immutable after the first request — setter throws `InvalidOperationException` via `CheckDisposedOrStarted()`. Confirmed by the user's stack trace and `LlamaCppSpeechRecognizer.cs:71` repro. Means any field-initialized `HttpClient` is one-shot for `BaseAddress`. Now documented in the field comment.
- `LlamaCppServerInfo.ProbeAsync` uses its own internal `HttpClient`, **not** the recognizer's `_httpClient`. The recognizer's `_httpClient` is only used in `TranscribeAsync` (POST `/v1/chat/completions`) and `WaitForServerReadyAsync` (poll `/health` during spawn). This matters for testing: the adopt-existing path doesn't touch `_httpClient` at all, so it doesn't repro the BaseAddress crash.
- `LlamaServerInstaller.BuildInstallId(variant)` derives the install ID from `variant.AssetName`, not from `Backend`. Two test variants with the same asset name produce the same install ID and collide in the registry — `MockLlamaServerInstaller` no-ops the second install or overwrites. Made one of my early ambiguity tests fail until I gave the variants distinct asset names via a `VariantWithAsset` helper.
- `[NotifyPropertyChangedFor(nameof(X))]` from `CommunityToolkit.Mvvm` works for chaining derived bool properties from multiple sources — used to keep `IsPendingActiveSwitch` in sync with both `ActiveManagedInstall` and `RunningManagedInstall`.
- `xunit.v3` (3.2.2) + VSTest adapter discovery is fine with mixed `[Fact] void` and `[Fact] async Task` in the same class.
- `ObjectConverters.IsNotNull` exists in Avalonia 12 but isn't used elsewhere in this project — explicit derived bool properties are the established pattern.

## Open Blockers

None. All work landed, tests green, committed as `bfacdfd`. Not pushed.

## Documentation Status

- ADR: none required. Bug fix + UX refinements; no new Core type, no new `PlatformServiceExtensions` entry, no new `.csproj` dep, no OS/flag-conditional behaviour. The change touches the Speech subsystem (`LlamaCppSpeechRecognizer`), but it's an internal lifecycle fix, not a contract change.
- Vault: minimal — one line added to `memory/services/desktop.md` (auto-probe on section open). The HttpClient lifecycle and the `RunningManagedInstall` distinction are documented in code/AXAML comments and don't add new public symbols, so no further service-profile edits.
- Knowledge: none. The `HttpClient.BaseAddress` constraint is a .NET API fact derivable from docs and now captured in a field comment; no `memory/knowledge/*.md` entry needed.

## Next Action

Wait for user direction. Likely candidates if the user picks back up here:

- **Push the branch** — `claude/serene-shannon-d02f45` is 1 ahead of `origin/master` and not pushed. PR not opened. Trivially: `git push -u origin claude/serene-shannon-d02f45` then `gh pr create`.
- **Initializing indicator in transcribe UI** — option C from the earlier UX discussion. When `LlamaCppSpeechRecognizer.InitializeAsync` is running (cold start can take 3–5 s), the transcribe button should show an "Initializing…" state instead of a silent stall. Would touch `TranscribeViewModel` and `TranscribeWindow.axaml`. The user picked option B today, not C, so this is opt-in for a future session.
- **Open follow-up bugs** — if more llama.cpp panel issues surface, the current factoring (running vs active install) is the right place to add behaviour.
