---
title: Key Subsystems
type: architecture
status: active
tags: [architecture, subsystems, hotkeys, settings, logging, startup]
last_updated: 2026-08-02
summary: Speech engines, text injection, global hotkeys, settings, logging, model management, startup, onboarding, and localization subsystems
---

# Key Subsystems

## Speech Engines

Three local engines + two opt-in cloud engines (ADR-032/043) behind one `ISpeechRecognizer` contract; `DelegatingSpeechRecognizer` (registered as the singleton) resolves the concrete recognizer via `SpeechRecognizerFactory` from the `SpeechEngine` setting at `InitializeAsync` time.

| Engine | Recognizer | Runtime | Download | Languages | Translation |
|--------|-----------|---------|----------|-----------|-------------|
| **Parakeet v3 (default, ADR-042)** | `ParakeetSpeechRecognizer` | sherpa-onnx in-process, CPU-only INT8 (ADR-041) | 4 ONNX files (~670 MB), auto-downloaded on first use | 25 European, always auto-detected — no language UI shown | none |
| Whisper | `WhisperSpeechRecognizer` | Whisper.net in-process (CUDA/Vulkan/CPU, ADR-012/022) | per-model GGML (~75 MB–3 GB) | ~99, source selectable | to English (Toggle) |
| Gemma 4 | `LlamaCppSpeechRecognizer` | llama-server sidecar, Vulkan (ADR-025) | GGUF + mmproj (~6–15 GB) | full list (LLM) | arbitrary (Full) |
| OpenAI-compatible (cloud, ADR-043) | `OpenAiCompatibleSpeechRecognizer` | HTTPS multipart batch to `{base}/audio/transcriptions` — OpenAI, Groq, any compatible host | none (BYOK API key in `ISecretStore`) | Whisper set, always auto-detected — no language UI shown | none |
| xAI Grok (cloud, ADR-043) | `XaiGrokSpeechRecognizer` | HTTPS multipart batch to `{base}/stt` (custom xAI schema) | none (BYOK API key in `ISecretStore`) | full list, always auto-detected — no language UI shown | none |

Cloud engines are strictly opt-in (never default), fail initialization with an actionable error when no API key is stored (`CloudProviderNotConfiguredException` → record-start dialog with "Open settings"), and light a persistent "Cloud" badge on the Transcribe window while active (ADR-032 transparency commitment). Utterance audio is WAV-encoded (`WavEncoder`) and POSTed per transcription; text post-processing stays centralized in `AudioPipelineService` for all five engines. Per-request HTTP failures are parsed + classified (`CloudSpeechHttpError` → `CloudSpeechTranscriptionException`/`CloudSpeechErrorKind`) and surfaced to the user via the pipeline's `TranscriptionFailed` event (quota/rate-limit/outage → message dialog, rejected key → "Open settings"), without stopping the recording (ADR-043 amendment).

