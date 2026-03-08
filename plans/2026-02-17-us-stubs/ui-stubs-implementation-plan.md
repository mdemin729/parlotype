---
title: UI stubs
status: completed
created: 2026-02-17
started: 2026-02-17
completed: 2026-02-17
---

# UI Stubs Implementation Plan

## Problem
Create the Parlotype desktop application UI based on the reference screenshots in `plans/UI_planning/`. The app is a compact, floating voice-typing toolbar with a settings flyout. No core logic — only UI stubs using the Fluent theme.

## Approach
Build a chromeless (no title bar) compact toolbar window with three buttons (Settings, Microphone, Help), a settings flyout with sub-menus, and a hint popup. Use CommunityToolkit.Mvvm with source generators, Avalonia Fluent theme. Add supporting enums/models to Core.

## Reference Screenshots Summary
1. **01-main-window** — Compact toolbar: drag handle (center), close button (right). Three icon buttons: ⚙️ Settings, 🎤 Microphone (large, center), ❓ Help.
2. **02-main-window-hint** — Error-style callout bubble above main window: "To use voice typing, select a text box then try again." with a "Got it" button.
3. **03-settings-menu** — Settings flyout/popup from ⚙️ button with items:
   - Voice typing launcher (toggle switch + description)
   - Automatic punctuation (toggle switch)
   - Filter profanity (toggle switch, on by default)
   - Wait time before acting (navigates to sub-menu →)
   - Select default microphone (navigates to sub-menu →)
   - Give feedback (action item)
   - Help us improve voice typing (section with description + "Learn how..." link)
   - "Powered by Microsoft Online Speech Tech" footer (we'll change this to our own branding)
4. **04-settings-wait-time** — Sub-menu for wait time: list of options (Instant 0.1s, Very Short 0.2s, Short 0.3s, Medium 0.5s ●, Long 1.0s, Extended 2.0s, Very Long 3.0s).
5. **05-settings-microphone** — Sub-menu for microphone: list of available mics (stub data), "Add new microphone", "Manage microphone settings".

---

## Workplan

### Phase 1: Core Models/Enums
- [ ] Add `WaitTimeOption` enum to `Parlotype.Core/Speech/` (Instant, VeryShort, Short, Medium, Long, Extended, VeryLong) with a helper to get the display name and seconds value
- [ ] Add `MicrophoneInfo` record to `Parlotype.Core/Audio/` (Id, Name, IsDefault)

### Phase 2: MainWindow — Chromeless Toolbar
- [ ] Update `MainWindow.axaml` to be chromeless (no title bar, `ExtendClientAreaToDecorationsHint`, custom drag handle + close button)
- [ ] Update `MainWindow.axaml` layout: three buttons in a row (Settings ⚙️, Microphone 🎤 large center, Help ❓)
- [ ] Update `MainWindow.axaml.cs` for drag behavior on the handle
- [ ] Update `MainWindowViewModel.cs` with commands for each button and state properties (IsRecording, IsSettingsOpen, IsHelpOpen, IsHintVisible)

### Phase 3: Settings Flyout
- [ ] Create `SettingsViewModel.cs` with all settings properties (VoiceTypingLauncherEnabled, AutoPunctuationEnabled, FilterProfanityEnabled, SelectedWaitTime, SelectedMicrophone, list of microphones, list of wait time options)
- [ ] Create `SettingsFlyoutView.axaml` — a UserControl used inside a Flyout/Popup, showing all settings items as in screenshot 03
- [ ] Wire up the settings flyout to the ⚙️ button in MainWindow

### Phase 4: Sub-menus (Wait Time + Microphone)
- [ ] Create `WaitTimePickerView.axaml` — nested flyout/popup showing the list of wait time options with current selection indicator
- [ ] Create `MicrophonePickerView.axaml` — nested flyout/popup showing stub microphone list + "Add new microphone" + "Manage microphone settings"
- [ ] Wire sub-menus to their respective settings items

### Phase 5: Hint Popup
- [ ] Create `HintPopupView.axaml` — a callout/popup with error icon, message text, and "Got it" button
- [ ] Wire the hint popup visibility to MainWindowViewModel.IsHintVisible
- [ ] Add a `DismissHintCommand` to close the hint

### Phase 6: DI Registration & Wiring
- [ ] Register new ViewModels in `App.axaml.cs` DI container
- [ ] Verify the app builds with zero warnings
- [ ] Smoke-test the UI launches correctly

---

## Notes
- Use text/unicode characters or Avalonia built-in icons for button icons (⚙, 🎤, ❓, ✕). If needed, consider PathGeometry or simple text.
- All toggle switches use Avalonia's `ToggleSwitch` control.
- Sub-menus for "Wait time" and "Select microphone" can be Flyout or Popup controls.
- Branding: replace "Powered by Microsoft Online Speech Tech" with "Powered by Whisper" or similar.
- Keep all logic as no-ops / stubs — button commands do nothing except toggle UI state.
- The hint popup can default to hidden; we may add a stub button or timer to show it for testing.
