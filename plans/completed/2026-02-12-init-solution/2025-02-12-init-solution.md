# Parlotype — .NET 10 Solution Initialization Plan

## Context

Parlotype is a local-first, privacy-focused voice-to-text desktop application ("Speak freely. Type privately."). The user has completed extensive research (3 documents in `docs/research/`) covering ASR engines, frameworks, competitive landscape, and branding. The chosen stack is **Avalonia UI** on **.NET 10** with **Whisper.net** for speech recognition. This plan initializes the solution structure so development can begin.

## Solution Architecture

Four projects with clean separation of concerns:

```
D:\projects\parlotype\
├── Parlotype.sln
├── Directory.Build.props              # Shared build settings (net10.0, nullable, etc.)
├── .gitignore
├── README.md                          # Project overview, setup instructions, tech stack
├── AGENTS.md                          # AI agent instructions for working on this codebase
├── docs/research/                     # (existing research documents)
└── src/
    ├── Parlotype.Core/                # Domain logic, interfaces, models (no UI, no platform deps)
    │   ├── Parlotype.Core.csproj
    │   ├── Audio/
    │   │   ├── IAudioCaptureService.cs
    │   │   └── AudioFormat.cs
    │   ├── Speech/
    │   │   ├── ISpeechRecognizer.cs
    │   │   └── TranscriptionResult.cs
    │   ├── TextProcessing/
    │   │   └── ITextProcessor.cs
    │   └── Hotkeys/
    │       └── IGlobalHotkeyService.cs
    │
    ├── Parlotype.Platform/            # Platform-specific implementations
    │   ├── Parlotype.Platform.csproj
    │   ├── Audio/
    │   │   └── WasapiAudioCaptureService.cs
    │   ├── Speech/
    │   │   └── WhisperSpeechRecognizer.cs
    │   ├── Hotkeys/
    │   │   └── SharpHookHotkeyService.cs
    │   └── PlatformServiceExtensions.cs   # DI registration for all platform services
    │
    ├── Parlotype.Desktop/             # Avalonia UI application (entry point)
    │   ├── Parlotype.Desktop.csproj
    │   ├── Program.cs
    │   ├── App.axaml / App.axaml.cs
    │   ├── ViewModels/
    │   │   ├── ViewModelBase.cs
    │   │   └── MainWindowViewModel.cs
    │   ├── Views/
    │   │   └── MainWindow.axaml / MainWindow.axaml.cs
    │   └── Assets/
    │       └── parlotype-logo.png     # Placeholder icon
    │
    └── Parlotype.Tests/               # Unit tests
        ├── Parlotype.Tests.csproj
        └── SampleTest.cs
```

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **MVVM Framework** | CommunityToolkit.Mvvm | Lighter than ReactiveUI, source-generated, excellent with Avalonia 11.x, no RxUI learning curve |
| **DI Container** | Microsoft.Extensions.DependencyInjection | Standard .NET DI, familiar, works well with Avalonia |
| **Avalonia Version** | 11.3.11 (latest stable) | Works with .NET 10 on desktop platforms |
| **App Lifetime** | ClassicDesktopStyleApplicationLifetime | Standard desktop app; will switch to tray-based lifetime in Phase 1 MVP |
| **Project Layout** | Core / Platform / Desktop / Tests | Clean separation: Core has zero dependencies on UI or platform specifics; Platform implements Core interfaces; Desktop wires everything via DI |

## NuGet Packages

### Parlotype.Core (class library — no external deps)
- No NuGet packages (pure domain interfaces and models)

### Parlotype.Platform
- `Whisper.net` 1.9.0 — Speech recognition
- `Whisper.net.Runtime` 1.9.0 — CPU fallback runtime
- `NAudio` — Windows audio capture (WASAPI)
- `SharpHook` — Global keyboard hooks
- `Microsoft.Extensions.DependencyInjection.Abstractions` — For DI extension methods

### Parlotype.Desktop
- `Avalonia` 11.3.11
- `Avalonia.Desktop` 11.3.11
- `Avalonia.Themes.Fluent` 11.3.11
- `Avalonia.Fonts.Inter` 11.3.11
- `Avalonia.Diagnostics` 11.3.11 (Debug only)
- `CommunityToolkit.Mvvm` — Source-generated MVVM
- `Microsoft.Extensions.DependencyInjection` — DI container

### Parlotype.Tests
- `xunit`
- `xunit.runner.visualstudio`
- `Microsoft.NET.Test.Sdk`

## Directory.Build.props

Centralizes shared settings for all projects:
- `TargetFramework`: `net10.0`
- `Nullable`: `enable`
- `ImplicitUsings`: `enable`
- `TreatWarningsAsErrors`: `true`

## Implementation Steps

### Step 1: Create solution structure
- Create `Directory.Build.props` at solution root
- Create `src/` directory and all four `.csproj` files
- Create `Parlotype.sln` and add all projects
- Set up project references: Desktop → Platform → Core; Tests → Core, Platform

