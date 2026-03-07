# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Parlotype is a local-first, privacy-focused voice-to-text desktop application. All speech recognition runs on-device using Whisper.net — voice data never leaves the user's machine. Built with .NET 10 and Avalonia UI.

## Build & Test Commands

```bash
dotnet build Parlotype.slnx          # Build entire solution (must compile with zero warnings)
dotnet test                           # Run all tests (platform + headless UI + benchmark)
dotnet test src/Parlotype.Tests       # Run only core/platform tests
dotnet test src/Parlotype.Desktop.Tests    # Run only Avalonia headless UI tests
dotnet test src/Parlotype.Benchmark.Tests  # Run only benchmark tests
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"  # Run a single test
dotnet run --project src/Parlotype.Desktop  # Launch the app
```

**Benchmark CLI:**
```bash
dotnet run --project src/Parlotype.Benchmark -- run \
  --config datasets/smoke-test-config.json \
  --datasets datasets --output results
```

Other benchmark commands: `list`, `compare`, `export`, `import` (see AGENTS.md for full usage).

**Note:** File lock errors from `.NET Host` processes are common on Windows. Kill the locking process by PID before rebuilding.

## Architecture

**Solution:** `Parlotype.slnx` (modern .slnx format) with 7 projects.

**Dependency direction:** `Desktop → Platform → Core` and `Benchmark → Platform → Core`.

| Project | Purpose |
|---------|---------|
| **Parlotype.Core** | Domain interfaces and models. Zero external dependencies. Subfolders: `Audio/`, `Hotkeys/`, `Settings/`, `Speech/`, `TextInjection/` |
| **Parlotype.Platform** | Implements Core interfaces (Whisper.net, NAudio/WASAPI, SileroVad, SharpHook). Register new services in `PlatformServiceExtensions.cs` |
| **Parlotype.Desktop** | Avalonia UI 11.3.0 app. Entry point. DI wiring in `App.axaml.cs` |
| **Parlotype.Benchmark** | Console CLI for evaluating transcription quality (WER/CER/RTF). System.CommandLine + Spectre.Console + SQLite |
| **Parlotype.Tests** | xUnit tests for Core and Platform |
| **Parlotype.Desktop.Tests** | Avalonia headless UI tests — uses `[AvaloniaFact]` instead of `[Fact]` |
| **Parlotype.Benchmark.Tests** | xUnit tests for benchmark metrics, comparison engine, formatters |

### Audio Pipeline Data Flow

```
WASAPI Capture → 16kHz Mono Float → Silero VAD → Speech Segments → Whisper Transcription → Text Injection
```

- **Batch mode** (default): buffers audio, detects end-of-speech via silence
- **Streaming mode**: processes fixed 3-second windows
- Capture and transcription run on separate threads; `ConcurrentQueue<float[]>` bridges them

### Key Subsystems

- **Text Injection:** `ClipboardTextInjectionService` (default, saves/restores clipboard around Ctrl+V) or `SharpHookTextInjectionService` (direct key simulation). `Win32TargetWindowTracker` tracks the last non-Parlotype foreground window.
- **Settings:** `JsonSettingsService` persists to `%LOCALAPPDATA%/parlotype/settings.json`. Thread-safe via `SemaphoreSlim`.
- **Logging:** ZLogger to console + rolling file in `%LOCALAPPDATA%/parlotype/logs/`.
- **Model Management:** `IModelDownloadService` → `HttpModelDownloadService` (Platform) → `ModelDownloadDialogService` (Desktop, shows modal) or `HeadlessModelDownloadService` (Benchmark, silent).

## Coding Conventions

- **.NET 10** target framework, nullable reference types enabled, **warnings as errors** (`TreatWarningsAsErrors=true` in `Directory.Build.props`)
- **MVVM:** `CommunityToolkit.Mvvm` with source generators — use `[ObservableProperty]` on private fields and `[RelayCommand]` on methods. ViewModels must be `partial` classes.
- **AXAML:** Always use `x:CompileBindings="True"` and `x:DataType`. Never use `{ReflectionBinding}`. Avalonia uses `.axaml`, not `.xaml`.
- **Flyout bindings:** Flyouts are disconnected from the visual tree — embed commands in display item wrappers (e.g. `MicrophoneDisplayItem`) instead of using `$parent` traversal bindings.
- **Interfaces in Core, implementations in Platform** — never add platform-specific packages to Core.
- **DI registration:** Add new services in `PlatformServiceExtensions.cs`. All services are singletons.
- **Background → UI thread:** `ObservableCollection` mutations from background threads must dispatch to `Avalonia.Threading.Dispatcher.UIThread`.
- **Benchmark output:** Use Spectre.Console, not `Console.WriteLine`.
- **Whisper model lifecycle:** Never load the Whisper model multiple times in a single run.
