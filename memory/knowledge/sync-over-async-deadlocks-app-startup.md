---
title: Blocking on ISettingsService from the UI thread deadlocks app startup
type: knowledge
tags: [avalonia, startup, async, settings, testing]
created: 2026-09-05
last_updated: 2026-09-05
summary: JsonFileStore awaits without ConfigureAwait(false), so awaiting it and blocking from Avalonia's UI thread hangs before the first window — and the obvious regression test silently passes unless the settings file actually exists
---

# Sync-over-async on the Avalonia UI thread

Hit while wiring the startup interface-language read for
[[../decisions/_index|ADR-064]].

## The deadlock

`JsonFileStore` — the base of `JsonSettingsService`, `DpapiSecretStore` and the window-state
store — awaits throughout **without `ConfigureAwait(false)`**:

```csharp
await _lock.WaitAsync(cancellationToken);
var dict = await LoadAsync(cancellationToken);
var json = await File.ReadAllTextAsync(_path, cancellationToken);
```

Avalonia installs an `AvaloniaSynchronizationContext` on the UI thread before
`OnFrameworkInitializationCompleted` runs. So any of these on that thread hangs the app
before the first window ever appears:

```csharp
settings.GetAsync<string>(key).GetAwaiter().GetResult();   // deadlock
settings.GetAsync<string>(key).Result;                     // deadlock
settings.GetAsync<string>(key).Wait();                     // deadlock
```

The continuation after each `await` is posted back to the UI thread, which is blocked
waiting for the task that continuation would complete.

`ConfigureAwait(false)` at the **call site** does not help — the captures happen inside
`JsonFileStore`, not in the caller's frame.

## The fix, and why it is two steps

```csharp
var stored = Task.Run(() => uiLanguage.ReadStoredLanguageAsync()).GetAwaiter().GetResult();
uiLanguage.ApplyLanguage(stored);
```

`Task.Run` starts the chain on the thread pool, where the continuations have somewhere to
run, so the blocking wait is safe. But the *result* must then be applied on the UI thread:
`CultureInfo.CurrentUICulture` is thread-static, and setting it inside the `Task.Run` would
land it on a pool thread rather than the thread that reads resources.

Hence the deliberate split of `UiLanguageService` into `ReadStoredLanguageAsync()` (async,
off-thread) and `ApplyLanguage()` (sync, on the UI thread). Collapsing them back into one
`ApplyStartupLanguageAsync().GetAwaiter().GetResult()` reintroduces the hang.

`Program.UserAskedForDataRemoval` (ADR-053) sidesteps the same problem differently — it
reads `settings.json` with synchronous `File.ReadAllText` because it runs before DI exists.

## The test trap

An `[AvaloniaFact]` **does** carry an `AvaloniaSynchronizationContext` (verified by probing
`SynchronizationContext.Current`), so it is the right place to guard this. But the obvious
test passes even with the bug present:

> if `settings.json` does not exist, `LoadAsync` returns early, nothing ever suspends, no
> continuation is posted, and there is no deadlock to observe.

The test must **write a real settings file first**. With the file present, the broken form
hangs and kills the test host; with the fix, it passes in ~40 ms. Verified both ways —
a regression test for this that has never been seen to fail is probably testing nothing.

See `LocalizationTests.StartupLanguage_LoadsOnAUiThread_WithoutDeadlocking`.