Engine-scoped settings sections hide via `RestrictToEngine` (ADR-028) or the capability-driven `IsVisibleFor` override (ADR-042 — the Language page hides for engines whose `LanguageCapabilities.HasLanguageChoices` is false, and the Transcribe widget's language strip hides + the window compacts 118→88 px). Model hot-swap for all engines via `UnloadAsync` (ADR-017); prewarm + loading spinner apply engine-agnostically through the delegating recognizer (ADR-038).

## Text Injection

Two implementations of `ITextInjectionService`:

| Implementation | Mechanism | Default? |
|---------------|-----------|----------|
| `ClipboardTextInjectionService` | Save clipboard → set text → Ctrl+V → restore | Yes |
| `SharpHookTextInjectionService` | Direct key simulation via SharpHook | No |

`Win32TargetWindowTracker` tracks the last non-Parlotype foreground window to know where to inject text.

## Global Hotkeys

Users configure a *list* of gestures, not a single chord (ADR-047).

- **Core model**: `DictationHotkey` = `HotkeyGesture` + `ActivationMode`.
  `HotkeyGesture` is one of `Chord` (wraps `HotkeyBinding`), `HoldModifier`, or
  `DoubleTapModifier`, the latter two carrying `ModifierKey` + `ModifierSide`
  (Left/Right/Either). Mode is constrained by kind: holds are push-to-talk only,
  double-taps are toggle only, chords are either.
- **Defaults** (`DictationHotkeyDefaults`): Hold Right Ctrl (PTT), Double-tap
  Ctrl (toggle), Ctrl+Alt+Space (toggle). `Escape` cancels and is hardwired.
- **Recognition**: `HotkeyGestureMatcher` (Core) turns `HotkeyKeyEvent`s into a
  `DictationAction` (Start/Stop/Cancel) + suppression flag, driving
  `ModifierTapTracker` and `ModifierHoldTracker` — pure, timestamp-driven, so
  timing is testable without a keyboard. Thresholds in `HotkeyGestureTiming`.
- **Deferred hold-start**: when a double-tap binding shares a hold binding's key
  (as the defaults do), the hold's start waits out the 250 ms tap window so a
  deliberate double-tap doesn't flicker a recording into existence.
- **Platform**: `SharpHookHotkeyService` is a thin adapter over `SimpleGlobalHook`
  (required for suppression; ADR-020) — builds the event, asks the matcher,
  raises the semantic event. Owns a `Timer` for deferred holds; the matcher is
  lock-guarded (hook thread + timer thread).
- **Mapping**: `KeyCodeMapper` converts key names ↔ SharpHook `KeyCode`, maps
  modifier codes to `ModifierKey` + side, and reads `EventMask` (side-resolvable
  — see [[sharphook-modifier-sides]]).
- **Suppression**: chords on both down and up; **never** bare modifiers (would
  break every Ctrl shortcut); `Escape` only while dictation is active.
- **State feedback**: `IGlobalHotkeyService.SetDictationActive(bool)`, fed by
  `HotkeyCoordinator` from `TranscribeViewModel.RecordingState` — toggle and
  Escape need state the hook cannot observe (widget button, failed model load).
- **Hold aborts** (ADR-057): a keystroke during a hold means the user reached for
  a shortcut, so the recording is discarded. For **Ctrl and Alt** holds that
  applies to any keystroke at any time — nothing typed under a command modifier
  reaches the app as text, so the "users type while dictating" case cannot arise
  there. Shift and Meta keep the 300 ms `HoldAbortGraceMs` window. Scope is holds
  only: `Ctrl+S` during toggle-mode dictation saves the file and keeps recording.
  Suppression is unchanged, so the shortcut itself still reaches the target app.
- **Cancel**: `TranscribeViewModel.CancelRecordingAsync()` detaches
  `TranscriptionAvailable` *before* calling `IAudioPipeline.CancelAsync()`, which
  drops the buffered audio unflushed and cancels the recognizer rather than
  draining it (ADR-057). The detach stays as cover for an utterance that
  completed between the keystroke and the cancel.
- **Validation**: `HotkeyConflictDetector.Check` returns `HotkeyConflict` with
  Blocking (OS-reserved, or duplicate/overlapping with existing bindings) or
  Warning (`Ctrl+Shift+Space` = IDE parameter hints; `Ctrl+Alt+<letter>` = AltGr)
  severity.
- **UI**: `HotkeySettingsView` is a binding list (add via presets or chord
  recorder, remove, per-chord mode); `TranscribeWindow`'s record button tooltip
  shows the current gesture via `HotkeyHint`.
- **Persistence**: `SettingsKeys.HotkeyBindings` — a readable string list encoded
  by `HotkeyBindingCodec` (`hold|Ctrl|Right|PushToTalk`). `HotkeySettingsMigrator`
  converts the legacy `HotkeyModifiers`/`HotkeyKey`/`ActivationMode` triple once.

## Settings

- `ISettingsService` (Core) → `JsonSettingsService` (Platform)
- Persists to `%LOCALAPPDATA%/parlotype-data/settings.json` (resolved via `IAppPaths.SettingsFilePath`, ADR-053)
- Thread-safe via `SemaphoreSlim`
- Secrets are separate: `ISecretStore` (Core) → `DpapiSecretStore` (Platform) → `%LOCALAPPDATA%/parlotype-data/secrets.json`, DPAPI-encrypted per value on Windows, base64 + one-time warning elsewhere; cloud API keys only, never in `settings.json` (ADR-043)

## Language & Translation

Single source of truth for "what language(s) the user wants" — see [[decisions/_index|ADR-034]] (data model), [[decisions/_index|ADR-035]] (first UX, superseded), [[decisions/_index|ADR-036]] (current UX: keyboard source, target forms, shared relationship model).

- **Catalog** (Core): `LanguageCatalog` exposes `WhisperLanguages` (curated ~99-language set) and `AllLanguages` (CultureInfo-derived fallback). Sentinels `AutoDetectCode = "auto"` / `NoTranslationCode = "none"` / `KeyboardLayoutCode = "keyboard"` (ADR-036; blank means auto, never keyboard).
- **Capabilities** (Core): `LanguageCapabilities` per engine via `SpeechEngineCapabilities.For`, with derived `TranslationForm` — `Full` (arbitrary targets ⇒ Gemma), `Toggle` (one fixed target ⇒ Whisper/English), `None` (cannot translate ⇒ future transcribe-only engines; capability path built, no engine triggers it yet).
- **Keyboard layout** (Core→Platform): `IKeyboardLayoutService.Detect()` → `KeyboardLayoutInfo(code, friendlyName)`; `Win32KeyboardLayoutService` reads the foreground-window thread's HKL (layouts are per-thread on Windows), `NoOpKeyboardLayoutService` elsewhere. `SourceLanguageResolver` (pure) maps the keyboard sentinel to the detected code with auto fallback + optional supported-list validation.
- **Settings keys** (Core): `SelectedSourceLanguage` (may hold `"keyboard"`), `SelectedTargetLanguage`, `TranslationEnabled` (master toggle), per-role MRU `RecentSourceLanguages` / `RecentTargetLanguages` (sentinels never enter MRU). Legacy `TranslateToEnglish` and shared `RecentLanguages` are migrated once via `LanguageSettingsMigrator` and then ignored.
- **Migration** (Core): `LanguageSettingsMigrator.MigrateAsync(settings)` is idempotent and invoked from `AudioPipelineService.CacheSettingsAsync`, `LlamaCppSpeechRecognizer.BuildPromptTextAsync`, and `LanguageRelationshipViewModel.InitializeAsync` so any read of the new keys triggers migration on first run.
- **Pipeline wiring** (Platform): `AudioPipelineService` resolves the keyboard sentinel (validated against `WhisperLanguages`) into `WhisperOptions.Language` and derives `TranslateToEnglish = TranslationEnabled && target == "en" && model.SupportsTranslation` (ADR-033 gate). `LlamaCppSpeechRecognizer.BuildPromptTextAsync` resolves the sentinel, then selects a prompt body via a source/target matrix (ADR-037): the built-in default has dedicated transcription / `TranslationText` / `AutoDetectText` bodies (`{speech_lang}`/`{text_lang}` tokens, `{speech_lang}`→"the detected language" when auto), while a custom single-body prompt gets the translation sentence appended. Translation is gated on `TranslationEnabled` **and** a real target ≠ source (toggle kept for Gemma 4, parity with Whisper).
- **Shared relationship VM** (Desktop): `LanguageRelationshipViewModel` (DI singleton) owns the spec-§7 state machine — source/target/translation state, resting-target restore, MRU, persistence, derived connector (`On`/`Off`/`Locked` + `→`/`=` glyph), summary sentence, and spec-§8 engine-switch fallbacks with auto-clearing toasts (source → keyboard; None → off; Toggle → forced single target, silent when off; Full → reset unknown target). Both UI surfaces delegate to it.
- **Settings page** (Desktop): `LanguageSelectionSettingsViewModel`/`View` — three-column source | connector pill | target layout; target side morphs by `TranslationForm` (ToggleSwitch / picker field / disabled card + amber note); floating popover pickers (`LanguagePickerView` content + `Border.popoverChrome`) with keyboard/auto/off specials, Recent/All groups, >8 search rule; summary line; ADR-033 paused note; toast region.
- **Transcribe quick picker** (Desktop): strip under the record button (`source chip · connector · target chip` — target mirrors source while off); connector flips translation in one click; chips open a 268px flyout above the widget with the target control + a read-only "You speak" row routing to Settings. `TranscribeViewModel` stops recording on `RelationshipChanged`.

## Logging

- ZLogger to console + rolling file
- Log directory: `%LOCALAPPDATA%/parlotype-data/logs/` (via `IAppPaths.LogsDirectory`)

## Model Management

Pipeline: `IModelDownloadService` (Core) → `HttpModelDownloadService` (Platform) → `ModelDownloadDialogService` (Desktop)

- `WhisperModelType` enum (Core) maps to `GgmlType` (Platform) via `WhisperModelTypeExtensions`
- `WhisperModelInfo` holds static metadata (display name, disk size, SHA hash)
- Model choice persisted via `SettingsKeys.SelectedWhisperModel`
- Tests use `HeadlessModelDownloadService` (downloads without UI)
- Gemma 4: `Gemma4ModelInfo` catalog + `Gemma4ModelDownloadService` + `Gemma4ModelDownloadDialogService` (ADR-029); persisted via `SettingsKeys.SelectedGemma4Model`
- Parakeet: `ParakeetModelInfo` catalog + `ParakeetModelDownloadService` + `ParakeetModelDownloadDialogService` (ADR-041); per-model subdir `models/<modelId>/` because upstream file names are generic; persisted via `SettingsKeys.SelectedParakeetModel`

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

User-facing `RuntimePreference` (`Parlotype.Core/Speech/`) maps to Whisper.net's `RuntimeOptions.RuntimeLibraryOrder` — see [[decisions/_index|ADR-022]] (Vulkan) and [[decisions/_index|ADR-049]] (CUDA removal; [[decisions/_index|ADR-012]] is superseded).

| Preference | Whisper.net order | Fallback? |
|------------|-------------------|-----------|
| `Auto` (default) | `[Vulkan, Cpu]` | yes — silent chained |
| `Vulkan` | `[Vulkan]` | **no** — strict |
| `Cpu` | `[Cpu]` | n/a |

- **Bootstrap**: `WhisperRuntimeBootstrap.Initialize(RuntimePreference, ILogger)` sets `RuntimeOptions.RuntimeLibraryOrder` once per process (first-call-wins). `IsSatisfiedBy(preference, loaded)` is the single matcher for "does the loaded library honour this preference" (`Auto` accepts anything; `Cpu` accepts `Cpu`+`CpuNoAvx`).
- **Strict-mode guard**: `WhisperSpeechRecognizer` calls `IVulkanEnvironmentProvider` before factory creation (it no longer takes `INvidiaEnvironmentProvider` — [[decisions/_index|ADR-049]]). On a strict mismatch it throws `RuntimeUnavailableException` (Core) instead of silently falling back to CPU. `TranscribeViewModel` catches it and shows a status-bar message directing the user to Settings.
- **Latch guard** ([[decisions/_index|ADR-048]]): `IWhisperRuntimeStatus` (Core) → `WhisperRuntimeStatus` (Platform) exposes the loaded runtime + `RequiresRestartFor(preference)`. `WhisperSpeechRecognizer.AssertRuntimeStillSelectable` runs **before the model download** in both `InitializeAsync` overloads and throws `RuntimeUnavailableException { RequiresRestart = true }`; the post-load assertion covers a stale *order* latch. Applies to `Cpu` too — a CPU selection under a latched GPU runtime is an error, not a silent GPU run.
- **Factory lifetime** ([[decisions/_index|ADR-048]]): `CreateVerifiedFactory` disposes the `WhisperFactory` on any verification/build failure and `_factory` is assigned only after the processor exists; `UnloadAsync` releases whatever exists regardless of `IsReady`. `WhisperFactory` has no finalizer — see [[whisper-net-quirks]].
- **UI**: `RuntimeSettingsViewModel` + `RuntimeSettingsView` (Settings → Runtime). Persists via `SettingsKeys.RuntimePreference`. Shows "Changes take effect after restart", plus a live "Restart required" panel (`RestartRequired`/`LoadedRuntimeName`) once the selection diverges from the loaded runtime — selection is process-global one-shot.
- **Packages**: `Whisper.net.Runtime.Vulkan` only, always included. No `EnableCuda` flag, no Full/Lite release split ([[decisions/_index|ADR-049]]).
- **NVIDIA detection survives** the CUDA removal: `INvidiaEnvironmentProvider` ([[decisions/_index|ADR-014]]) still runs at startup, but only feeds the diagnostic log — its Settings UI is gone.

## Audio Level & Waveform Visualisation

Real-time visual feedback showing whether the user is speaking. See [[decisions/_index|ADR-023]].

- **Core**: `IAudioLevelProvider` (event `LevelChanged`, property `CurrentLevel`), `AudioLevelEventArgs`, `RecordingState` enum (`Disabled`, `Loading`, `Idle`, `Active`). `IAudioPipeline.PrewarmAsync` (default no-op) pre-loads the model — see [[decisions/_index|ADR-038]]
- **Platform**: `AudioPipelineService` implements `IAudioLevelProvider` — computes RMS on each audio chunk via `PublishAudioLevel()`, fires event. DI forwards via `sp.GetRequiredService<IAudioPipeline>()` cast. `PrewarmAsync` + `StartAsync` share `EnsureModelInitializedAsync` under a `SemaphoreSlim _initLock`.
- **Desktop**: `WaveformView` custom `Control` with `DrawingContext` rendering:
  - **Disabled**: microphone icon via `StreamGeometry`
  - **Loading**: rotating 270° arc spinner (shared `_phase`) while the model loads
  - **Idle**: 13 white bars with gentle sine-wave breathing animation
  - **Active**: 13 white bars with decorative multi-frequency wave animation (amplitude 0.6 default)
  - 60 fps `DispatcherTimer`, attached/detached with visual tree
- **State machine** in `TranscribeViewModel`:
  - `RecordingState.Loading` + `IsLoading` set while `StartAsync` awaits a cold model load; `PrewarmAsync` warms it silently in the background (kicked off from `App`)
  - EMA-smoothed RMS (attack 0.4, decay 0.05) compared against threshold 0.005
  - 1200ms hold-off keeps Active state through natural speech pauses
  - Button turns blue `#378ADD` when recording (Idle or Active) or loading

## Packaging, App Paths & Updates

See [[decisions/_index|ADR-053]]. Cross-cutting: touches Core contracts, Platform
services, the Desktop entry point, and CI.

### App paths — the constraint that shapes everything

Velopack installs to `%LOCALAPPDATA%\{packId}` (packId is `Parlotype`, permanent)
and deletes **that entire folder** on uninstall *and* on a `Setup.exe` re-run.
Because Windows paths are case-insensitive, the old data root
`%LOCALAPPDATA%\parlotype` *was* that folder — see
[[knowledge/velopack-pack-folder-is-destructive]].

- **Core**: `IAppPaths` + `AppPaths` (`.Default`) — one source of truth for every
  write path. Windows root `%LOCALAPPDATA%/parlotype-data`; macOS
  `~/Library/Application Support/Parlotype` (+ `~/Library/Logs/Parlotype`); Linux
  XDG (`$XDG_DATA_HOME` / `$XDG_CONFIG_HOME` / `$XDG_STATE_HOME`).
- Consumers: `JsonSettingsService`, `DpapiSecretStore`, `JsonWindowStateService`
  (injected), plus `HttpModelDownloadService`, `JsonPromptTemplateRegistry`,
  `JsonLlamaServerRegistry`, `LlamaCppSpeechRecognizer`, `ParakeetModelInfo`,
  `Gemma4ModelInfo`, `App.LogDirectory` (via `AppPaths.Default`, where no
  constructor exists).
- `AppPathsTests` fails the suite if any path lands inside the pack folder.
- **No automatic migration** — existing installs are moved by hand
  (`docs/RELEASING.md`).

### Entry point

`VelopackApp.Build().Run()` is the first statement in `Program.Main`. Velopack
re-invokes the same exe with hook arguments and expects handle-and-exit within
15–30 s, so nothing may initialise first. `vpk pack` verifies this statically.
`VelopackFileLogger` gives the hooks somewhere to log, since DI and ZLogger do not
exist yet at that point.

### Uninstall cleanup

User data is kept on uninstall by default. The hook may not show UI, so it cannot
ask — instead `SettingsKeys.UninstallRemovesUserData` (default false) records the
user's choice in advance from Settings → Application → Data, and
`OnBeforeUninstallFastCallback` executes it. The hook parses settings.json
directly (no DI at that point); anything ambiguous reads as false, so data is only
deleted on an unambiguous opt-in. `DataSettingsViewModel` also exposes a
"Delete downloaded models…" action that unloads the recognizer and stops the
`llama-server` sidecar before deleting, since Windows locks loaded model files.
Windows-only — macOS and Linux have no uninstall hooks.

### Updates

- **Core**: `IUpdateService`, `UpdateStatus`, `UpdateState`.
- **Platform**: `VelopackUpdateService` over `UpdateManager` + `GithubSource`.
  Startup check (30 s delay) then every 6 h; background download; apply on
  restart. `IsInstalled`/`NotInstalledException` make dev and portable builds a
  silent no-op.
- **Desktop**: `App` kicks off `StartAsync` fire-and-forget;
  `UpdateSettingsViewModel` drives Settings → Application → Updates.
- **Privacy**: the only outbound request in local mode. Anonymous GET of the
  public GitHub releases API — no machine id, install id, or usage data. Default
  on (`SettingsKeys.UpdatesCheckAutomatically`), disclosed in README and in the
  page itself, one-click opt-out.

### Launch at sign-in

See [[decisions/_index|ADR-059]] and [[windows-run-key-startup-approval]].

- **Core**: `ILaunchAtLoginService` (`IsSupported`/`GetState`/`SetEnabled`, never
  throws), `LaunchAtLoginState` (`Unsupported`/`Disabled`/`Enabled`/
  `BlockedByOperatingSystem`), `SettingsKeys.LaunchAtLogin`.
- **Platform**: `WindowsRunKeyLaunchAtLoginService` over the per-user `HKCU` Run
  key, registering the Velopack **stub** at the install root (never
  `current\`, which is replaced on update) and only for Velopack-installed
  builds. `NoOpLaunchAtLoginService` elsewhere. `LaunchAtLoginCoordinator` owns
  the default-on policy and writes only when preference and OS state differ.
- **Desktop**: `App` reconciles once at startup, fire-and-forget off the UI
  thread; `StartupSettingsViewModel` drives Settings → Application → Startup and
  reconciles again on page load.
- **The OS gets a vote**: Windows stores a Task Manager veto in
  `…\Explorer\StartupApproved\Run`, *separately* from the entry — so the state is
  a 4-value enum, not a bool. Parlotype detects and explains that state but never
  writes the approval blob.
- **Default on, disclosed**: absent preference means on, so upgrades adopt it
  too; the first-run tour's tray step says so and points at the opt-out.

## Onboarding Tour

See [[decisions/_index|ADR-056]]. Desktop-only (Core gains just
`SettingsKeys.OnboardingCompleted`); everything lives under
`src/Parlotype.Desktop/Onboarding/`, `ViewModels/Onboarding/`,
`Views/OnboardingWindow.axaml` and `Services/OnboardingService.cs`.

- **Step model**: `OnboardingStepFactory.Build(bindings)` → eight declarative
  `OnboardingStep` records (welcome / recording / widget / engine / model /
  cloud / tray / recap). `OnboardingWizardViewModel` owns index + Back/Next/Skip
  and opens each step's target window through `IWindowManager`; the recording
  step lists the user's actual `DictationHotkey.DisplayString`s (empty list →
  fallback line, never resurrected defaults — ADR-047).
- **Highlighting**: attached property `OnboardingTarget.Id` marks controls in
  AXAML (ids in `OnboardingTargetIds`, referenced via `{x:Static}` and
  `SpeechEngineDisplayItem.OnboardingId`). `OnboardingHighlightService.Apply`
  scans the visual tree and attaches a pulsing `OnboardingHighlight`
  (DispatcherTimer + `Render`, `WaveformView` idiom) via
  `AdornerLayer.SetAdorner`; unresolved ids retry on `Window.LayoutUpdated`;
  missing/invisible targets are silently skipped. Works headlessly.
- **Window**: `OnboardingWindow` — frameless Topmost 380 px card (ADR-040
  chrome), polls the desktop lifetime for the step's target window
  (`WindowManager` posts fire-and-forget — no completion signal), positions
  itself beside it clamped to the working area, re-activates above the Topmost
  widget. Esc/✕ = Skip.
- **Trigger**: `IOnboardingService`/`OnboardingService`.
  `MaybeShowOnFirstRunAsync` (fire-and-forget from
  `App.OnFrameworkInitializationCompleted`) stamps the flag **before** showing;
  not Velopack-gated, so updaters and dev builds also see it exactly once.
  Re-launch from Settings → Help (`HelpSettingsViewModel`, Application
  category, also a live hotkey reference).
- **Deep links**: `SettingsSection.Engine` / `EngineModel` (resolves to the
  active engine's model page; cloud → Engine fallback) / `Help`.

## Localization (Strings)

First externalized-strings layer (ADR-056): `Resources/Strings.resx` +
hand-written public `Strings` accessor (`ResourceManager`, key-name fallback,
deterministic under CLI builds). Only tour + Help copy so far; all of it flows
AXAML ← VM property ← `Strings`, so a translation is a satellite
`Strings.<culture>.resx` with zero markup changes. `StringsTests` fails if any
accessor property lacks a resx entry. The rest of the app's copy (including
user-visible strings in Core: `HotkeyHint`, `DictationHotkey.ModeLabel`) is
still hardcoded.

## Single Instance & Activation

See [[decisions/_index|ADR-055]]. Desktop-only — no Core or Platform involvement.

`SingleInstanceGuard` (`Desktop/Services/`) takes the named mutex
`Local\Parlotype.SingleInstance` in `Program.Main`, **after `VelopackApp.Run()`**
(hook invocations re-enter the same exe and must not be turned away) **and before
Avalonia** (a process about to exit shows nothing). A launch that loses the race
calls `SignalPrimary()` and returns 0.

- **Why it matters**: every extra process installs its own `TaskPoolGlobalHook`,
  so one hotkey press reaches all of them — several recordings start and whichever
  finishes first injects text. `SuppressEvent` makes the ordering worse, not
  better.
- **Activation**: the primary owns a named auto-reset event and watches it on a
  background thread; `App.OnFrameworkInitializationCompleted` wires the callback to
  `IWindowManager.ShowTranscribe()`, so re-launching a tray-only app opens its
  window instead of appearing to do nothing. The event is created when the mutex
  is won (not when the listener starts), so a signal arriving during startup is
  delivered rather than dropped.
- **Session-scoped, not machine-scoped** (`Local\`): hotkeys, audio and the tray
  belong to a logon session, so a second signed-in user gets their own instance.
- **Fail-open**: `AbandonedMutexException` (killed instance) counts as acquiring;
  any other failure logs to `velopack.log` and reports primary. Not starting is
  worse than starting twice.
- **Windows-only activation**: named `EventWaitHandle`s throw on Unix, so macOS and
  Linux get the lock but a second launch just exits.
- Not a DI service — it predates `BuildServiceProvider`, and lives on
  `Program.SingleInstance` like `Program.TextInjectionMode`.
  `Acquire(name)` takes an override so tests never touch the real lock.
