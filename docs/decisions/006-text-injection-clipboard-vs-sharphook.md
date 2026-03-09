---
status: accepted
date: 2026-02-22
---

# 006. Text Injection — Clipboard vs SharpHook

## Context

After transcription, Parlotype needs to inject the recognized text into the previously active application's text field. This must work across arbitrary target applications (browsers, editors, chat apps) without requiring accessibility APIs or target-app cooperation.

## Decision

Two text injection strategies behind a common `ITextInjectionService` interface, with clipboard as the default.

**Strategy 1 — ClipboardTextInjectionService (default):**

- Saves current clipboard content via Win32 APIs (OpenClipboard, GetClipboardData)
- Sets transcribed text to clipboard
- Simulates Ctrl+V via SharpHook EventSimulator
- Restores original clipboard content
- Reliable across virtually all applications

**Strategy 2 — SharpHookTextInjectionService (opt-in via `--text-injection-mode=sharp-hook` CLI arg):**

- Uses SharpHook's `EventSimulator.SimulateTextEntry()` for direct character-by-character input
- Faster, no clipboard interference
- May not work in all applications (some reject simulated input)

**Supporting infrastructure:**

- `Win32TargetWindowTracker` uses `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` to track the last non-Parlotype foreground window
- `ITargetWindowTracker.ActivateTargetWindow()` restores focus before injection
- `TextInjectionMode` enum in Core; mode selection in App.axaml.cs DI registration

## Consequences

- Easier: Clipboard approach works with 99%+ of Windows applications. Users can switch modes without code changes.
- Easier: ITextInjectionService abstraction allows adding future strategies (e.g., UI Automation).
- Harder: Clipboard approach briefly clobbers clipboard content (restored after paste). Race conditions possible if user clips during injection.
- Harder: SharpHook mode has compatibility issues with some applications that filter simulated input.
