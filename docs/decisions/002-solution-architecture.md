---
status: accepted
date: 2026-02-12
---

# 002. Solution Architecture

## Context

Parlotype is a local-first, privacy-focused voice-to-text desktop app. The project needed a modular architecture supporting speech recognition (Whisper.net), audio capture (NAudio/WASAPI), and a cross-platform UI (Avalonia), while keeping domain logic testable and platform-independent.

## Decision

Four-project layered architecture with strict dependency direction: Desktop → Platform → Core, Tests → Core + Platform.

- **Parlotype.Core**: Pure domain interfaces and models. Zero external NuGet dependencies. All contracts (IAudioCaptureService, ISpeechRecognizer, IGlobalHotkeyService, ITextInjectionService, ISettingsService) live here.
- **Parlotype.Platform**: Implements Core interfaces using platform-specific libraries (Whisper.net, NAudio, SileroVad, SharpHook). Registered via PlatformServiceExtensions.cs.
- **Parlotype.Desktop**: Avalonia UI app (11.3.0, Fluent theme). Entry point. Wires DI, hosts views/viewmodels.
- **Parlotype.Tests**: xUnit tests for Core and Platform.

Key technology choices:

- **.NET 10** with nullable reference types, implicit usings, TreatWarningsAsErrors (Directory.Build.props)
- **CommunityToolkit.Mvvm** over ReactiveUI — lighter, source-generated, no RxUI learning curve
- **Microsoft.Extensions.DependencyInjection** — standard .NET DI, familiar
- **Avalonia UI 11.3.0** with Fluent theme for cross-platform desktop

## Consequences

- Easier: Core is testable without platform dependencies. New platform implementations can be swapped in (e.g., macOS audio). DI registration is centralized.
- Easier: Adding new features follows a clear pattern: interface in Core → implementation in Platform → registration in PlatformServiceExtensions → UI in Desktop.
- Harder: Requires discipline to keep Core dependency-free. Some abstractions (like key codes for hotkeys) need platform-agnostic representations.
