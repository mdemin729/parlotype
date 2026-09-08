---
title: Headless screenshot tests need Skia, not the headless drawing backend
type: knowledge
tags: [avalonia, headless, testing, screenshots, skia, xunit]
created: 2026-09-07
last_updated: 2026-09-07
summary: CaptureRenderedFrame returns null under Avalonia's default headless drawing backend — UseSkia() plus UseHeadlessDrawing=false is what makes pixels exist, and the failure is silent
---

# Headless screenshot tests need Skia, not the headless drawing backend

Learned while building the runtime-validation screenshot suite, restated a
fortnight later during the benchmark warm-up work — the same trap caught two
separate sessions, which is why it is written down here.

## The setup that works

`Window.CaptureRenderedFrame()` only produces a bitmap when the test app is
built with the **real Skia renderer** and the headless platform's own drawing
backend explicitly disabled:

```csharp
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<TestApp>()
                 .UseSkia()
                 .UseHeadless(new AvaloniaHeadlessPlatformOptions
                 {
                     UseHeadlessDrawing = false,
                 });
```

Live in `src/Parlotype.Desktop.Tests/TestAppBuilder.cs`.

## Why it is easy to lose

`UseHeadlessDrawing = true` (the default) installs a **stub** drawing backend:
layout runs, bindings evaluate, hit-testing works, and every non-visual
assertion passes exactly as it would with Skia. Only rasterisation is absent.
`CaptureRenderedFrame` then returns **null** — no exception, no warning, no log
line. A screenshot test written against the default backend fails on a
`NullReferenceException` far from the cause, or worse, silently asserts nothing
if it null-guards the bitmap.

The corollary: a green headless suite is **not** evidence that anything
rendered. Verify visual changes by sampling actual pixels — see
[[avalonia-resource-scope-in-headless-tests]] for the sibling trap where
resources resolve to null and colours blank out with every assertion still
passing.

## Collecting a report across all tests

Screenshot suites usually want one HTML report after the last test, not a file
per test. The xUnit v3 pattern is `IClassFixture<T>` where the fixture
implements `IAsyncLifetime` and writes the report in `DisposeAsync` — fixture
teardown runs once, after every test in the class, which sidesteps the
parallel-execution timing races you get from trying to detect "the last test".
Parlotype's fixtures emit `reports/*.html` with base64-embedded PNGs; extracting
one and viewing it verifies UI copy without launching the app.

Related: [[avalonia-popup-patterns]] (`CaptureRenderedFrame` also excludes the
popup layer, so popover content must be screenshotted directly),
[[culture-changing-tests-need-avaloniafact]].
