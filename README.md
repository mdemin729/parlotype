# Parlotype

**Speak freely. Type privately.**

Parlotype is a **local-by-default** voice-to-text desktop application: on-device speech recognition is the default, and your voice never leaves your machine in local mode. Choose between two local engines — **[Whisper](https://github.com/openai/whisper)** (fast, well-tested, the default) and **[Gemma 4](https://deepmind.google/models/gemma/gemma-4/)** (Google's multimodal model, run via a local [llama.cpp](https://github.com/ggml-org/llama.cpp) sidecar). Cloud speech providers are planned as an **opt-in** option for users whose hardware can't deliver the latency they need — see [Provider Modes](#provider-modes) below and [ADR-032](docs/decisions/032-online-speech-providers-positioning.md).

## Tech Stack

- **.NET 10** — Runtime
- **Avalonia UI 12** — Cross-platform desktop UI (tray-based)
- **Whisper.net** — On-device speech recognition (OpenAI Whisper)
- **llama.cpp (`llama-server`)** — Gemma 4 speech recognition sidecar
- **Silero VAD** — Voice activity detection
- **NAudio** — Windows audio capture (WASAPI)
- **CommunityToolkit.Mvvm** — MVVM framework
- **SharpHook** — Global hotkeys

## Platform Support

Parlotype currently runs on **Windows** only. macOS and Linux support are planned for the future.

**GPU acceleration** is supported on **NVIDIA** GPUs (via CUDA) and on **AMD / Intel / other** GPUs (via Vulkan). If no compatible GPU is detected, Parlotype falls back to CPU automatically. The active runtime can be changed in **Settings → Runtime**.

## Download / Releases

Pre-built Windows binaries are published on the [Releases page](../../releases). Each
release ships two self-contained `win-x64` builds — no .NET runtime install required, just
unzip and run `Parlotype.Desktop.exe`:

| Build | Contents | Use when |
|-------|----------|----------|
| **Full** | CUDA + Vulkan GPU runtimes | You have an **NVIDIA** GPU and want CUDA acceleration (also requires the [CUDA toolkit](#cuda-optional-nvidia-gpus) installed) |
| **Lite** | Vulkan GPU runtime only | Everything else — AMD / Intel GPUs, or you prefer a smaller download (NVIDIA still works via Vulkan) |

Both are self-contained, so they're large (Lite ~720 MB, Full ~870 MB unzipped; the
downloaded zips are smaller). Both fall back to CPU automatically if no compatible GPU is
found. The builds are currently unsigned, so Windows SmartScreen may warn on first launch.

## Speech Engines

Parlotype ships two interchangeable speech-to-text engines. Both run entirely on-device — switch between them at **Settings → Speech Engine**.

### Provider Modes

Parlotype is **local by default. Cloud by choice.**

- **Local (default)** — Whisper or Gemma 4 run entirely on your machine. No audio leaves the device. This is the only mode enabled out of the box and the only mode required to use the app.
- **Cloud (opt-in, planned)** — When local hardware can't deliver the latency you need, you will be able to enable a cloud speech provider in **Settings → Speech Engine**. When enabled, audio will be sent over HTTPS to the provider you choose using credentials you supply (bring your own key). A clear in-app indicator will show when cloud mode is active. Cloud providers will never be auto-selected.

See [ADR-032](docs/decisions/032-online-speech-providers-positioning.md) for the full positioning principles and brand commitments.

### Whisper (default)

OpenAI Whisper via [Whisper.net](https://github.com/sandrohanea/whisper.net). Fast, well-tested, and the default. You can pick any GGML model from **Tiny** through **Large v3 Turbo** in the settings menu; models download on demand. GPU-accelerated via CUDA or Vulkan (see above), with automatic CPU fallback.

### Gemma 4 (llama.cpp sidecar)

Google's multimodal **Gemma 4** model, run through a local [`llama-server`](https://github.com/ggml-org/llama.cpp) process. Parlotype manages the sidecar for you: audio is sent over a loopback HTTP connection, so nothing leaves your machine.

- **Model files** — GGUF weights plus an audio projector (`mmproj`), downloaded from HuggingFace (`ggml-org/gemma-4-E2B-it-GGUF` / `gemma-4-E4B-it-GGUF`) into `%LOCALAPPDATA%\parlotype\models\`. Sizes range from ~5.5 GiB to ~15 GiB depending on variant and quantization; the default is **E4B (Q4_K_M)** (~5.9 GiB).
- **Server build** — the `llama-server` binary is installed and managed from the Settings UI (or you can point Parlotype at your own copy).
- **English-only** for now.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### CUDA (optional, NVIDIA GPUs)

For GPU-accelerated speech recognition with an NVIDIA graphics card:

1. Install the latest NVIDIA drivers for your GPU.
2. Download and install CUDA from [developer.nvidia.com/cuda-downloads](https://developer.nvidia.com/cuda-downloads).
   - Choose **Express Installation** when prompted.
3. Restart your computer after installation.

Parlotype will automatically detect and use CUDA when available. No additional configuration is needed.

### Vulkan (optional, AMD / Intel / NVIDIA GPUs)

Vulkan is the recommended runtime when you don't have an NVIDIA GPU. Most modern GPU drivers (Radeon, Intel Arc, GeForce) already ship the Vulkan loader (`vulkan-1.dll`) — no extra install is needed for end users.

If Parlotype reports that the Vulkan loader is missing, install the **Vulkan SDK** from [vulkan.lunarg.com/sdk/home](https://vulkan.lunarg.com/sdk/home) (the SDK bundles a system-wide Vulkan loader and is also useful for development).

Parlotype will automatically detect and use Vulkan when available, in priority order **CUDA → Vulkan → CPU** (Auto mode). You can pin a specific runtime under **Settings → Runtime**.

## Build & Run

```powershell
dotnet build Parlotype.slnx
dotnet run --project src\Parlotype.Desktop
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

The benchmark computes **WER** (Word Error Rate), **CER** (Character Error Rate), and **RTF** (Real-Time Factor) against WAV/FLAC datasets with ground-truth transcriptions. Results are saved as JSON and auto-indexed into SQLite for historical queries. Supports tag/sample filtering (`--tags`, `--samples`), side-by-side comparison with delta metrics, and export to CSV, Markdown, or JSON.

## Project Structure

```
src/
├── Parlotype.Core/            # Domain interfaces and models (zero external deps)
├── Parlotype.Platform/        # Platform-specific implementations (Whisper, NAudio, SharpHook)
├── Parlotype.Desktop/          # Avalonia 12 desktop app (tray-based, entry point)
├── Parlotype.Desktop.Tests/   # Avalonia headless UI tests (xUnit v3)
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
- Speech recognition is pluggable: `SpeechRecognizerFactory` resolves either the Whisper or Gemma 4 (`LlamaCppSpeechRecognizer`) implementation of `ISpeechRecognizer` from the persisted `SpeechEngine` setting
- Users can select from all available Whisper GGML models (Tiny through Large v3 Turbo) in the settings menu
- Configurable Whisper parameters (language, beam size, temperature, thread count) via `WhisperOptions` for benchmarking
- Architectural decisions are recorded as ADRs in [`docs/decisions/`](docs/decisions/) — the Gemma 4 integration alone spans seven ADRs (024–030)

## License

This project is licensed under the [MIT License](LICENSE).
