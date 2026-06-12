---
title: Key Subsystems
type: architecture
status: active
tags: [architecture, subsystems, hotkeys, settings, logging]
last_updated: 2026-05-31
summary: Text injection, global hotkeys, settings, logging, and model management subsystems
---

# Key Subsystems

## Text Injection

Two implementations of `ITextInjectionService`:

| Implementation | Mechanism | Default? |
|---------------|-----------|----------|
| `ClipboardTextInjectionService` | Save clipboard → set text → Ctrl+V → restore | Yes |
| `SharpHookTextInjectionService` | Direct key simulation via SharpHook | No |

`Win32TargetWindowTracker` tracks the last non-Parlotype foreground window to know where to inject text.

## Global Hotkeys

- **Core**: `IGlobalHotkeyService`, `HotkeyBinding` record (modifiers + key name string)
- **Platform**: `SharpHookHotkeyService` using `SimpleGlobalHook` (required for event suppression; ADR-020)
- **Mapping**: `KeyCodeMapper` converts Core key names → SharpHook `KeyCode`
- **Modes**: Push-to-Talk (key-down → start, key-up → stop) and Toggle
- **Suppression**: `SuppressEvent` prevents hotkey passthrough (Windows/macOS only; requires `SimpleGlobalHook` — `TaskPoolGlobalHook` silently ignores it)
- **Conflict detection**: `HotkeyConflictDetector` warns on reserved OS shortcuts
- **UI**: `HotkeyRecorderView` captures key combos in settings flyout
- **Persistence**: `JsonSettingsService` stores `HotkeyModifiers`, `HotkeyKey`, `ActivationMode`

## Settings

- `ISettingsService` (Core) → `JsonSettingsService` (Platform)
- Persists to `%LOCALAPPDATA%/parlotype/settings.json`
- Thread-safe via `SemaphoreSlim`

## Language & Translation

Single source of truth for "what language(s) the user wants" — see [[decisions/_index|ADR-034]] (data model), [[decisions/_index|ADR-035]] (first UX, superseded), [[decisions/_index|ADR-036]] (current UX: keyboard source, target forms, shared relationship model).

- **Catalog** (Core): `LanguageCatalog` exposes `WhisperLanguages` (curated ~99-language set) and `AllLanguages` (CultureInfo-derived fallback). Sentinels `AutoDetectCode = "auto"` / `NoTranslationCode = "none"` / `KeyboardLayoutCode = "keyboard"` (ADR-036; blank means auto, never keyboard).
- **Capabilities** (Core): `LanguageCapabilities` per engine via `SpeechEngineCapabilities.For`, with derived `TranslationForm` — `Full` (arbitrary targets ⇒ Gemma), `Toggle` (one fixed target ⇒ Whisper/English), `None` (cannot translate ⇒ future transcribe-only engines; capability path built, no engine triggers it yet).
- **Keyboard layout** (Core→Platform): `IKeyboardLayoutService.Detect()` → `KeyboardLayoutInfo(code, friendlyName)`; `Win32KeyboardLayoutService` reads the foreground-window thread's HKL (layouts are per-thread on Windows), `NoOpKeyboardLayoutService` elsewhere. `SourceLanguageResolver` (pure) maps the keyboard sentinel to the detected code with auto fallback + optional supported-list validation.
- **Settings keys** (Core): `SelectedSourceLanguage` (may hold `"keyboard"`), `SelectedTargetLanguage`, `TranslationEnabled` (master toggle), per-role MRU `RecentSourceLanguages` / `RecentTargetLanguages` (sentinels never enter MRU). Legacy `TranslateToEnglish` and shared `RecentLanguages` are migrated once via `LanguageSettingsMigrator` and then ignored.
- **Migration** (Core): `LanguageSettingsMigrator.MigrateAsync(settings)` is idempotent and invoked from `AudioPipelineService.CacheSettingsAsync`, `LlamaCppSpeechRecognizer.BuildPromptTextAsync`, and `LanguageRelationshipViewModel.InitializeAsync` so any read of the new keys triggers migration on first run.
- **Pipeline wiring** (Platform): `AudioPipelineService` resolves the keyboard sentinel (validated against `WhisperLanguages`) into `WhisperOptions.Language` and derives `TranslateToEnglish = TranslationEnabled && target == "en" && model.SupportsTranslation` (ADR-033 gate). `LlamaCppSpeechRecognizer` resolves the sentinel for the `{language}` prompt token and gates its in-prompt translation instruction on `TranslationEnabled`.
- **Shared relationship VM** (Desktop): `LanguageRelationshipViewModel` (DI singleton) owns the spec-§7 state machine — source/target/translation state, resting-target restore, MRU, persistence, derived connector (`On`/`Off`/`Locked` + `→`/`=` glyph), summary sentence, and spec-§8 engine-switch fallbacks with auto-clearing toasts (source → keyboard; None → off; Toggle → forced single target, silent when off; Full → reset unknown target). Both UI surfaces delegate to it.
- **Settings page** (Desktop): `LanguageSelectionSettingsViewModel`/`View` — three-column source | connector pill | target layout; target side morphs by `TranslationForm` (ToggleSwitch / picker field / disabled card + amber note); floating popover pickers (`LanguagePickerView` content + `Border.popoverChrome`) with keyboard/auto/off specials, Recent/All groups, >8 search rule; summary line; ADR-033 paused note; toast region.
- **Transcribe quick picker** (Desktop): strip under the record button (`source chip · connector · target chip` — target mirrors source while off); connector flips translation in one click; chips open a 268px flyout above the widget with the target control + a read-only "You speak" row routing to Settings. `TranscribeViewModel` stops recording on `RelationshipChanged`.

