---
status: accepted
date: 2026-02-18
---

# 004. JSON Settings Persistence

## Context

Multiple features need persistent user preferences: selected microphone, theme, Whisper model, hotkey binding. The app needs a simple, human-readable settings store that works on first launch with no setup.

## Decision

JSON file-based settings via `JsonSettingsService` implementing `ISettingsService`.

- Stored at `%LOCALAPPDATA%/parlotype/settings.json`
- Generic key-value API: `GetAsync<T>(key)` / `SetAsync<T>(key, value)` using System.Text.Json
- Thread-safe via `SemaphoreSlim` (async-compatible, unlike lock)
- Directory auto-created on first write
- Graceful degradation: returns default(T) if file missing or corrupt

Alternatives considered:

- **SQLite**: Overkill for ~10 settings. Adds binary dependency. Not human-readable.
- **Windows Registry**: Not cross-platform. Harder to debug.
- **XML/TOML**: Less ecosystem support in .NET than JSON.

## Consequences

- Easier: Human-readable, debuggable by opening the file. No database setup. Works cross-platform.
- Easier: Simple to add new settings — just define a new SettingsKeys constant and call Get/Set.
- Harder: No schema validation. Concurrent multi-process access not handled (single-app scenario). Large settings files would need migration strategy.
