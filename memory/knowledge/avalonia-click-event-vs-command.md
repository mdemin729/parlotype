---
type: knowledge
tags: [avalonia, headless-testing, xunit, button, command]
created: 2026-08-02
summary: Raising Button.ClickEvent via RaiseEvent does not invoke a bound Command — headless tests must click through the input pipeline
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
layout has happened. Discovered while testing `OnboardingWindow` (ADR-055) —
see the `Click` helper in `OnboardingWindowTests`.

Related: [[avalonia-popup-patterns]] for other headless-testing traps.