## Logging

- ZLogger to console + rolling file
- Log directory: `%LOCALAPPDATA%/parlotype/logs/`

## Model Management

Pipeline: `IModelDownloadService` (Core) → `HttpModelDownloadService` (Platform) → `ModelDownloadDialogService` (Desktop)

- `WhisperModelType` enum (Core) maps to `GgmlType` (Platform) via `WhisperModelTypeExtensions`
- `WhisperModelInfo` holds static metadata (display name, disk size, SHA hash)
- Model choice persisted via `SettingsKeys.SelectedWhisperModel`
- Tests use `HeadlessModelDownloadService` (downloads without UI)

## NVIDIA/CUDA Environment Detection

First-party detection independent of Whisper.net — see [[decisions/_index|ADR-014]]. Provides startup diagnostics for why CUDA was/wasn't selected and a data source for a future diagnostics UI.

- **Core**: `INvidiaEnvironmentProvider`, `NvidiaEnvironmentInfo`, `CudaRuntimeProbe` in `Parlotype.Core/Speech/`
- **Platform (Windows)**: `WindowsNvidiaEnvironmentProvider` combines three failure-isolated sources:
  1. `nvidia-smi` parsing → driver version + driver max CUDA version
  2. Filesystem scan of `%ProgramFiles%\NVIDIA GPU Computing Toolkit\CUDA\v*` → installed toolkits
  3. `cudart` P/Invoke probe via `NativeLibrary.TryLoad` + `cudaRuntimeGetVersion` / `cudaDriverGetVersion` → loadable runtimes with versions
- **Platform (other OS)**: `NoOpNvidiaEnvironmentProvider` returns `NvidiaEnvironmentInfo.Empty`
- **DI**: selection in `PlatformServiceExtensions` via `OperatingSystem.IsWindows()`
- **Caching**: first call detects, result cached with `SemaphoreSlim`; `RefreshAsync` clears cache and re-runs
- **Startup hook**: `App.axaml.cs` fires `Task.Run` after `BuildServiceProvider`, logs Information line summarising driver/toolkits/runtimes

## Vulkan Environment Detection

First-party detection independent of Whisper.net — see [[decisions/_index|ADR-022]]. Used by `WhisperSpeechRecognizer` to gate strict `RuntimePreference.Vulkan`, by `RuntimeSettingsViewModel` to dim unavailable runtime options, and by `App.axaml.cs` for a startup diagnostic log.

