---
title: "Session: 2026-05-02 — Hotkey suppression fix"
type: session
status: complete
tags: [hotkeys, sharphook, bugfix]
created: 2026-05-02
summary: "Fixed hotkey character leaking into text editor by upgrading SharpHook and switching to SimpleGlobalHook"
---

# Session: 2026-05-02 — Hotkey Suppression Fix

## Active Focus
- `src/Parlotype.Platform/Hotkeys/SharpHookHotkeyService.cs` — switched `TaskPoolGlobalHook` → `SimpleGlobalHook`, added key-up suppression with modifier check
- `src/Parlotype.Platform/Parlotype.Platform.csproj` — upgraded SharpHook 6.0.0 → 7.1.1

## Decisions Made
- **ADR-020**: Switched to `SimpleGlobalHook` because `TaskPoolGlobalHook` silently ignores `SuppressEvent` (handlers run on thread pool, not hook thread)
- Suppression on key-release must include modifier check to avoid stuck-key states (caught by code review)
- SharpHook 7.1.1 has minimal breaking changes from 6.0.0 (interface reorganization only, no impact on our code)

## Facts Learned
- **SharpHook `SuppressEvent` only works with `SimpleGlobalHook`** — `TaskPoolGlobalHook` and `EventLoopGlobalHook` silently ignore it. No compile-time or runtime error. This was the root cause of the hotkey character leak. Documented in `memory/knowledge/sharphook-suppress-event.md`.
- SharpHook v7 added `EventLoopGlobalHook` (dedicated event-loop thread) but it also doesn't support suppression.
- `SimpleGlobalHook` handlers must be fast since they block the OS hook thread. Current handlers (volatile bools + event raise) are fine.
- **Linux has no userland key suppression** — libuiohook (SharpHook) can observe but not suppress on Linux. On X11, `XGrabKey` works. On Wayland, only the compositor can suppress. The `xdg-desktop-portal` GlobalShortcuts D-Bus API (supported on GNOME & KDE) is the future-proof path. Documented in ADR-020's "Linux Suppression Options" section.

## Open Blockers
- None

## Documentation Status
- ADR: done — `docs/decisions/020-sharphook7-simple-global-hook.md`
- Vault (services/architecture): done — updated `memory/architecture/subsystems.md`
- Knowledge (non-derivable facts): done — `memory/knowledge/sharphook-suppress-event.md`

## Next Action
- Verify hotkey suppression manually by launching the app (`dotnet run --project src/Parlotype.Desktop`) and testing `Win+Shift+K` in a text editor
- Consider implementing `LinuxPortalHotkeyService` using `xdg-desktop-portal` GlobalShortcuts D-Bus API for Linux suppression support (see ADR-020)
- Update `memory/AGENTS.md` quick commands: `dotnet run --project src/Parlotype.Desktop.V2` → `src/Parlotype.Desktop` (V1 was sunset in ADR-018)
