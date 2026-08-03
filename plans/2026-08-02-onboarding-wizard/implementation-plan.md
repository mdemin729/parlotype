# First-Run Onboarding Wizard (Parlotype)

## Context

Parlotype starts tray-only: `App.OnFrameworkInitializationCompleted` never sets `MainWindow`, so after installation a new user sees literally nothing — no window, no hint about hotkeys, the widget, or settings. We add a step-by-step onboarding wizard that auto-opens once (simple `OnboardingCompleted` settings flag — user confirmed: anyone without the flag sees it once, including updaters and first dev run) and is re-launchable from a new **Settings → Help** section. The differentiator: each step **opens the real app window it describes and highlights the live UI elements** (record button, engine cards, model list, ✕ button…), instead of showing static pictures. Texts are English but externalized (first localization layer in the repo) so translations can be added without touching markup.

Verified codebase facts the plan builds on:
- `IWindowManager.ShowTranscribe(activate)/ShowSettings(SettingsSection?)` ([WindowManager.cs](src/Parlotype.Desktop/Services/WindowManager.cs)) — singleton hide-don't-close windows, bodies in `Dispatcher.UIThread.Post` (no completion signal).
- Deep-link: `enum SettingsSection { Language, CloudProviders }` → `SettingsWindowViewModel.NavigateTo` (silent no-op if section hidden for active engine). Cloud providers section is **invisible under default Parakeet** — the cloud step targets the two cloud engine cards on the Engine page instead.
- TranscribeWindow already names `RecordButton`, `GripZone`, `CloseButton`, `LanguageStrip`; height flips 118↔88 (Parakeet hides the strip). Settings views have almost no named controls.
- Hotkey display text exists in Core: `HotkeyGesture.DisplayString` ("Hold Right Ctrl"), `DictationHotkey.ModeLabel`, `HotkeyHint.Describe`. Runtime read: `IGlobalHotkeyService.Bindings` + `BindingsChanged`; an empty stored list is a deliberate user choice — never assume non-empty.
- No highlight/overlay precedent (no AdornerLayer usage), no resx/localization anywhere, no first-run flag.
- Bool settings house convention: stored as string, default-off read = `bool.TryParse(saved, out var v) && v`.

## Key design decisions

