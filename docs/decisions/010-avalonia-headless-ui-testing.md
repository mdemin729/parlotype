---
status: accepted
date: 2026-02-18
---

# 010. Avalonia Headless UI Testing

## Context

UI logic in ViewModels and View code-behind (keyboard handling, flyout interactions, visual state) needs automated testing. Traditional UI testing requires a display server and is flaky. Avalonia provides a headless testing framework that renders the visual tree without a real window.

## Decision

Use `Avalonia.Headless.XUnit` (11.3.0) for display-less UI integration tests.

**Test Infrastructure:**

- `Avalonia.Headless.XUnit` NuGet package provides the headless rendering backend
- `TestAppBuilder` class configures the headless Avalonia app with mock services registered via DI
- Tests use `[AvaloniaFact]` attribute (replaces `[Fact]`) to run on the Avalonia UI thread
- Separate test project: `Parlotype.Desktop.Tests`

**Mock Services:**

- All Core interfaces have mock implementations in `Desktop.Tests/Mocks/` folder
- `MockAudioCaptureService`, `MockSpeechRecognizer`, `MockSettingsService`, `MockGlobalHotkeyService`, etc.
- Mocks are controllable: `SimulatePress()`, `SetDevices()`, `SetSetting()` for deterministic testing
- Registered in TestAppBuilder replacing real Platform services

**Test Patterns:**

- Create Window, verify initial state, simulate user actions, assert ViewModel/View changes
- `Window.KeyPressQwerty(Key.X, RawInputModifiers.Control)` for keyboard simulation
- `Dispatcher.UIThread.RunJobs()` to flush pending UI updates
- `typeof(MainWindow).GetField("_viewModel", BindingFlags.NonPublic)` for ViewModel access when needed

**What's Tested:**

- MainWindow initial state (recording button enabled, status text)
- Recording toggle via button click
- Settings flyout ViewModel binding
- Hotkey recorder keyboard capture and conflict detection
- PTT/Toggle mode switching via hotkey events

## Consequences

- **Easier:** UI tests run in CI without a display server. Fast execution (no window management overhead). Deterministic — no timing-dependent failures.
- **Easier:** Mock services provide full control over test scenarios. Can simulate device errors, transcription results, hotkey events.
- **Harder:** Avalonia headless has quirks — flyouts don't fully render, some visual properties differ from real rendering.
- **Harder:** Reflection needed to access private ViewModel fields. Code-behind event handlers harder to test than pure ViewModel logic.