### Step 2: Set up Parlotype.Core
- Create interface files: `ISpeechRecognizer`, `IAudioCaptureService`, `ITextProcessor`, `IGlobalHotkeyService`
- Create model files: `TranscriptionResult`, `AudioFormat`
- These are intentionally minimal — just enough to establish the contract boundaries

### Step 3: Set up Parlotype.Platform
- Create stub implementations for the Core interfaces (throw `NotImplementedException` for now)
- Create `PlatformServiceExtensions.cs` for DI registration
- Add NuGet package references

### Step 4: Set up Parlotype.Desktop (Avalonia app)
- `Program.cs` — Standard Avalonia entry point with `BuildAvaloniaApp()`
- `App.axaml` — Fluent theme, DI container setup
- `MainWindow.axaml` — Simple window with app name and "Ready" status text
- `MainWindowViewModel.cs` — Basic ViewModel with status property
- `ViewModelBase.cs` — Base class extending `ObservableObject`

### Step 5: Set up Parlotype.Tests
- Create xUnit project with references to Core and Platform
- Add a single sample test to verify the test pipeline works

### Step 6: Create .gitignore, README.md, and AGENTS.md
- `.gitignore` — Standard .NET gitignore (bin/, obj/, *.user, .vs/, etc.) + Whisper model files (*.bin), Avalonia designer cache
- `README.md` — Project name, tagline, tech stack summary, prerequisites (.NET 10 SDK), how to build/run, project structure overview, license placeholder
- `AGENTS.md` — Instructions for AI agents working on this codebase: architecture overview, project relationships, coding conventions (nullable enabled, warnings as errors), how to build/test, key patterns (MVVM with CommunityToolkit, DI via Microsoft.Extensions, interfaces in Core / implementations in Platform)

### Step 7: Initialize git repo and verify
- `git init`
- Build the solution (`dotnet build`)
- Run tests (`dotnet test`)
- Run the app to verify the Avalonia window appears (`dotnet run --project src/Parlotype.Desktop`)

## Verification

1. `dotnet build Parlotype.sln` — should compile with zero warnings (TreatWarningsAsErrors)
2. `dotnet test` — sample test should pass
3. `dotnet run --project src/Parlotype.Desktop` — Avalonia window with "Parlotype" title should appear
4. Confirm project references work (Desktop depends on Platform depends on Core)

## Files to Create (complete list)

1. `D:\projects\parlotype\Directory.Build.props`
2. `D:\projects\parlotype\.gitignore`
3. `D:\projects\parlotype\src\Parlotype.Core\Parlotype.Core.csproj`
4. `D:\projects\parlotype\src\Parlotype.Core\Audio\IAudioCaptureService.cs`
5. `D:\projects\parlotype\src\Parlotype.Core\Audio\AudioFormat.cs`
6. `D:\projects\parlotype\src\Parlotype.Core\Speech\ISpeechRecognizer.cs`
7. `D:\projects\parlotype\src\Parlotype.Core\Speech\TranscriptionResult.cs`
8. `D:\projects\parlotype\src\Parlotype.Core\TextProcessing\ITextProcessor.cs`
9. `D:\projects\parlotype\src\Parlotype.Core\Hotkeys\IGlobalHotkeyService.cs`
10. `D:\projects\parlotype\src\Parlotype.Platform\Parlotype.Platform.csproj`
11. `D:\projects\parlotype\src\Parlotype.Platform\Audio\WasapiAudioCaptureService.cs`
12. `D:\projects\parlotype\src\Parlotype.Platform\Speech\WhisperSpeechRecognizer.cs`
13. `D:\projects\parlotype\src\Parlotype.Platform\Hotkeys\SharpHookHotkeyService.cs`
14. `D:\projects\parlotype\src\Parlotype.Platform\PlatformServiceExtensions.cs`
15. `D:\projects\parlotype\src\Parlotype.Desktop\Parlotype.Desktop.csproj`
16. `D:\projects\parlotype\src\Parlotype.Desktop\Program.cs`
17. `D:\projects\parlotype\src\Parlotype.Desktop\App.axaml`
18. `D:\projects\parlotype\src\Parlotype.Desktop\App.axaml.cs`
19. `D:\projects\parlotype\src\Parlotype.Desktop\ViewModels\ViewModelBase.cs`
20. `D:\projects\parlotype\src\Parlotype.Desktop\ViewModels\MainWindowViewModel.cs`
21. `D:\projects\parlotype\src\Parlotype.Desktop\Views\MainWindow.axaml`
22. `D:\projects\parlotype\src\Parlotype.Desktop\Views\MainWindow.axaml.cs`
23. `D:\projects\parlotype\src\Parlotype.Tests\Parlotype.Tests.csproj`
24. `D:\projects\parlotype\src\Parlotype.Tests\SampleTest.cs`
25. `D:\projects\parlotype\README.md`
26. `D:\projects\parlotype\AGENTS.md`
27. `D:\projects\parlotype\Parlotype.sln` (generated via `dotnet sln`)
