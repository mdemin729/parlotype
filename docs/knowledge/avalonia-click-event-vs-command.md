---
title: "RaiseEvent(Button.ClickEvent) does not execute the bound Command"
area: "avalonia"
summary: "Raising Button.ClickEvent directly skips OnClick, so a bound Command never runs; headless tests must click through MouseDown/MouseUp."
verified: 2026-11-11
---

# RaiseEvent(Button.ClickEvent) does not execute the bound Command

Raising the routed event directly —
`button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))` — **bypasses
`Button.OnClick`**, which is where Avalonia both raises `Click` *and* invokes
the bound `Command`. So the pattern from `ConfirmationDialogTests` (which works
because that dialog wires `Click` handlers in code-behind) silently does
nothing for command-bound buttons: the test sees the event raised, the command
never runs.

**Fix for headless tests:** drive the real input pipeline instead —

```csharp
var point = button.TranslatePoint(
    new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
window.MouseDown(point, MouseButton.Left);   // Avalonia.Headless extension
window.MouseUp(point, MouseButton.Left);
Dispatcher.UIThread.RunJobs();
```

Run `Dispatcher.UIThread.RunJobs()` (or `UpdateLayout`) before translating so
layout has happened. Discovered while testing `OnboardingWindow` (ADR-056) —
see the `Click` helper in `OnboardingWindowTests`.

Related: [avalonia-popup-patterns](avalonia-popup-patterns.md) for other headless-testing traps, and
[avalonia-window-activation-focus](avalonia-window-activation-focus.md) for the runtime counterpart — activating a
window focuses nothing inside it.
