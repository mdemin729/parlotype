---
title: Avalonia's GetObservable(...).Subscribe(lambda) is unreachable from application code
type: knowledge
tags: [avalonia, reactive, observable, compile-error, cs1660, cs0122]
created: 2026-09-13
last_updated: 2026-09-13
summary: AvaloniaObject.GetObservable(prop) returns IObservable<T>, whose own Subscribe(IObserver<T>) member wins dot-style overload resolution before the Action<T>-taking Avalonia.Reactive.Observable.Subscribe extension is even considered (CS1660) — and that extension type is internal to Avalonia.Base anyway (CS0122), so it cannot be called qualified either. Use a plain AvaloniaObject.PropertyChanged subscription filtered by property instead.
---

# Avalonia's `GetObservable(...).Subscribe(lambda)` is unreachable from application code

Found while implementing [[../decisions/_index|ADR-068]] (Transcribe widget auto-hide).
`TranscribeAutoHideController` needed to react to the Transcribe window's `IsPointerOver`
and `IsVisible` changing. The implementation plan sketched the obvious-looking call:

```csharp
window.GetObservable(InputElement.IsPointerOverProperty).Subscribe(isOver => Evaluate());
```

This does not compile from `Parlotype.Desktop` — or from any code outside
`Avalonia.Base` — for two independent reasons that both have to be understood, because
neither has a one-line fix on its own.

## Why it fails

1. **CS1660 — the lambda overload is never even a candidate.** `GetObservable` returns
   `IObservable<T>`, and `IObservable<T>` itself declares an instance member
   `Subscribe(IObserver<T>)`. C#'s dot-style member lookup finds that instance member and
   stops looking — extension methods (including the `Action<T>`-taking
   `Avalonia.Reactive.Observable.Subscribe` overload that would accept the lambda) are only
   considered when no applicable instance member of that name exists, and here one does.
   The compiler rejects the lambda as failing to convert to `IObserver<T>` — it is trying
   to satisfy the *instance* `Subscribe`, never reaching the extension overload at all.
2. **CS0122 — the extension is not accessible either.** Even written out fully qualified
   (`Avalonia.Reactive.Observable.Subscribe(window.GetObservable(...), isOver => ...)`),
   the call still fails: `Avalonia.Reactive.Observable`, the type that declares the
   `Action<T>`-taking `Subscribe` extension for Avalonia's own observables, is `internal`
   to the `Avalonia.Base` assembly. It is invisible to any other assembly regardless of how
   the call is spelled.

Together these rule out both the natural dot-call and the "just qualify it" workaround —
this is not a missing-`using` or overload-ambiguity problem that a cast or explicit type
argument fixes. The extension method is simply unreachable from application code, full
stop, for as long as it stays `internal`.

## The workaround actually shipped

`TranscribeAutoHideController` subscribes to the window's plain `AvaloniaObject.PropertyChanged`
event once, in its constructor, and filters by `AvaloniaPropertyChangedEventArgs.Property`
inside the handler — one subscription covering both properties it cares about:

```csharp
_window.PropertyChanged += OnWindowPropertyChanged;

private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
{
    if (e.Property == InputElement.IsPointerOverProperty)
        Evaluate();
    else if (e.Property == Visual.IsVisibleProperty && e.NewValue is false)
        Reset();
}
```

`PropertyChanged` is public API on every `AvaloniaObject` and needs no extra import beyond
`Avalonia` itself.

## When this bites

Any code outside `Avalonia.Base` that reaches for `someAvaloniaObject.GetObservable(prop)
.Subscribe(lambda)` — a pattern that looks completely idiomatic (Avalonia's own samples and
internal source use it freely, because code *inside* `Avalonia.Base` can see the internal
extension). `Subscribe(IObserver<T>)` still works if you hand it an actual `IObserver<T>`
(a small hand-rolled implementation, or `Observer.Create(...)` if `System.Reactive` is
already referenced), but for a single property-change reaction a filtered `PropertyChanged`
handler is less ceremony than authoring or pulling in an `IObserver<T>`.

See `src/Parlotype.Desktop/Services/TranscribeAutoHideController.cs` for the shipped code.
