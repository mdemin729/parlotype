---
status: accepted
date: 2026-05-02
---

# 020. Upgrade SharpHook to 7.1.1 and Switch to SimpleGlobalHook

## Context

Parlotype's global hotkey system used `TaskPoolGlobalHook` (SharpHook 6.0.0) with `e.SuppressEvent = true` to prevent hotkey characters from reaching the focused application. However, per the [SharpHook documentation](https://sharphook.tolik.io/articles/hooks.html), **`TaskPoolGlobalHook` silently ignores `SuppressEvent`** because event handlers run on thread-pool threads, not the hook thread. This meant hotkey suppression was never actually working — pressing `Win+Shift+K` would type "K" into the text editor.

Additionally, `OnKeyReleased` never set `SuppressEvent`, so even with the correct hook type, key-up events would pass through to the target application.

## Decision

1. **Upgrade SharpHook** from 6.0.0 to 7.1.1. The v6→v7 breaking changes are minimal (interface reorganization, `SimpleReactiveGlobalHook` renamed) and do not affect our code.

2. **Switch from `TaskPoolGlobalHook` to `SimpleGlobalHook`**. `SimpleGlobalHook` runs event handlers synchronously on the hook thread, which is required for `SuppressEvent` to work. Our handlers are lightweight (set volatile bools, fire events), so blocking the hook thread is not a concern.

3. **Suppress key-up events** by adding `e.SuppressEvent = true` in `OnKeyReleased`, with a modifier check matching the symmetric behavior in `OnKeyPressed`. This prevents stuck-key states and ensures the full hotkey combination is consumed.

## Consequences

**Easier:**
- Hotkey characters no longer leak into the focused application (Windows/macOS)
- Both key-down and key-up are symmetrically suppressed with modifier validation

**More difficult:**
- `SimpleGlobalHook` handlers block the hook thread — if handlers become expensive in the future, this could cause input lag. Current handlers are fast (volatile bool + event raise), so this is not a concern today.
- `SuppressEvent` remains unsupported on Linux (libuiohook limitation). Linux users will still see hotkey characters passed through. See **Linux Suppression Options** below for future mitigation paths.

## Linux Suppression Options

libuiohook (and therefore SharpHook) can *observe* keyboard events on Linux but cannot *suppress* them — this is a fundamental OS-level limitation, not a SharpHook bug. Below are the approaches available for future implementation:

| Approach | Suppression | Display Server | Trade-offs |
|----------|:-----------:|----------------|------------|
| **X11 `XGrabKey`** | ✅ | X11 only | Exclusive key grab — the key is consumed and never reaches other apps. Classic approach (sxhkd, autokey). X11 is being phased out. |
| **`xdg-desktop-portal` GlobalShortcuts** | ✅ | Wayland + X11 | D-Bus portal where the *compositor* handles the grab. Supported on GNOME & KDE (the two largest desktops). The standard, future-proof approach. |
| **Compositor plugins** | ✅ | Wayland | GNOME Shell extensions, KWin scripts, Sway config — compositor-level shortcuts always suppress. Per-compositor effort. |
| **Dedicated non-text key** | N/A | Any | If the hotkey uses a key that doesn't produce text (e.g. `F13`, `Pause`, `ScrollLock`), suppression is unnecessary. Workaround, not a fix. |

### Recommended future path

1. **`xdg-desktop-portal` GlobalShortcuts** via D-Bus is the most promising cross-compositor solution. It would require a new `LinuxPortalHotkeyService` in Parlotype.Platform that communicates over D-Bus (`org.freedesktop.portal.GlobalShortcuts`) instead of using SharpHook. The compositor owns the key grab, so suppression works natively.

2. **Fallback guidance** — for compositors that don't support the GlobalShortcuts portal, document how users can bind a system shortcut at the compositor level (GNOME Settings → Shortcuts, KDE System Settings, Sway config) that triggers Parlotype via D-Bus or command-line.
