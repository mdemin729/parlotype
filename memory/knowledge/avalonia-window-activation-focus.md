---
title: Activating a window is not focusing anything in it
type: knowledge
tags: [avalonia, window, focus, activation, dispatcher, multi-window]
created: 2026-08-02
last_updated: 2026-08-02
summary: Window.Activate() focuses no control inside the window, and a window shown from a queued Dispatcher.Post activates after a caller that did not yield — both bite any multi-window flow
---

# Activating a window is not focusing anything in it

Two traps that together broke the onboarding tour's keyboard navigation
(ADR-056) — neither is visible from reading a single file.

## 1. `Activate()` moves the window, not the caret

`Window.Activate()` brings a window to the foreground, but **no control inside
it receives keyboard focus**. Key presses then reach the window with no focused
element, so `Button` shortcuts (Enter/Space on the focused button) do nothing.

Fixes, best used together:

- `IsDefault="True"` on the primary button — Avalonia registers a key handler on
  the *visual root*, so Enter works with nothing focused. It does not fire when
  another control already handled Enter (a focused `Button` handles Enter itself
  and marks it handled), so it is safe alongside normal buttons.
- `control.Focus(NavigationMethod.Tab)` — the `Tab` navigation method is what
  makes the focus ring actually render; the default `Unspecified` focuses
  silently and the user sees nothing.

## 2. Dispatcher-post ordering decides who ends up focused

A service that shows windows via `Dispatcher.UIThread.Post(...)` (Parlotype's
`WindowManager` does) runs at **Normal** priority. Code that asks for a window
and then finds it *synchronously* — because it was already open — completes its
whole continuation **before** that queued post runs. The post then calls
`Show()`/`Activate()` and takes focus back, so the caller's own `Activate()` is
silently undone.

The symptom is order-dependent and therefore easy to misread: the first visit to
a window behaves correctly (the search awaits, so the post runs first) and every
later visit does not.

Fix: yield below Normal before doing the work that must come last.

```csharp
await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
```

`Background` sits below `Normal`/`Default`, so every pending `Post` has run by
the time the continuation resumes.

## Verifying focus across windows

`GetForegroundWindow` is useless when the app under test is not in the
foreground. `GetGUIThreadInfo(uiThreadId).hwndFocus` reports the focus window
*within a thread* regardless of foreground state, and
`PostMessage(hwnd, WM_KEYDOWN/WM_KEYUP, VK_RETURN, 0)` drives that window
without touching global input — `SendKeys` would go to whatever the user has
focused, and `SetForegroundWindow` from a background process is silently
ignored.

Related: [[avalonia-click-event-vs-command]] for the test-harness equivalent,
[[avalonia12-frameless-window]] for frameless-window behaviour.