1. **Highlight = attached-property markers + AdornerLayer adorner.** Elements marked in AXAML with `onb:OnboardingTarget.Id`; `OnboardingHighlightService.Apply(window, ids)` finds marked controls via `GetVisualDescendants()` and attaches a pulsing-outline `OnboardingHighlight` control with `AdornerLayer.SetAdorner`. The adorner layer re-arranges over the control on every layout pass → the 118↔88 flip, strip show/hide, and Settings content swap need zero tracking code. Animation uses the house idiom (custom `Control` + `DispatcherTimer` + `Render`, like `WaveformView`) — no Avalonia Animations. Ids not yet found/visible go on a pending set retried on `window.LayoutUpdated`. Missing ids never throw. **Spike the headless adorner test first** (risk #1).
2. **Localization = `Resources/Strings.resx` + hand-written static accessor** (`internal static class Strings`, `ResourceManager`, one property per key). Standard .NET culture fallback / satellite assemblies later; deterministic under CLI build with warnings-as-errors. All copy flows AXAML ← VM properties ← `Strings`; markup never hardcodes text.
3. **First-run = `SettingsKeys.OnboardingCompleted`** (string-bool, default off). Checked in `OnboardingService.MaybeShowOnFirstRunAsync()` called fire-and-forget from `App.OnFrameworkInitializationCompleted`; the flag is written **when the wizard auto-shows** (not on close) so "only once" holds even if the app is killed mid-tour.
4. **Wizard = separate compact non-modal Topmost window** (frameless card, ~380 px wide, `SizeToContent=Height`, drag header, ✕=Skip, Esc=Skip) that repositions next to the current step's target window (right of it, clamped to the working area; left if no room) and re-`Activate()`s itself after the target appears. Nothing goes into Core except the settings key constant — the wizard is pure Desktop; **no new Core interface**.
5. **No model downloads triggered**: the wizard only calls `ShowTranscribe`/`ShowSettings(section)`; it never executes `SelectCommand`, download or recording commands (navigation to Engine/model pages is download-free — verified).

## Steps (8)

Target-id constants live in `OnboardingTargetIds`; AXAML references them via `{x:Static}`.

| # | id | Opens | Highlights | Content (dynamic parts in *italics*) |
|---|----|-------|-----------|---------------------------------------|
| 1 | welcome | — | — | What Parlotype does; recognition is local by default, audio never leaves the machine unless a cloud engine is opted into |
| 2 | recording | Transcribe (`activate:false`) | `Transcribe.Record` | Start via hotkeys or widget button; Esc cancels; text is typed into the previously focused app. *DetailLines = each valid binding as "{DisplayString} — {ModeLabel}" from `IGlobalHotkeyService.Bindings` + Esc line; empty list → localized "No dictation hotkey set…" fallback* |
| 3 | widget | Transcribe | `Transcribe.Grip`, `Transcribe.Close`, `Transcribe.LanguageStrip` | Widget anatomy: record button, status = tooltip on the card (ADR-040), drag grip, ✕ hides to tray, language strip for engines with language choices (invisible targets — strip under Parakeet — silently skipped; text phrased engine-agnostically) |
| 4 | engine | Settings @ `SettingsSection.Engine` (new) | `Settings.EngineList` | Engine choice lives here; per-engine pages appear in the nav |
| 5 | model | Settings @ `SettingsSection.EngineModel` (new, resolves to active engine's model page) | `Settings.ModelList` | Picking a model; models download automatically on first use; "Installed" markers |
| 6 | cloud | Settings @ `SettingsSection.Engine` | `Settings.EngineCard.OpenAiCompatible`, `Settings.EngineCard.XaiGrok` | Cloud engines are opt-in, BYOK, send audio to the provider; Cloud badge on the widget while active |
| 7 | tray | Transcribe | `Transcribe.Close` | Closing keeps the app running in the tray; tray click reopens; Exit only via tray menu |
| 8 | recap | — | — | Where to find everything later; reopen the tour from Settings → Help. *DetailLines = `[HotkeyHint.Describe(bindings)]`* |

## Work items (execution order)

**WI-0 — Project workflow chores (per plans/WORKFLOW.md):** create `plans/2026-08-02-onboarding-wizard/` with `task.md` (frontmatter `status: in_progress`, `created`/`started`: 2026-08-02) + `implementation-plan.md` (this content); add row to `plans/INDEX.md` → In Progress.

**WI-1 — Core key:** [SettingsKeys.cs](src/Parlotype.Core/Settings/SettingsKeys.cs) — add `OnboardingCompleted` const with doc comment (string-bool; default off = not yet shown; written when the wizard auto-shows).

**WI-2 — Strings:** new `src/Parlotype.Desktop/Resources/Strings.resx` (SDK auto-embeds, no csproj change) with keys `Onboarding_{Welcome|Recording|Widget|Engine|Model|Cloud|Tray|Recap}_{Title|Body}`, `Onboarding_Nav_{Back|Next|Finish|Skip}`, `Onboarding_Progress_Format` ("Step {0} of {1}"), `Onboarding_Hotkeys_None`, `Onboarding_Recording_EscLine`, `Onboarding_WindowTitle`, `Help_{Title|Intro|HotkeysHeading|NoHotkeys|EscCancelLine|OpenTourButton|OpenTourCaption}`; new `src/Parlotype.Desktop/Resources/Strings.cs` accessor (falls back to key name if missing).

**WI-3 — Deep links:** [SettingsSection.cs](src/Parlotype.Desktop/ViewModels/SettingsSection.cs) — add `Engine`, `EngineModel`, `Help`; [SettingsWindowViewModel.cs](src/Parlotype.Desktop/ViewModels/SettingsWindowViewModel.cs) `NavigateTo` — `Engine → SpeechEngine`, `Help → Help` (WI-8), `EngineModel →` switch on `SpeechEngine.SelectedEngine`: Parakeet→`ParakeetModel`, Whisper→`WhisperModel`, Gemma4→`Gemma4Model`, cloud→fallback `SpeechEngine`.

**WI-4 — Target markers:** new `src/Parlotype.Desktop/Onboarding/OnboardingTarget.cs` (non-static class, `AttachedProperty<string?> IdProperty` + Get/Set) and `OnboardingTargetIds.cs` (consts listed in the table; `Settings.EngineCard.{SpeechEngine member name}`). Mark: `RecordButton`/`GripZone`/`CloseButton`/`LanguageStrip` in [TranscribeWindow.axaml](src/Parlotype.Desktop/Views/TranscribeWindow.axaml); the cards `ItemsControl` in [SpeechEngineSettingsView.axaml](src/Parlotype.Desktop/Views/Settings/SpeechEngineSettingsView.axaml) + card `Button` gets `onb:OnboardingTarget.Id="{Binding OnboardingId}"` (new `OnboardingId => $"Settings.EngineCard.{Type}"` on `SpeechEngineDisplayItem`); the model-list `ItemsControl` in ParakeetModelSettingsView / WhisperModelSettingsView / Gemma4ModelSettingsView all get `Settings.ModelList`.

**WI-5 — Highlight (spike test first):** new `src/Parlotype.Desktop/Onboarding/OnboardingHighlight.cs` (Control, `IsHitTestVisible=false`, ~80 ms timer, `Render` strokes a 2 px rounded rect in `#378ADD`, opacity pulsing 0.35–1.0, timer start/stop on attach/detach) and `OnboardingHighlightService.cs` (singleton; `Apply(Window, IReadOnlyList<string>)` — clear previous, scan visual descendants for id matches, `AdornerLayer.SetAdorner` on `IsEffectivelyVisible` matches, pending set retried on `LayoutUpdated`; `Clear()`). Fallback if AdornerLayer is absent headlessly: manually positioned overlay Border — encapsulated in the service, call sites unchanged.

**WI-6 — Step model + VM:** new in `src/Parlotype.Desktop/Onboarding/`: `OnboardingTargetWindow` enum (None/Transcribe/Settings), `OnboardingStep` record (Id, Title, Body, TargetWindow, SettingsSection?, TargetIds, DetailLines), `OnboardingStepFactory.Build(IReadOnlyList<DictationHotkey>?)` → the 8 steps from `Strings`. New `src/Parlotype.Desktop/ViewModels/Onboarding/OnboardingStepItemViewModel.cs` (step + `IsCurrent` for dots) and `OnboardingWizardViewModel.cs` — ctor `(IWindowManager, IGlobalHotkeyService?)`; `Steps`, `CurrentIndex` (+`OnCurrentIndexChanged` → notify `CurrentStep`/`ProgressText`/`IsFirstStep`/`IsLastStep`/`NextButtonText`/`HasDetailLines`, update `IsCurrent`, `ActivateCurrentStep()`); `Back/Next/Skip` RelayCommands (Skip + final Next raise `CloseRequested`); `Start()` rebuilds steps (fresh hotkey text on re-launch); `ActivateCurrentStep` calls `ShowTranscribe(activate:false)` / `ShowSettings(step.SettingsSection)`. VM never touches highlighting or persistence — headless-testable with `MockWindowManager`/`MockGlobalHotkeyService`.

**WI-7 — Wizard window:** new `src/Parlotype.Desktop/Views/OnboardingWindow.axaml(.cs)` — frameless Topmost card (Width 380, SizeToContent=Height, local styles so tests don't need app styles): drag header + ✕(Skip); Title; Body; DetailLines list; "Step N of M" + dot ItemsControl (`Classes.active="{Binding IsCurrent}"`); footer Skip left / Back + Next-or-Finish right; Esc→Skip. Code-behind: on `CurrentIndex` change → clear highlight → poll desktop lifetime `Windows` (50 ms, ≤2 s, abort on step change) for the visible target window → `PositionRelativeTo(target)` (right side, tops aligned, clamp to `WorkingArea` via `PixelPoint`/`DesktopScaling`; left if no room; no-target steps keep position) → `highlightService.Apply` → `Activate()` self; `Closed` → `Clear()`.

**WI-8 — Help section:** new `src/Parlotype.Desktop/ViewModels/Settings/HelpSettingsViewModel.cs` (`Title => Strings.Help_Title`, `Category => SettingsCategory.Application`, always visible; ctor `(IGlobalHotkeyService?, IOnboardingService)`; `HotkeyLines` = "{DisplayString} — {ModeLabel}" + Esc line, fallback `Help_NoHotkeys`, rebuilt on `BindingsChanged`; text properties for all copy; `OpenTourCommand → onboarding.ShowWizard()`), new `Views/Settings/HelpSettingsView.axaml(.cs)`, DataTemplate in [SettingsWindow.axaml](src/Parlotype.Desktop/Views/SettingsWindow.axaml), 17th ctor param + property + `_allSections` entry (after `data`) in `SettingsWindowViewModel`.

**WI-9 — Service + wiring:** new `src/Parlotype.Desktop/Services/IOnboardingService.cs` (`Task MaybeShowOnFirstRunAsync(); void ShowWizard();`) + `OnboardingService.cs` — `ShouldAutoShowAsync()` = house default-off read of the flag; `MaybeShowOnFirstRunAsync` (try/catch-log) → if unseen: `SetAsync(flag, "True")` then `ShowWizard()`; `ShowWizard()` posts to UI thread, creates/reuses the `OnboardingWindow` (WindowManager `PlatformImpl` pattern), `vm.Start()`, wires `CloseRequested→Close`, `Show()+Activate()`; `protected virtual` seam for trigger tests. [App.axaml.cs](src/Parlotype.Desktop/App.axaml.cs): register `HelpSettingsViewModel`, `OnboardingWizardViewModel`, `OnboardingHighlightService`, `IOnboardingService→OnboardingService`; after `_hotkeyCoordinator.StartAsync()` add `_ = ...MaybeShowOnFirstRunAsync();`.

**WI-10 — Tests** (`src/Parlotype.Desktop.Tests/`, new `Mocks/MockOnboardingService.cs`):
- `OnboardingStepFactoryTests` (plain xunit): 8 steps in order, non-empty texts, recording DetailLines from `DictationHotkeyDefaults.All` ("Hold Right Ctrl — Push to talk"…), empty-list fallback, correct sections/target ids.
- `OnboardingWizardViewModelTests`: Start/Next/Back bounds, progress/button text, `IsCurrent`, per-step `MockWindowManager` effects (`ShowTranscribeCount`, `LastSettingsSection`), Skip/final-Next raise `CloseRequested`.
- `OnboardingServiceTests` (spy subclass): unset flag → shows once + writes "True"; "True" → no show; garbage → shows; idempotent second call.
- `OnboardingHighlightServiceTests` (`[AvaloniaFact]`, **spike**): adorner attached after `Apply`, `Clear` detaches, unknown id no-throw, invisible target picked up after becoming visible, re-`Apply` supersedes.
- `OnboardingWindowTests` (`[AvaloniaFact]`): chrome present, Next click advances, Esc raises `CloseRequested`.
- `HelpSettingsViewModelTests`: title/category/visibility, lines + refresh on `UpdateBindings`, null-service fallback, `OpenTourCommand` counts.
- `SettingsWindowViewModelTests` (modify): `BuildViewModel` 17th arg, append "Help" row in all 4 `Assert.Collection` nav tests, new `NavigateTo` tests for Engine/EngineModel(per-engine)/Help.
- `StringsTests`: reflection over `Strings` properties — non-empty, not equal to key name.

**WI-11 — Docs/DoD:** ADR `docs/decisions/056-first-run-onboarding-wizard.md` (Desktop-only subsystem, no Core interface; attached-property+adorner highlight; resx as first localization layer; flag semantics incl. updaters-see-it-once as chosen; new deep links; known limitation — wizard repositions only on step change, not on target drag). Vault: `memory/services/desktop.md` (onboarding subsystem, Help section, Strings), `memory/decisions/_index.md` (ADR-056 row), `memory/architecture/subsystems.md` (onboarding + localization). Knowledge note only if non-derivable facts emerge (e.g. AdornerLayer headless quirks). Close the plan folder: `status: completed`, INDEX.md row removed. End-of-session note in `memory/sessions/`.

## Verification

1. `dotnet build Parlotype.slnx` — zero warnings; `dotnet test` — all pass (incl. updated SettingsWindowViewModelTests).
2. Manual end-to-end: delete `OnboardingCompleted` from `%LOCALAPPDATA%/parlotype-data/settings.json` → `dotnet run --project src/Parlotype.Desktop` → wizard auto-opens; walk all 8 steps checking each opens the right window, highlights pulse on the right elements (record button; grip/✕/strip behavior under Parakeet; engine cards; Parakeet model list; cloud cards; ✕ again), Back/Next/Skip/progress dots work, recording step shows the real configured bindings (change one in Settings → Hotkeys, relaunch tour from Help, text updates).
3. Restart app → wizard does **not** auto-open; Settings → Help shows current hotkeys and relaunches the tour.
4. Confirm no model download dialog appears at any point during the tour.

## Risks

- **AdornerLayer under headless/FluentTheme** unverified — spike WI-5's test first; fallback overlay is service-internal.
- **Window-appearance race** (`Dispatcher.Post` fire-and-forget) — mitigated by poll + `LayoutUpdated` retry; worst case a step shows text without highlight (degraded, not broken).
- Two Topmost windows (wizard + widget): z-order handled by re-activation + non-overlapping placement; cosmetic only.
