# Parlotype

**Speak freely. Type privately.**

Parlotype is a local-first, privacy-focused voice-to-text desktop application. All speech recognition runs on-device using [Whisper](https://github.com/openai/whisper) — your voice data never leaves your machine.

## Tech Stack

- **.NET 10** — Runtime
- **Avalonia UI 12** — Cross-platform desktop UI (tray-based)
- **Whisper.net** — On-device speech recognition (OpenAI Whisper)
- **Silero VAD** — Voice activity detection
- **NAudio** — Windows audio capture (WASAPI)
- **CommunityToolkit.Mvvm** — MVVM framework
- **SharpHook** — Global hotkeys

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build & Run

```powershell
dotnet build Parlotype.slnx
dotnet run --project src\Parlotype.Desktop.V2
```

The app starts minimized to the system tray. Click the tray icon for an Open / Settings / Exit menu, or press the global hotkey to open the Transcribe window and start recording.

### Visual inspector (Debug builds)

The desktop app supports the official Avalonia 12 Developer Tools (Essentials
edition — free under AvaloniaUI's community licence for organisations under
€1M revenue). Setup is per-developer:

1. Install the standalone tool once:

   ```powershell
   dotnet tool install --global AvaloniaUI.DeveloperTools
   ```

2. Launch it in a separate window:

   ```powershell
   avdt
   ```

3. Run the app in **Debug** configuration, give a Parlotype window focus, and press
   **F12**. The inspector will connect and show the visual tree, properties,
   layout, and styles.

First-time activation requires a free [AvaloniaUI Portal](https://avaloniaui.net/)
account. The `AvaloniaUI.DiagnosticsSupport` package is referenced with a
`Configuration == Debug` condition, so Release builds carry no extra binaries.
See [ADR 016](docs/decisions/016-avalonia12-developer-tools.md).

## Run Tests

```powershell
dotnet test
```

## Benchmark

Evaluate speech recognition quality with the built-in benchmark tool:

```powershell
# Run a benchmark
dotnet run --project src\Parlotype.Benchmark -- run `
  --config datasets\smoke-test-config.json `
  --datasets datasets `
  --output results

# List historical benchmark runs
dotnet run --project src\Parlotype.Benchmark -- list --output results

# Compare two runs side by side
dotnet run --project src\Parlotype.Benchmark -- compare `
  --run-a <run-id-a> --run-b <run-id-b> --output results

# Export a run as CSV, Markdown, or JSON
dotnet run --project src\Parlotype.Benchmark -- export `
  --run-id <run-id> --format markdown --output results

# Rebuild SQLite index from existing JSON result files
dotnet run --project src\Parlotype.Benchmark -- import --output results

# Run a parameter sweep across configurations
dotnet run --project src\Parlotype.Benchmark -- sweep `
  --config datasets\sweep-config.json `
  --datasets datasets `
  --output results

# Check for regressions against a baseline (for CI)
dotnet run --project src\Parlotype.Benchmark -- check `
  --baseline <run-id> --current latest `
  --output results --max-wer-delta 2.0
```

The benchmarkcomputes **WER** (Word Error Rate), **CER** (Character Error Rate), and **RTF** (Real-Time Factor) against WAV/FLAC datasets with ground-truth transcriptions. Results are saved as JSON and auto-indexed into SQLite for historical queries. Supports tag/sample filtering (`--tags`, `--samples`), side-by-side comparison with delta metrics, and export to CSV, Markdown, or JSON.

## Project Structure

```
src/
├── Parlotype.Core/            # Domain interfaces and models (zero external deps)
├── Parlotype.Platform/        # Platform-specific implementations (Whisper, NAudio, SharpHook)
├── Parlotype.Desktop.V2/      # Avalonia 12 desktop app (tray-based, entry point)
├── Parlotype.Desktop.V2.Tests/ # Avalonia headless UI tests (xUnit v3)
├── Parlotype.Benchmark/       # CLI benchmark tool (WER/CER/RTF, sweep, compare, CI check)
├── Parlotype.Benchmark.Tests/ # Benchmark unit tests
└── Parlotype.Tests/           # Core + Platform unit tests (xUnit)

datasets/
└── smoke-test/                # Sample WAV dataset with ground truth for benchmarking
```

## Architecture

- **Core** defines interfaces (`ISpeechRecognizer`, `IAudioCaptureService`, etc.)
- **Platform** implements those interfaces with real dependencies
- **Desktop** wires everything via dependency injection and provides the UI
- Clean separation ensures Core has no UI or platform dependencies
- Users can select from all available Whisper GGML models (Tiny through Large v3 Turbo) in the settings menu
- Configurable Whisper parameters (language, beam size, temperature, thread count) via `WhisperOptions` for benchmarking

## License

This project is licensed under the [MIT License](LICENSE).
