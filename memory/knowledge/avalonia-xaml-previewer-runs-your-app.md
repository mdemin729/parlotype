---
title: The Avalonia XAML Previewer Runs Your Real App
type: knowledge
status: active
tags: [avalonia, designer, previewer, rider, sharphook, startup, process]
created: 2026-09-05
summary: Rider/VS XAML preview executes OnFrameworkInitializationCompleted without ever calling Program.Main — every side effect in it runs in a dotnet.exe owned by the IDE
---

# The Avalonia XAML Previewer Runs Your Real App

Learned diagnosing a process that answered the dictation hotkey with no tray icon and no
window ([[063-xaml-previewer-must-not-start-the-app|ADR-063]]).

## What it actually does

From `Avalonia.DesignerSupport` 12.0.2, decompiled:

```csharp
Assembly.LoadFrom(appPath).EntryPoint;              // ONLY to read its DeclaringType
Design.IsDesignMode = true;                         // set before the app is constructed
AppBuilder.Configure(entryPoint.DeclaringType);     // finds the public BuildAvaloniaApp()
appBuilder.SetupWithoutStarting();                  // → App.OnFrameworkInitializationCompleted()
Dispatcher.UIThread.MainLoop(CancellationToken.None);
```

The two non-obvious parts:

- **`Program.Main` is never invoked.** The previewer only needs the entry point to find the
  type that declares `public static AppBuilder BuildAvaloniaApp()`. Anything `Main` does —
  single-instance locks, packaging hooks, argument parsing — simply does not happen.
- **`SetupWithoutStarting()` still calls `OnFrameworkInitializationCompleted()`.** That is
  part of `AppBuilder.Setup()`, not of running a lifetime. So every side effect an app puts
  in that method runs inside the previewer.

`ApplicationLifetime` is left null, so an `if (ApplicationLifetime is IClassicDesktopStyle…)`
block is skipped — which is exactly where most apps put their shutdown/cleanup handler. The
side effects start; nothing ever tears them down. And the main loop takes
`CancellationToken.None`, so the process ends only when the IDE kills it — which Rider does
per preview refresh, immediately spawning a replacement.

## How it presents

- Process name is **`dotnet.exe`**, not your app — invisible if you search Task Manager for
  the product name. Parent is `rider64.exe` (or the VS equivalent).
- Command line contains `Avalonia.Designer.HostApp.dll` and your `*.dll`, and points at
  whatever configuration the IDE last built (`bin\Release\…` even while you debug Debug).
- Several can run at once.
- A quick, unambiguous check for "is my app's global hook loaded in there": enumerate the
  process's windows and look for a **`libuiohook`** class window (SharpHook installs one,
  titled "Hidden Window to Monitor Display Change Events").

## The guard

Two orthogonal conditions, because either alone is a single point of failure:

```csharp
isDesignMode ? null : lifetime as IClassicDesktopStyleApplicationLifetime;
```

`Design.IsDesignMode` names the intent exactly but is the previewer's internal contract, not
yours. The lifetime check is structural and yours — a host that supplies no desktop lifetime
was never going to get a working desktop app. Put it as the *first* statement of
`OnFrameworkInitializationCompleted` so future additions are covered without an ordering rule.

Do **not** try to hide `BuildAvaloniaApp()`: the previewer requires it, and removing it just
disables XAML preview.

## Related

- [[dotnet-run-orphans-and-parent-pid]] — the other way a dev-time Parlotype survives; its
  "ruled out" note on `WM_TASKBARCREATED` still holds, but the previewer, not an orphaned
  `dotnet run` child, was the cause of the reported symptom.
- [[named-sync-primitives]] — the single-instance guard the previewer bypasses entirely.
- [[sharphook-suppress-event]] — why several live hooks make hotkey delivery undefined.
