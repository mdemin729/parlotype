---
title: AsyncRelayCommand CanExecute Flicker in ItemsControl
type: knowledge
tags: [communitytoolkit, mvvm, avalonia, ui]
created: 2026-04-30
last_updated: 2026-04-30
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
