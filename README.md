# Parlotype

**Speak freely. Type privately.**

Parlotype is a local-first, privacy-focused voice-to-text desktop application. All speech recognition runs on-device using [Whisper](https://github.com/openai/whisper) — your voice data never leaves your machine.

## Tech Stack

- **.NET 10** — Runtime
- **Avalonia UI 11** — Cross-platform desktop UI
- **Whisper.net** — On-device speech recognition (OpenAI Whisper)
- **Silero VAD** — Voice activity detection
- **NAudio** — Windows audio capture (WASAPI)
- **CommunityToolkit.Mvvm** — MVVM framework
- **SharpHook** — Global hotkeys

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build & Run

```bash
dotnet build Parlotype.slnx
dotnet run --project src/Parlotype.Desktop
```

```powershell
dotnet build Parlotype.slnx
dotnet run --project src\Parlotype.Desktop
```

## Run Tests

```bash
dotnet test
```

```powershell
dotnet test
```

## Benchmark

Evaluate speech recognition quality with the built-in benchmark tool:

```bash
# Run a benchmark
dotnet run --project src/Parlotype.Benchmark -- run \
  --config datasets/smoke-test-config.json \
  --datasets datasets \
  --output results

# List historical benchmark runs
dotnet run --project src/Parlotype.Benchmark -- list --output results

# Compare two runs side by side
dotnet run --project src/Parlotype.Benchmark -- compare \
  --run-a <run-id-a> --run-b <run-id-b> --output results

# Export a run as CSV, Markdown, or JSON
dotnet run --project src/Parlotype.Benchmark -- export \
  --run-id <run-id> --format markdown --output results

# Rebuild SQLite index from existing JSON result files
dotnet run --project src/Parlotype.Benchmark -- import --output results
```

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
```

The benchmark computes **WER** (Word Error Rate), **CER** (Character Error Rate), and **RTF** (Real-Time Factor) against WAV/FLAC datasets with ground-truth transcriptions. Results are saved as JSON and auto-indexed into SQLite for historical queries. Supports tag/sample filtering (`--tags`, `--samples`), side-by-side comparison with delta metrics, and export to CSV, Markdown, or JSON.

## Project Structure

```
src/
├── Parlotype.Core/        # Domain interfaces and models (zero external deps)
├── Parlotype.Platform/    # Platform-specific implementations (Whisper, NAudio, SharpHook)
├── Parlotype.Desktop/     # Avalonia UI application (entry point)
├── Parlotype.Benchmark/   # CLI benchmark tool (WER/CER/RTF, compare, export)
├── Parlotype.Benchmark.Tests/ # Benchmark unit tests
└── Parlotype.Tests/       # Unit tests (xUnit)

datasets/
└── smoke-test/            # Sample WAV dataset with ground truth for benchmarking
```

## Architecture

- **Core** defines interfaces (`ISpeechRecognizer`, `IAudioCaptureService`, etc.)
- **Platform** implements those interfaces with real dependencies
- **Desktop** wires everything via dependency injection and provides the UI
- Clean separation ensures Core has no UI or platform dependencies
- Users can select from all available Whisper GGML models (Tiny through Large v3 Turbo) in the settings menu
- Configurable Whisper parameters (language, beam size, temperature) via `WhisperOptions` for benchmarking

## License

TBD
