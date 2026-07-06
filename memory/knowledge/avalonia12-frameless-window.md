---
type: knowledge
tags: [avalonia, avalonia12, window, frameless, drag, headless-testing]
created: 2026-07-05
summary: Avalonia 12 frameless-window gotchas — WindowDecorations replaces SystemDecorations; BeginMoveDrag blocks until drop on Windows; headless Screens is populated, not empty
---

# Avalonia 12 Frameless Window Gotchas

- **`SystemDecorations` is obsolete in Avalonia 12** — the XAML compiler emits `AVLN5001`, which `TreatWarningsAsErrors` turns into a build failure in test projects referencing the property from C#. Use `Window.WindowDecorations` with the `WindowDecorations` enum (`None` / `BorderOnly` / `Full`). Note: AXAML-only usages of obsolete members (e.g. `ModelDownloadDialog.axaml` still on `SystemDecorations`) currently surface as warnings that do *not* fail the build, unlike C# usages.
- **Rounded corners need a transparent window**: with `WindowDecorations="None"` the window surface is still a rectangle; set `TransparencyLevelHint="Transparent"` + `Background="Transparent"` on the window and draw the visible card as a root `Border` with `CornerRadius` (pattern used by `TranscribeWindow`, ADR-040).
- **`BeginMoveDrag(PointerPressedEventArgs)` blocks on Windows**: it enters the Win32 modal move loop (`SC_MOVE`) and returns only when the user drops the window — so code right after the call runs at drag-end, a natural place to persist `Window.Position`. Headless platform treats it as a no-op, so drag behaviour can't be unit-tested.
- **Headless `Screens.All` is NOT empty — correction of an earlier entry here.** Verified empirically (`window.Screens.All` read both before and after `Show()`): the headless platform reports one real virtual screen, `PixelRect(0,0,1920,1280)` at 1.0 scaling, available even on a freshly constructed, unshown window. `Width`/`Height` set via AXAML are also already correct pre-`Show()` (they're plain properties, not layout-computed). So an off-screen/on-screen `PixelRect.Intersects` check genuinely exercises real geometry in headless tests — it is not silently hitting a "no screen info, trust blindly" fallback. Only design that fallback for platforms that truly return null/empty (defensive, not the common case).
