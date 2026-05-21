---
title: SharpHook SuppressEvent requires SimpleGlobalHook
type: knowledge
tags: [sharphook, hotkeys, suppression]
created: 2026-05-02
last_updated: 2026-05-02
summary: TaskPoolGlobalHook and EventLoopGlobalHook silently ignore SuppressEvent; only SimpleGlobalHook supports it
---

# SharpHook SuppressEvent requires SimpleGlobalHook

## Fact

SharpHook's `SuppressEvent` property on `KeyboardHookEventArgs` **only works with `SimpleGlobalHook`**. Both `TaskPoolGlobalHook` and `EventLoopGlobalHook` silently ignore it because their handlers run on different threads from the hook thread.

There is no compile-time or runtime error — the property setter accepts the value but the suppression never takes effect.

## Source

- [SharpHook documentation — Hooks](https://sharphook.tolik.io/articles/hooks.html):
  > `TaskPoolGlobalHook` … has a downside – suppressing event propagation will be ignored since event handlers are run on other threads.
- Verified with SharpHook 7.1.1

## Why This Matters

- If you switch back to `TaskPoolGlobalHook` for performance, hotkey characters will leak into the focused application again with no visible error.
- `SimpleGlobalHook` handlers must be kept lightweight to avoid blocking the OS hook thread and causing input lag.
- `SuppressEvent` is only supported on Windows and macOS (libuiohook limitation on Linux).
