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

### GPU / CUDA Builds

The `Whisper.net.Runtime.Cuda` NuGet package (~350 MB) is included by default. To build without CUDA (faster CI, smaller output):

```bash
dotnet build Parlotype.slnx -p:EnableCuda=false   # CPU-only build
dotnet test -p:EnableCuda=false                     # CPU-only tests
```

CUDA is auto-detected at runtime via `RuntimeOptions.RuntimeLibraryOrder`. If no NVIDIA GPU is available, Whisper.net falls back to CPU silently.

**Note:** File lock errors from `.NET Host` processes are common on Windows. Kill the locking process by PID before rebuilding.

### Benchmark CLI

```bash
# Run a benchmark
dotnet run --project src/Parlotype.Benchmark -- run \
  --config datasets/smoke-test-config.json \
  --datasets datasets --output results

# Run with tag/sample filtering
dotnet run --project src/Parlotype.Benchmark -- run \
  --config datasets/smoke-test-config.json \
  --datasets datasets --output results \
  --tags clean,short --samples kennedy

# List historical runs
dotnet run --project src/Parlotype.Benchmark -- list --output results

# Compare two runs
dotnet run --project src/Parlotype.Benchmark -- compare \
  --run-a <run-id-a> --run-b <run-id-b> --output results

# Export a run (csv, markdown, json)
dotnet run --project src/Parlotype.Benchmark -- export \
  --run-id <run-id> --format markdown --output results

# Rebuild SQLite index from JSON files
dotnet run --project src/Parlotype.Benchmark -- import --output results

# Run a parameter sweep (Cartesian product of config axes)
dotnet run --project src/Parlotype.Benchmark -- sweep \
  --config datasets/sweep-config.json \
  --datasets datasets --output results

# Check for regressions against a baseline (CI integration)
dotnet run --project src/Parlotype.Benchmark -- check \
  --baseline <run-id> --current latest \
  --output results --max-wer-delta 2.0
```

## Architecture

**Solution:** `Parlotype.slnx` (modern .slnx format) with 7 projects.

**Dependency direction:** `Desktop → Platform → Core` and `Benchmark → Platform → Core`. Tests → Core, Platform. Desktop.Tests → Desktop, Core. Benchmark.Tests → Benchmark, Core.

| Project | Purpose |
|---------|---------|
| **Parlotype.Core** | Domain interfaces and models. Zero external dependencies. All contracts live here. Subfolders: `Audio/`, `Hotkeys/`, `Settings/`, `Speech/`, `TextInjection/` |
| **Parlotype.Platform** | Implements Core interfaces (Whisper.net, NAudio, SileroVad, SharpHook). Subfolders mirror Core: `Audio/`, `Hotkeys/`, `Settings/`, `Speech/`. Register new services in `PlatformServiceExtensions.cs` |
| **Parlotype.Desktop** | Avalonia UI app (11.3.0, Fluent theme). Entry point. Wires DI, hosts views/viewmodels |
| **Parlotype.Benchmark** | Console CLI for evaluating transcription quality (WER/CER/RTF). System.CommandLine + Spectre.Console + SQLite. Includes SQLite index for historical run queries, comparison engine with delta metrics, parameter sweep support, repetition-based stability analysis, per-sample memory/GC tracking, CI regression detection, and export to CSV/Markdown/JSON. CLI commands: `run`, `import`, `list`, `compare`, `export`, `check`, `sweep`. Subfolders: `Configuration/`, `Metrics/`, `Pipeline/`, `Results/`, `Reporting/` |
| **Parlotype.Tests** | xUnit tests for Core and Platform (audio pipeline, VAD, Whisper) |
| **Parlotype.Desktop.Tests** | Avalonia headless UI tests using `Avalonia.Headless.XUnit`. Uses `[AvaloniaFact]` instead of `[Fact]`. Mock services in `Mocks/` folder |
| **Parlotype.Benchmark.Tests** | xUnit tests for benchmark metrics (WER/CER calculation, text normalization, config deserialization), comparison engine, CSV/Markdown formatters, SQLite index, sweep expansion, repetition stats, memory metrics, and regression checks |

### Audio Pipeline Data Flow

```
WASAPI Capture → 16kHz Mono Float → Silero VAD → Speech Segments → Whisper Transcription → Text Injection
```

- **Batch mode** (default): buffers audio, detects end-of-speech via silence
- **Streaming mode**: processes fixed 3-second windows
- Capture and transcription run on separate threads; `ConcurrentQueue<float[]>` bridges them

### Key Subsystems

