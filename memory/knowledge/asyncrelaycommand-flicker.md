---
title: AsyncRelayCommand CanExecute Flicker in ItemsControl
type: knowledge
tags: [communitytoolkit, mvvm, avalonia, ui]
created: 2026-04-30
last_updated: 2026-09-06
summary: CommunityToolkit.Mvvm AsyncRelayCommand disables all buttons sharing the command while executing, causing visible flicker in Avalonia ItemsControl lists
---

# AsyncRelayCommand CanExecute Flicker in ItemsControl

## Fact

When a `[RelayCommand]` method returns `async Task`, CommunityToolkit.Mvvm generates an `AsyncRelayCommand<T>` whose default behaviour sets `CanExecute = false` while `IsRunning = true`. If multiple UI elements (e.g., buttons in an `ItemsControl`) share the **same command instance**, ALL buttons disable and re-enable simultaneously, producing a visible flicker.

## Workaround

Keep the command method synchronous (`void`) and fire-and-forget the async work from within:

```csharp
[RelayCommand]
private void SelectModel(WhisperModelType type)
{
    Apply(type);  // instant UI update
    _ = ApplyModelChangeAsync(type);  // async cleanup
}
```

This generates a `RelayCommand<T>` (no `IsRunning` tracking), so `CanExecute` remains `true` throughout.

## Alternative

Use `[RelayCommand(AllowConcurrentExecutions = true)]` on the async method — this preserves `CanExecute = true` but loses concurrency protection.

## Context

Discovered during Parlotype V2 Whisper model selection: an `ItemsControl` renders 12 model buttons all bound to `SelectModelCommand`. Switching to async caused all 12 to flash disabled→enabled.

## Second occurrence — worth checking first

Hit again in `InterfaceLanguageSettingsViewModel.SelectLanguageAsync` (ADR-064): the
interface-language picker's four rows (System default, English, Русский, Español) all bind
`SelectCommand` to the **same** `SelectLanguageCommand` instance, built once in the
constructor. It was `async Task`, so picking any language flashed all four rows
disabled/re-enabled for the length of the settings write — reported by the user as the
picker's own rows blinking.

An unrelated, real defect was fixed first in the same investigation
([[avalonia-itemssource-clear-deselects-and-recreates-content]] — a `ContentControl` tearing
down and rebuilding the whole settings page on the same `CultureChanged` event) and did
**not** resolve the report, because it was a different bug in the same feature. This pattern
— multiple `UiLanguageDisplayItem`/`WhisperModelDisplayItem`-style rows sharing one command —
is common across Parlotype's settings pages (CLAUDE.md's "Flyout bindings" convention), so
**any list of buttons/rows sharing a command is the first thing to check** when the reported
symptom is "the list flashes/flickers," before looking at collection rebuilds, DynamicResource,
or rendering. The regression test can't just check the view model's final state — it has to
assert the command's runtime *type* (`Assert.IsNotAssignableFrom<IAsyncRelayCommand>(...)`),
since a mock's already-completed `Task` makes the `IsRunning` window too short to observe by
timing.
