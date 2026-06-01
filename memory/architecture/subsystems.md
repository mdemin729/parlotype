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

Single source of truth for "what language(s) the user wants" — see [[decisions/_index|ADR-034]] (initial data model) and [[decisions/_index|ADR-035]] (UX redesign).

- **Catalog** (Core): `LanguageCatalog` exposes `WhisperLanguages` (curated ~99-language set) and `AllLanguages` (CultureInfo-derived fallback). Sentinels `AutoDetectCode = "auto"` / `NoTranslationCode = "none"`.
- **Capabilities** (Core): `LanguageCapabilities` per engine via `SpeechEngineCapabilities.For`. Whisper publishes `FixedTranslationTargets = [English]` (ADR-035) so the unified picker renders it like any other target; Gemma 4 sets `SupportsArbitraryTranslation = true` for the full catalog.
- **Settings keys** (Core): `SelectedSourceLanguage`, `SelectedTargetLanguage`, `TranslationEnabled` (master toggle), per-role MRU `RecentSourceLanguages` / `RecentTargetLanguages`. Legacy `TranslateToEnglish` and shared `RecentLanguages` are migrated once via `LanguageSettingsMigrator` and then ignored.
- **MRU helper** (Core): `RecentLanguages.Add(existing, code, max = 5)` — pure push-to-front / dedupe / cap. Caller owns the storage key.
- **Migration** (Core): `LanguageSettingsMigrator.MigrateAsync(settings)` is idempotent and invoked from `AudioPipelineService.CacheSettingsAsync`, `LlamaCppSpeechRecognizer.BuildPromptTextAsync`, and `LanguageSelectionSettingsViewModel.InitializeAsync` so any read of the new keys triggers migration on first run.
- **Pipeline wiring** (Platform): `AudioPipelineService` derives `WhisperOptions.TranslateToEnglish = TranslationEnabled && target == "en" && model.SupportsTranslation` (the ADR-033 capability gate still applies). `LlamaCppSpeechRecognizer` gates its in-prompt translation instruction on `TranslationEnabled`.
- **UI** (Desktop): `LanguageSelectionSettingsViewModel`/`View` owns the section; arrow between source & target buttons is `ToggleTranslationCommand`; two `LanguagePickerViewModel`/`View` instances render the inline pickers via callback-bound `getSupported`/`getRecents`/`getSelectedCode`/`onSelect`/`getLeadingSentinel`. "Translation paused" hint appears when the user has translation on but the active Whisper model can't translate (ADR-033 + ADR-035).

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