- **Text Injection:** `ClipboardTextInjectionService` (default, saves/restores clipboard around Ctrl+V) or `SharpHookTextInjectionService` (direct key simulation). `Win32TargetWindowTracker` tracks the last non-Parlotype foreground window.
- **Global Hotkeys:** `IGlobalHotkeyService` (Core) → `SharpHookHotkeyService` (Platform, `TaskPoolGlobalHook` for non-blocking keyboard event dispatch). `HotkeyBinding` record (modifiers + key name string) in Core, mapped to SharpHook `KeyCode` via `KeyCodeMapper` in Platform. Supports Push-to-Talk (key-down → start, key-up → stop) and Toggle modes. Event suppression via `SuppressEvent` (Windows/macOS only). Config persisted via `JsonSettingsService` (`HotkeyModifiers`, `HotkeyKey`, `ActivationMode` keys). `HotkeyConflictDetector` warns on reserved OS shortcuts. `HotkeyRecorderView` captures key combos in the settings flyout.
- **Settings:** `JsonSettingsService` persists to `%LOCALAPPDATA%/parlotype/settings.json`. Thread-safe via `SemaphoreSlim`.
- **Logging:** ZLogger to console + rolling file in `%LOCALAPPDATA%/parlotype/logs/`.
- **Model Management:** `IModelDownloadService` (Core) → `HttpModelDownloadService` (Platform, HTTP with progress) → `ModelDownloadDialogService` (Desktop, modal confirmation dialog + progress bar). Tests use a `HeadlessModelDownloadService` that downloads without UI.

## Coding Conventions

- **.NET 10** target framework (`net10.0`), nullable reference types enabled, implicit usings enabled, **warnings as errors** (`TreatWarningsAsErrors=true` in `Directory.Build.props`)
- **MVVM:** `CommunityToolkit.Mvvm` with source generators — use `[ObservableProperty]` on private fields and `[RelayCommand]` on methods. ViewModels must be `partial` classes.
- **AXAML:** Always use `x:CompileBindings="True"` and `x:DataType`. Never use `{ReflectionBinding}`. Avalonia uses `.axaml`, not `.xaml`.
- **Design-time data:** Use `<Design.DataContext>` with parameterless ViewModel constructors backed by design stubs.
- **Conditional CSS classes:** Use `Classes.xxx="{Binding Property}"` with `<Window.Styles>` for visual state changes (e.g. `Classes.recording="{Binding IsRecording}"` on the microphone button).
- **Flyout bindings:** Flyouts are disconnected from the visual tree — embed commands directly in display item wrappers (e.g. `MicrophoneDisplayItem`, `WaitTimeDisplayItem`, `WhisperModelDisplayItem`) instead of using `$parent` traversal bindings.
- **Flyout lifecycle:** Avalonia flyouts lack MVVM-friendly lifecycle bindings — use code-behind to hook `PopupFlyoutBase.Opening` for refreshing ViewModel data when flyouts open (see `SettingsFlyoutView.axaml.cs`).
- **Interfaces in Core, implementations in Platform** — never add platform-specific packages to Core.
- **DI registration:** Add new services in `PlatformServiceExtensions.cs` using `Microsoft.Extensions.DependencyInjection`. All services are singletons.
- **Background → UI thread:** `ObservableCollection` mutations from background threads must dispatch to `Avalonia.Threading.Dispatcher.UIThread`.
- **Benchmark output:** Use Spectre.Console, not `Console.WriteLine`.
- **Whisper model selection:** `WhisperModelType` enum in Core maps to `GgmlType` in Platform via `WhisperModelTypeExtensions`. `WhisperModelInfo` holds static metadata (display name, disk size, SHA). Model choice is persisted via `SettingsKeys.SelectedWhisperModel` and read by `WhisperSpeechRecognizer` at initialization.
- **Whisper parameters:** `WhisperOptions` record in Core configures model, language, beam size, temperature, initial prompt, and CPU thread count. `ISpeechRecognizer.InitializeAsync(WhisperOptions)` overload applies these; the no-args overload reads from settings (desktop default). `WhisperSpeechRecognizer` uses greedy decoding for beam size 1, beam search for larger values, and `WithThreads()` when thread count is specified.
- **Whisper model lifecycle:** Never load multiple Whisper models simultaneously. Sequential load→unload→load is supported via `ISpeechRecognizer.UnloadAsync()` (see ADR-017).

## Key Patterns

