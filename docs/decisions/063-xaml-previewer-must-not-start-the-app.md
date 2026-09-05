---
status: accepted
date: 2026-09-05
---

# 063. The XAML Previewer Must Not Start the App

## Context

The symptom that motivated [ADR-062](062-dev-parent-process-watchdog.md) came back after that
fix shipped: Parlotype still answering the dictation hotkey — hold **Ctrl**, speak, text is
injected — with no tray icon, no window, and nothing called "Parlotype" in Task Manager.

This time the surviving process was identified:

```
PID 77708   parent: rider64.exe
"C:\Program Files\dotnet\dotnet.exe" exec
  --runtimeconfig ...\Parlotype.Desktop\bin\Release\net10.0\Parlotype.runtimeconfig.json
  --depsfile     ...\Parlotype.Desktop\bin\Release\net10.0\Parlotype.deps.json
  ...\avalonia\12.0.2\tools\net8.0\designer\Avalonia.Designer.HostApp.dll
  --transport tcp-bson://127.0.0.1:62969/ --method avalonia-remote
  ...\Parlotype.Desktop\bin\Release\net10.0\Parlotype.dll
```

It owns a window of class **`libuiohook`** — SharpHook's global keyboard hook. It is the
**Avalonia XAML previewer**, hosted by Rider, and it had started the whole app. From the
shared rolling log, one previewer launch:

```
Dev watchdog armed — this instance will shut down when rider64 (77928) exits
Speech-model prewarm enabled — warming in background
Global hotkey listener started — bindings: Double-tap Ctrl, Hold Right Ctrl
Parakeet model loaded successfully
Utterance ceiling: 60s of speech (Parakeet)
Target window set to 196906 (PID 14304, Name explorer)
```

Global hook, microphone, text-injection window tracking, and a ~2.6 GB Parakeet model — in a
process the user never launched. Two of them ran concurrently, because Rider spawns a fresh
previewer per refresh.

**Mechanism** (decompiled from `Avalonia.DesignerSupport` 12.0.2):

```csharp
Assembly.LoadFrom(appPath).EntryPoint;              // only to get Program's *type*
Design.IsDesignMode = true;                         // set before the app is built
AppBuilder.Configure(entryPoint.DeclaringType);     // finds Program.BuildAvaloniaApp()
appBuilder.SetupWithoutStarting();                  // → App.OnFrameworkInitializationCompleted()
Dispatcher.UIThread.MainLoop(CancellationToken.None);
```

`Program.Main` is **never invoked** — the previewer takes only its declaring type, to locate
the public `BuildAvaloniaApp()`. Consequences that all conspire:

- **No `SingleInstanceGuard`** (ADR-055): the previewer runs alongside the real app, and
  several hooks compete for one keypress — precisely what ADR-055 exists to prevent.
- **No `IClassicDesktopStyleApplicationLifetime`**, so `App`'s `desktop.Exit` handler is never
  registered and *nothing ever disposes the hook*.
- **ADR-062's watchdog cannot help**: it arms on the immediate parent, which here is
  `rider64.exe` — alive for the whole session. Even had it fired, its callback was
  `(ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown()`, a silent
  no-op without a lifetime.
- The previewer's main loop takes `CancellationToken.None`, so it ends only when Rider kills
  the process — which it does per refresh, spawning a replacement that starts everything again.

ADR-062's orphaned-`dotnet run`-child is a real failure mode and its fix stands, but it was
not the cause of the reported symptom. This is.

## Decision

`App.OnFrameworkInitializationCompleted` bails out before any runtime bootstrap unless this
process is a real Parlotype run:

```csharp
if (ResolveRuntimeLifetime(ApplicationLifetime, Design.IsDesignMode) is not { } desktop)
{
    base.OnFrameworkInitializationCompleted();
    return;
}
```

`ResolveRuntimeLifetime` (internal, unit-tested) returns the desktop lifetime or null:

```csharp
isDesignMode ? null : lifetime as IClassicDesktopStyleApplicationLifetime;
```

**Two orthogonal conditions, deliberately.** `Design.IsDesignMode` is Avalonia's own flag and
names the intent exactly, but it is the previewer's internal contract, not ours — a future
previewer could stop setting it. The lifetime check is structural and ours: everything below
the guard needs a desktop lifetime (shutdown mode, the `Exit` handler, windows, the tray), so
a host that supplies none was never going to get a working app. Either alone would be a
single point of failure.

The method's body is now flat — the old `if (ApplicationLifetime is … desktop)` block is gone,
since `desktop` is guaranteed from the guard onward, and the watchdog's shutdown callback
calls `desktop.Shutdown()` on a non-null capture instead of a null-conditional cast.

**Not** keyed on "did `Program.Main` run". That signal is also true and is the most tamper-proof
of the three, but it couples `App` to a static on `Program` to add a third vote where two
independent ones already agree.

## Consequences

- The previewer no longer takes the microphone, the global hook, gigabytes of RAM, or a slot
  the single-instance guard cannot see. Verified on the real `Avalonia.Designer.HostApp`
  against both builds: unguarded it owns a `libuiohook` window, guarded it does not.
- **XAML previews no longer follow the user's saved theme.** `ApplyTheme` sat after the guard
  because it needs the DI container. Previews render in the default variant — which is the
  better default anyway: a design surface should not depend on one developer's settings.
- Design-time data is unaffected: `<Design.DataContext>` with parameterless ViewModel
  constructors never went through the container (existing convention, `CLAUDE.md`).
- Anything added to `OnFrameworkInitializationCompleted` from now on is automatically covered.
  The guard is the first statement, so there is no ordering rule to remember.
- ADR-062's watchdog and the `desktop.Exit` ordering it introduced stay as they are; they
  cover a different, still-real failure mode.

## Alternatives rejected

- **Make the previewer harmless by hiding `BuildAvaloniaApp`.** The previewer requires a public
  static `AppBuilder BuildAvaloniaApp()` on the entry point's type; renaming or hiding it
  disables XAML preview entirely. Punishing the tool for our bootstrap's side effects.
- **Move the bootstrap into `Program.Main`.** Correct in spirit and it would fix this, but
  Avalonia's lifecycle wants app-level setup in `OnFrameworkInitializationCompleted`, and it
  would strand the DI container outside the `Application` that every ViewModel resolves from.
  A guard is a two-line statement of the same intent.
- **Have the previewer inherit the single-instance guard.** `SingleInstanceGuard` lives in
  `Program.Main` by design (ADR-055: it must run after `VelopackApp.Run()` and before Avalonia).
  Reaching it from `App` would invert that ordering, and a previewer that *loses* the race would
  then try to activate the real app's window on every XAML edit.
- **Detect the host process by name** (`Avalonia.Designer.HostApp`, `rider64`). Brittle
  denylist; breaks on the next tool that loads the assembly.
