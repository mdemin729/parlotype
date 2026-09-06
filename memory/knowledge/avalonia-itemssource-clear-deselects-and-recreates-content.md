---
title: ObservableCollection.Clear() on a bound ItemsSource deselects, and ContentControl rebuilds on the round trip
type: knowledge
tags: [avalonia, listbox, contentcontrol, mvvm, ui]
created: 2026-09-06
last_updated: 2026-09-06
summary: Clearing an ObservableCollection bound to ListBox.ItemsSource nulls a two-way SelectedItem even when the item is about to be re-added; if a ContentControl elsewhere binds to a property derived from that selection, it tears down and reconstructs its templated child on the null round trip
---

# ObservableCollection.Clear() deselects, and a dependent ContentControl rebuilds

## Fact

`ListBox.ItemsSource="{Binding Items}"` with `SelectedItem="{Binding Selected, Mode=TwoWay}"`:
calling `Items.Clear()` — even immediately followed by re-adding every item, including a
replacement for the one that was selected — makes the ListBox deselect. The `Reset` from
`Clear()` invalidates whatever index/reference the control was tracking, so it pushes
`SelectedItem = null` back through the two-way binding before your code re-adds anything.

If some other control's `Content`/property is bound to something derived from that selection
(e.g. `ContentControl.Content="{Binding SelectedSection}"` where `SelectedSection =>
SelectedNavItem?.Section`), it observes `value -> null -> value` in the same rebuild, even
though the logical selection never should have changed. A `ContentControl` does not cache a
templated child across `Content` going through `null` — going to `null` disposes it, and
coming back constructs a **brand-new** instance from the `DataTemplate`. The fresh instance's
child controls (e.g. `Button`s inside an `ItemsControl`) render before pointer/pseudo-class
state settles, which is what reads as a flash/flicker rather than a stable page.

This is easy to miss because rebuilding a whole collection and then restoring the selection
"looks" like a no-op if you only check the final state (`Assert.Same(section, vm.Selected)`
after the fact passes fine) — the bug is only visible mid-operation, or in the rendered
control tree, not in the model's end state.

## Fix

Never let the bound collection go through a state where the still-wanted row is absent.
Append the new items, reassign the selection (now pointing at an item already present in the
collection), *then* remove the old ones — all synchronously, so nothing renders the
intermediate list:

```csharp
var oldCount = Items.Count;
foreach (var item in newItems)
    Items.Add(item);

Selected = target;   // target is already in Items — no deselect round trip

for (var i = 0; i < oldCount; i++)
    Items.RemoveAt(0);
```

## How to catch it in a test

A test against the bare view model will not see this: without a real `ListBox` bound to the
collection, there is nothing to deselect on `Clear()`, and `Assert.Same` on the final selected
value passes on both the buggy and fixed code. You need the real view instantiated and shown,
then compare the *rendered* templated child's identity before and after:

```csharp
var window = new SettingsWindow { DataContext = vm };
window.Show();
var contentControl = window.GetVisualDescendants().OfType<ContentControl>()
    .First(c => c.Content is TargetViewModel);
var before = contentControl.GetVisualDescendants().OfType<TargetView>().Single();

TriggerTheRebuild();

var after = contentControl.GetVisualDescendants().OfType<TargetView>().Single();
Assert.Same(before, after);
```

## Context

Found in Parlotype's Settings window (ADR-064 amendment): `SettingsWindowViewModel.RebuildNavItems()`
ran on every `Localizer.CultureChanged` (needed because nav row labels are static snapshots,
not bound) via `NavItems.Clear()` + re-`Add`. `SettingsWindow.axaml`'s `ContentControl` binds
`Content` to `SelectedSection`, which is `SelectedNavItem?.Section`. Every interface-language
switch tore down and rebuilt whichever settings page was open — reported by the user as the
language picker's own rows flickering when they picked a language, since selecting a language
is exactly what fires `CultureChanged`. See [[culture-changing-tests-need-avaloniafact]] for a
related but different `Localizer.CultureChanged` pitfall (thread affinity, not this one).
