# Microphone Selection Implementation Plan

## Problem
Allow users to select the default microphone from the list of available system microphones. The list should refresh automatically when microphones are added or removed. Persist the selection across app restarts.

## Approach
- Create `IMicrophoneEnumerator` in Core for listing devices and receiving change notifications
- Implement `WasapiMicrophoneEnumerator` in Platform using NAudio's `MMDeviceEnumerator` + `IMMNotificationClient`
- Update `IAudioCaptureService.StartAsync` to accept a `MicrophoneInfo?` parameter for device selection
- Create `ISettingsService` in Core + `JsonSettingsService` in Platform for persisting settings
- Update `SettingsViewModel` to use real device enumeration instead of hardcoded list
- Update the microphone picker UI to show real devices with auto-refresh

## Workplan

### Phase 1: Core Interfaces
- [ ] Create `IMicrophoneEnumerator` in `Parlotype.Core/Audio/` — GetAvailableMicrophones(), GetDefaultMicrophone(), DevicesChanged event
- [ ] Update `IAudioCaptureService.StartAsync` to accept optional `MicrophoneInfo?` parameter
- [ ] Create `ISettingsService` in `Parlotype.Core/Settings/` — GetAsync<T>, SetAsync<T>
- [ ] Create `AppSettings` record in `Parlotype.Core/Settings/` — SelectedMicrophoneId property
- [ ] Git commit

### Phase 2: Platform — MicrophoneEnumerator
- [ ] Create `WasapiMicrophoneEnumerator` in Platform/Audio implementing `IMicrophoneEnumerator`
- [ ] Use `MMDeviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)` for listing
- [ ] Implement `IMMNotificationClient` for device add/remove/state change notifications
- [ ] Map `MMDevice` → `MicrophoneInfo` (ID, FriendlyName, IsDefault)
- [ ] Fire `DevicesChanged` event on any device change (marshalled to be thread-safe)
- [ ] Git commit

### Phase 3: Platform — Settings Persistence
- [ ] Create `JsonSettingsService` in `Parlotype.Platform/Settings/`
- [ ] Store settings in `AppData/Local/parlotype/settings.json`
- [ ] Implement read/write with file locking
- [ ] Git commit

### Phase 4: Platform — Update WasapiAudioCaptureService
- [ ] Update `StartAsync` to accept `MicrophoneInfo?` and use specified device or default
- [ ] Look up device by ID from `MMDeviceEnumerator`
- [ ] Git commit

### Phase 5: Desktop — ViewModel Integration
- [ ] Update `SettingsViewModel` to inject `IMicrophoneEnumerator` and `ISettingsService`
- [ ] Replace hardcoded microphone list with real enumeration
- [ ] Subscribe to `DevicesChanged` to auto-refresh the list
- [ ] Persist selected microphone via `ISettingsService`
- [ ] Load persisted selection on startup
- [ ] Update `MicrophoneDisplayItem` if needed
- [ ] Register new services in DI (`App.axaml.cs` + `PlatformServiceExtensions.cs`)
- [ ] Git commit

### Phase 6: Build & Test
- [ ] Verify full solution builds with zero warnings
- [ ] Run all existing tests
- [ ] Git commit (if any fixes needed)

## Notes
- `IMMNotificationClient` callbacks come on a COM thread — must marshal to UI thread via `Dispatcher` or use thread-safe event raising
- Settings file location: `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)/parlotype/settings.json`
- `MicrophoneInfo.IsDefault` should reflect whether the device is the system default, not the user's selection

## Fallback & Auto-Select Behavior
- **Device removed:** If the currently selected microphone is removed, automatically fall back to the first available microphone in the list. Update the persisted setting accordingly.
- **Device added:** When a new microphone is added to the system, automatically select it as the active microphone and persist the selection.
- Both behaviors should be handled in `SettingsViewModel` when reacting to `DevicesChanged` events.

---

# Fix Microphone List Flickering + Add Animations

## Problem
When a microphone is added or removed, the list flickers because `RefreshMicrophoneList()` calls `AvailableMicrophones.Clear()` and re-adds all items. This destroys and recreates every UI element. Additionally, `IMMNotificationClient` callbacks come on a COM thread but the `ObservableCollection` is modified without dispatching to the UI thread.

## Approach
1. **Diff-based update** — Instead of clear-and-rebuild, compare old vs new device lists and perform surgical Add/Remove so unchanged items stay untouched.
2. **UI thread dispatch** — Marshal `DevicesChanged` handling to the Avalonia UI thread via `Dispatcher.UIThread`.
3. **Item animations** — Add opacity + translate transitions on the microphone DataTemplate items for smooth add/remove appearance.

## Workplan

- [ ] **SettingsViewModel**: Replace `RefreshMicrophoneList()` clear-and-rebuild with diff-based Add/Remove that only touches changed items
- [ ] **SettingsViewModel**: Dispatch `OnDevicesChanged` to Avalonia UI thread via `Dispatcher.UIThread.InvokeAsync`
- [ ] **MicrophoneDisplayItem**: Add `IsVisible` property with animation support (for delayed removal)
- [ ] **SettingsFlyoutView.axaml**: Add `Transitions` (opacity + translate) on the microphone item Button inside the DataTemplate
- [ ] **Build & test**: Verify 0 warnings, all tests pass
- [ ] **Commit**

## Notes
- Avalonia 11.3.0 supports `Transitions` on controls for property-based animations.
- For list add: new items get Opacity transition from 0→1.
- For list remove: animate Opacity 1→0 via IsVisible=false before actually removing from collection (delayed remove pattern).
- `IMMNotificationClient` fires on COM thread — must use Dispatcher.UIThread for ObservableCollection mutations.