- **Core**: `IVulkanEnvironmentProvider`, `VulkanEnvironmentInfo`, `VulkanDeviceInfo`, `VulkanDeviceType` in `Parlotype.Core/Speech/`
- **Platform (Windows)**: `WindowsVulkanEnvironmentProvider` probes:
  1. `vulkan-1` loader presence via `NativeLibrary.TryLoad`
  2. Loader API version via `vkEnumerateInstanceVersion` (assumes 1.0 when symbol missing)
  3. Physical devices via `vkCreateInstance` + `vkEnumeratePhysicalDevices` + `vkGetPhysicalDeviceProperties` (P/Invoke; failures are absorbed)
  4. `VULKAN_SDK` env var → SDK install presence
- **Platform (other OS)**: `NoOpVulkanEnvironmentProvider` returns `VulkanEnvironmentInfo.Empty`
- **DI**: selection in `PlatformServiceExtensions` via `OperatingSystem.IsWindows()`
- **Caching**: first call detects, result cached with `SemaphoreSlim`; `RefreshAsync` clears cache and re-runs
- **Startup hook**: `App.axaml.cs` `LogVulkanEnvironmentAsync` parallels the NVIDIA log

## Whisper Runtime Selection

User-facing `RuntimePreference` (`Parlotype.Core/Speech/`) maps to Whisper.net's `RuntimeOptions.RuntimeLibraryOrder` — see [[decisions/_index|ADR-012]] (CUDA) and [[decisions/_index|ADR-022]] (Vulkan).

| Preference | Whisper.net order | Fallback? |
|------------|-------------------|-----------|
| `Auto` (default) | `[Cuda, Vulkan, Cpu]` | yes — silent chained |
| `Cuda` | `[Cuda]` | **no** — strict |
| `Vulkan` | `[Vulkan]` | **no** — strict |
| `Cpu` | `[Cpu]` | n/a |

- **Bootstrap**: `WhisperRuntimeBootstrap.Initialize(RuntimePreference, ILogger)` sets `RuntimeOptions.RuntimeLibraryOrder` once per process (first-call-wins).
- **Strict-mode guard**: `WhisperSpeechRecognizer` calls `INvidiaEnvironmentProvider`/`IVulkanEnvironmentProvider` before factory creation. On a strict mismatch it throws `RuntimeUnavailableException` (Core) instead of silently falling back to CPU. `TranscribeViewModel` catches it and shows a status-bar message directing the user to Settings.
- **UI**: `RuntimeSettingsViewModel` + `RuntimeSettingsView` (Settings → Runtime). Persists via `SettingsKeys.RuntimePreference`. Shows "Changes take effect after restart" because runtime selection is process-global one-shot.
- **Packages**: `Whisper.net.Runtime.Cuda` (conditional on `EnableCuda` MSBuild prop, default true) and `Whisper.net.Runtime.Vulkan` (always included).

## Audio Level & Waveform Visualisation

Real-time visual feedback showing whether the user is speaking. See [[decisions/_index|ADR-023]].

- **Core**: `IAudioLevelProvider` (event `LevelChanged`, property `CurrentLevel`), `AudioLevelEventArgs`, `RecordingState` enum (`Disabled`, `Idle`, `Active`)
- **Platform**: `AudioPipelineService` implements `IAudioLevelProvider` — computes RMS on each audio chunk via `PublishAudioLevel()`, fires event. DI forwards via `sp.GetRequiredService<IAudioPipeline>()` cast.
- **Desktop**: `WaveformView` custom `Control` with `DrawingContext` rendering:
  - **Disabled**: microphone icon via `StreamGeometry`
  - **Idle**: 13 white bars with gentle sine-wave breathing animation
  - **Active**: 13 white bars with decorative multi-frequency wave animation (amplitude 0.6 default)
  - 60 fps `DispatcherTimer`, attached/detached with visual tree
- **State machine** in `TranscribeViewModel`:
  - EMA-smoothed RMS (attack 0.4, decay 0.05) compared against threshold 0.005
  - 1200ms hold-off keeps Active state through natural speech pauses
  - Button turns blue `#378ADD` when recording (Idle or Active)
