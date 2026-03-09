---
status: accepted
date: 2026-02-19
---

# 005. ZLogger Structured Logging

## Context

No logging existed in the application. Debugging audio pipeline issues, transcription failures, device changes, and settings problems required adding structured logging throughout the Platform services and Desktop ViewModels.

## Decision

Use **ZLogger v2.5.10** for structured logging via `Microsoft.Extensions.Logging`.

- `Microsoft.Extensions.Logging.Abstractions` added to Core (zero external deps maintained)
- ZLogger configured in App.axaml.cs with two sinks:
  - **Console**: Colored plain-text for development. Format: `{timestamp} [{level}] {message} ({category})`
  - **Rolling file**: Daily rotation + 10MB size limit at `%LOCALAPPDATA%/parlotype/logs/parlotype-{date}_{seq}.log`
- `ILogger<T>` injected via constructor into all Platform services and key ViewModels
- NullLogger<T> fallback pattern for design-time and test constructors

Alternatives considered:

- **Serilog**: More popular, but heavier dependency tree. ZLogger is more .NET-native and uses source generators for zero-alloc logging.
- **NLog**: XML configuration feels dated. ZLogger's C# fluent API is cleaner.
- **Console.WriteLine**: No structured data, no levels, no file output.

## Consequences

- Easier: All Platform services and ViewModels have contextual logging. Debug-level logs show exact pipeline flow (device open, VAD segments, transcription results, settings reads).
- Easier: Rolling file logs persist across sessions for post-mortem debugging.
- Harder: Every service constructor now requires ILogger<T>, adding ceremony to test setups (mitigated by NullLogger pattern).
