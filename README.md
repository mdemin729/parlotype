# Parlotype

**Speak freely. Type privately.**

Parlotype is a local-first, privacy-focused voice-to-text desktop application. All speech recognition runs on-device using [Whisper](https://github.com/openai/whisper) — your voice data never leaves your machine.

## Tech Stack

- **.NET 10** — Runtime
- **Avalonia UI 11** — Cross-platform desktop UI
- **Whisper.net** — On-device speech recognition (OpenAI Whisper)
- **NAudio** — Windows audio capture (WASAPI)
- **CommunityToolkit.Mvvm** — MVVM framework
- **SharpHook** — Global hotkeys

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build & Run

```bash
dotnet build Parlotype.sln
dotnet run --project src/Parlotype.Desktop
```

## Run Tests

```bash
dotnet test
```

## Project Structure

```
src/
├── Parlotype.Core/        # Domain interfaces and models (zero external deps)
├── Parlotype.Platform/    # Platform-specific implementations (Whisper, NAudio, SharpHook)
├── Parlotype.Desktop/     # Avalonia UI application (entry point)
└── Parlotype.Tests/       # Unit tests (xUnit)
```

## Architecture

- **Core** defines interfaces (`ISpeechRecognizer`, `IAudioCaptureService`, etc.)
- **Platform** implements those interfaces with real dependencies
- **Desktop** wires everything via dependency injection and provides the UI
- Clean separation ensures Core has no UI or platform dependencies
- Users can select from all available Whisper GGML models (Tiny through Large v3 Turbo) in the settings menu

## License

TBD
