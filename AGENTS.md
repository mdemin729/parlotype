# AGENTS.md — Parlotype

Instructions for AI agents working on this codebase.

## Architecture

Parlotype is a local-first voice-to-text desktop app with seven projects:

- **Parlotype.Core** — Domain interfaces and models. Zero external dependencies. All contracts live here. Subfolders: `Audio/`, `Hotkeys/`, `Settings/`, `Speech/`, `TextProcessing/`.
- **Parlotype.Platform** — Implements Core interfaces with real libraries (Whisper.net, NAudio, SileroVad, SharpHook). Subfolders mirror Core: `Audio/`, `Hotkeys/`, `Settings/`, `Speech/`.
- **Parlotype.Desktop** — Avalonia UI app (11.3.0, Fluent theme). Entry point. Wires DI, hosts views/viewmodels.
- **Parlotype.Benchmark** — Console app for evaluating speech recognition quality. Runs Whisper against WAV datasets, computes WER/CER/RTF metrics, outputs JSON results with Spectre.Console tables. Uses `System.CommandLine` for CLI. Subfolders: `Configuration/`, `Metrics/`, `Pipeline/`, `Results/`, `Reporting/`.
- **Parlotype.Tests** — xUnit tests for Core and Platform (audio pipeline, VAD, Whisper).
- **Parlotype.Desktop.Tests** — Avalonia headless UI tests using `Avalonia.Headless.XUnit`. Uses `[AvaloniaFact]` instead of `[Fact]`. Mock services in `Mocks/` folder.
- **Parlotype.Benchmark.Tests** — xUnit tests for benchmark metrics (WER/CER calculation, text normalization, config deserialization).

**Dependency direction:** Desktop → Platform → Core. Benchmark → Platform → Core. Tests → Core, Platform. Desktop.Tests → Desktop, Core. Benchmark.Tests → Benchmark, Core.

## Coding Conventions

- **Target framework:** .NET 10 (`net10.0`)
- **Nullable reference types:** Enabled globally — never suppress without justification
- **Warnings as errors:** Enabled — all warnings must be resolved
- **Implicit usings:** Enabled
- **MVVM pattern:** Use `CommunityToolkit.Mvvm` with source generators (`[ObservableProperty]`, `[RelayCommand]`)
- **DI:** `Microsoft.Extensions.DependencyInjection` — register services in `PlatformServiceExtensions.cs`
- **Interfaces in Core, implementations in Platform** — never add platform-specific packages to Core
- **AXAML:** Always use `x:CompileBindings="True"` and `x:DataType` on all AXAML files
- **Design-time data:** Use `<Design.DataContext>` with parameterless ViewModel constructors backed by design stubs
- **Flyout bindings:** Flyouts are disconnected from the visual tree — embed commands directly in display item wrappers (e.g. `MicrophoneDisplayItem`, `WaitTimeDisplayItem`, `WhisperModelDisplayItem`) instead of using `$parent` traversal bindings
- **Whisper model selection:** `WhisperModelType` enum in Core maps to `GgmlType` in Platform via `WhisperModelTypeExtensions`. `WhisperModelInfo` holds static metadata (display name, disk size, SHA). Model choice is persisted via `SettingsKeys.SelectedWhisperModel` and read by `WhisperSpeechRecognizer` at initialization.
- **Whisper parameters:** `WhisperOptions` record in Core configures model, language, beam size, temperature, and initial prompt. `ISpeechRecognizer.InitializeAsync(WhisperOptions)` overload applies these; the no-args overload reads from settings (desktop default). `WhisperSpeechRecognizer` uses greedy decoding for beam size 1 and beam search for larger values.
- **Model download:** `IModelDownloadService` (Core) → `HttpModelDownloadService` (Platform, HTTP with progress) → `ModelDownloadDialogService` (Desktop, modal confirmation dialog + progress bar). Tests use a `HeadlessModelDownloadService` that downloads without UI.
- **Conditional CSS classes:** Use `Classes.xxx="{Binding Property}"` with `<Window.Styles>` for visual state changes (e.g. `Classes.recording="{Binding IsRecording}"` on the microphone button)
- **Flyout lifecycle:** Avalonia flyouts lack MVVM-friendly lifecycle bindings — use code-behind to hook `PopupFlyoutBase.Opening` for refreshing ViewModel data when flyouts open (see `SettingsFlyoutView.axaml.cs`)

## Build & Test

```bash
dotnet build Parlotype.slnx      # Must compile with zero warnings
dotnet test                       # All tests must pass (platform + headless UI + benchmark)
dotnet run --project src/Parlotype.Desktop  # Launch the app
```

```powershell
dotnet build Parlotype.slnx      # Must compile with zero warnings
dotnet test                       # All tests must pass (platform + headless UI + benchmark)
dotnet run --project src\Parlotype.Desktop  # Launch the app
```

### Benchmark

```bash
dotnet run --project src/Parlotype.Benchmark -- run \
  --config datasets/smoke-test-config.json \
  --datasets datasets \
  --output results
```

```powershell
dotnet run --project src\Parlotype.Benchmark -- run `
  --config datasets\smoke-test-config.json `
  --datasets datasets `
  --output results
```

**Note:** File lock errors from running `.NET Host` processes are common. Kill the locking process by PID before rebuilding.

## Key Patterns

- New domain contracts → add interface to `Parlotype.Core` in the appropriate subfolder
- New platform implementations → add to `Parlotype.Platform` and register in `PlatformServiceExtensions.cs`
- New UI features → add ViewModels to `Parlotype.Desktop/ViewModels/` and Views to `Parlotype.Desktop/Views/`
- Extract reusable UI components into separate UserControls (e.g. `MicrophoneSettingsView`)
- Always write tests for logic in Core and Platform
- Write benchmark metrics tests in `Parlotype.Benchmark.Tests` for WER/CER calculators and text normalization
- Write headless UI tests in `Parlotype.Desktop.Tests` for view/viewmodel integration — use `MockMicrophoneEnumerator` and `MockSettingsService` for controllable testing
- `ObservableCollection` mutations from background threads must dispatch to `Avalonia.Threading.Dispatcher.UIThread`