- New domain contracts → add interface to `Parlotype.Core` in the appropriate subfolder
- New platform implementations → add to `Parlotype.Platform` and register in `PlatformServiceExtensions.cs`
- New UI features → add ViewModels to `Parlotype.Desktop/ViewModels/` and Views to `Parlotype.Desktop/Views/`
- Extract reusable UI components into separate UserControls (e.g. `MicrophoneSettingsView`)
- Always write tests for logic in Core and Platform
- Write benchmark metrics tests in `Parlotype.Benchmark.Tests` for WER/CER calculators, text normalization, comparison engine, formatters, SQLite index, sweep expansion, and regression checks
- Benchmark results are auto-indexed into SQLite (`benchmarks.db`) after each run for historical queries
- Parameter sweeps use `SweepConfig` with dot-notation axes (e.g., `whisper.model`, `whisper.beamSize`) — `SweepExpander` generates Cartesian product of configs
- Repetitions: set `repetitions > 1` in config to run samples N times and compute mean/stddev WER/CER for stability analysis
- CI regression detection: `check` command compares against a baseline with configurable thresholds, returns exit code 0 (pass) or 1 (fail)
- Write headless UI tests in `Parlotype.Desktop.Tests` for view/viewmodel integration — use `MockMicrophoneEnumerator` and `MockSettingsService` for controllable testing

## Memory Vault

The `memory/` directory is an Obsidian vault serving as the persistent cognitive substrate for AI agents. It uses three-tier progressive disclosure:

1. **Tier 1 (always loaded):** `memory/AGENTS.md` — lightweight router with navigation pointers
2. **Tier 2 (on demand):** `memory/*/_index.md` — summary tables per directory
3. **Tier 3 (when relevant):** Full documents — service profiles, architecture, conventions

### Vault Structure
- `memory/architecture/` — audio pipeline, dependency graph, subsystem docs
- `memory/services/` — profiles for all 7 projects
- `memory/conventions/` — .NET standards, Avalonia patterns, testing strategy
- `memory/decisions/` — index linking to ADRs in `docs/decisions/`
- `memory/sessions/` — session handoff notes (episodic memory)
- `memory/knowledge/` — stable facts learned across sessions (semantic memory)
- `memory/skills/` — agent skills (obsidian-markdown, debug-pipeline, implement-feature, session-management)

### Session Lifecycle

Apply this protocol every session. Full detail lives in
`.claude/skills/session-management/SKILL.md` (canonical: `memory/skills/session-management.md`).

**Start of session:**
1. Read `memory/AGENTS.md` for orientation
2. Read the latest note in `memory/sessions/` and pick up from its **Next Action**
3. Read the relevant `_index.md` for the area of work, then drill into specific documents as needed
4. Skim `memory/knowledge/_index.md` for recently learned facts

**End of session:**
1. Create `memory/sessions/YYYY-MM-DD-HHMM-<slug>.md` from `memory/sessions/_template.md`
   (e.g. `2026-04-30-0916-skills-scaffolding.md`; `HHMM` = start time 24h, `<slug>` = short kebab-case topic)
2. Fill in: Active Focus, Decisions Made, Facts Learned, Open Blockers, **Next Action**

**Knowledge distillation:**
- Stable, non-derivable facts → `memory/knowledge/<topic>.md` + index row
- Updates to existing knowledge → edit the relevant file
- Ephemeral facts → leave only in the session note

### Maintenance
- `bash memory/scripts/generate-index.sh` — vault stats, orphan detection
- `bash memory/scripts/check-staleness.sh [days]` — flag stale notes (default: 90 days)

## Plans & Decisions

See [plans/WORKFLOW.md](plans/WORKFLOW.md) for task tracking workflows, plan format, and ADR templates. Plans live in `plans/` (flat structure, status via YAML frontmatter). ADRs live in `docs/decisions/`.

## Definition of Done

A non-trivial change is **not done** until all of the following hold:

1. **Build & tests** — `dotnet build Parlotype.slnx` is clean (zero warnings) and `dotnet test` passes.
2. **Behaviour verified** — the original symptom is reproduced and shown gone, or the new feature is exercised end-to-end (manual run, log line, test, etc.).
3. **ADR exists** if any of the following triggers fire:
   - A new interface, record, or enum is added to `Parlotype.Core`
   - A new entry is added to `PlatformServiceExtensions.cs`
   - A new dependency is added to any `.csproj`
   - Behaviour intentionally diverges by OS, build flag, or runtime
   - A new external process, native library, or P/Invoke call is introduced
   - The change touches the audio pipeline, hotkey, settings, or Whisper subsystems
4. **Memory vault updated** when the change adds or renames public symbols, services, or subsystems:
   - The relevant `memory/services/<project>.md` lists the new symbol(s)
   - `memory/decisions/_index.md` references any new ADR
   - For cross-cutting subsystems, `memory/architecture/subsystems.md` gains or updates a section
5. **Knowledge captured** — facts learned along the way that are *not derivable from current code* (e.g. third-party quirks, environment gotchas) are recorded under `memory/knowledge/` with an index row.
6. **Ask before scope-pruning** — if you choose to defer (3), (4), or (5), surface the choice via `ask_user` rather than silently shipping. The user may reasonably want docs in a follow-up commit, but the agent should not decide that unilaterally.
