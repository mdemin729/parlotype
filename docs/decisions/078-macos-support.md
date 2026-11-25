---
status: accepted
date: 2026-11-14
amends: [042, 043, 047, 051, 057, 059, 064]
---

# 078. macOS support

## Context

Parlotype ran on Windows only. The architecture was already shaped for more
([ADR-002](002-solution-architecture.md)): Core holds no platform code, and every native
dependency sits behind a Core interface. But the Platform implementations behind those
interfaces existed only for Windows, and on any other system the app either failed at
startup or ran with its main functions replaced by no-ops:

| Capability | Windows implementation | Elsewhere before this ADR |
|---|---|---|
| Microphone capture, device list | WASAPI through NAudio | threw at startup (COM) |
| Paste into the focused app | Win32 clipboard, `SetForegroundWindow` | P/Invoke into `user32.dll` failed |
| Cloud API keys | DPAPI | base64, not encrypted |
| Start at sign-in | `HKCU` Run key | unsupported |
| Keyboard-layout language | Win32 | unavailable |
| `llama-server` install | zip, `llama-server.exe` | "not yet implemented" |

Every package the app depends on ships macOS binaries for Intel and Apple Silicon
(Avalonia, SharpHook, Whisper.net, sherpa-onnx, Velopack), and the solution compiled on
macOS without a change. What was missing was the platform layer, and the places where the
app's behaviour and wording assume a PC.

The work was done and checked on an Intel MacBook Pro that cannot run anything newer than
macOS 13.7. That machine set one constraint of its own, described under "Which macOS".

## Decision

**macOS is a supported platform, for Intel and Apple Silicon. It gets its own
implementations of the Core contracts, written as plain P/Invoke against system
frameworks, and its own defaults and wording where a Mac differs from a PC. Nothing about
the Windows build changes.**

### Native access is P/Invoke, not a binding package

The macOS surface Parlotype needs is small: an audio queue, a device list, a pasteboard,
the frontmost application, the keychain, two privacy checks, the current input source.
Each is a handful of C functions or Objective-C messages, and all of it is in
`Parlotype.Platform` behind the existing interfaces. `MacOsNative` holds the shared
CoreFoundation and Objective-C runtime calls.

A binding package (or a native audio library such as PortAudio) would have been one more
dependency and, for anything native, one more binary to sign and notarize. The cost of
doing without is that the interop is ours to get right, under two rules written down in
`MacOsNative`: no struct returns through `objc_msgSend` (the calling convention differs
between x64 and arm64), and an autorelease pool around every message sent from a thread
AppKit does not own.

| Contract | macOS implementation | Mechanism |
|---|---|---|
| `IAudioCaptureService` | `CoreAudioCaptureService` | Audio Queue Services. The queue is asked for 16 kHz mono float directly and converts inside CoreAudio, so there is no resampler. |
| `IMicrophoneEnumerator` | `CoreAudioMicrophoneEnumerator` | CoreAudio HAL. A device's id is its UID, which survives a replug. |
| `ITextInjectionService` | `MacOsClipboardTextInjectionService` | `NSPasteboard` with restore, then Cmd+V through SharpHook. The dictated text carries the nspasteboard.org transient markers, the counterpart of the Windows clipboard-history exclusion formats. |
| `ITargetWindowTracker` | `MacOsTargetWindowTracker` | macOS activates applications, not windows: the target is a process id, sampled from `NSWorkspace` every 250 ms and reactivated before the paste only if it is no longer frontmost. |
| `ISecretStore` | `KeychainSecretStore` | Generic-password items in the login keychain, service "Parlotype". `secrets.json` is not used on macOS. |
| `ILaunchAtLoginService` | `MacOsLaunchAgentLaunchAtLoginService` | A property list in `~/Library/LaunchAgents`. |
| `IKeyboardLayoutService` | `MacOsKeyboardLayoutService` | Text Input Sources, on the main thread. |
| `ISystemPermissionService` (new) | `MacOsSystemPermissionService` | `AXIsProcessTrusted`, `AVCaptureDevice` authorization. |

### Privacy permissions are a first-class state

macOS gates two things Parlotype cannot work without, and neither fails loudly when it is
missing: without **Accessibility** the keyboard hook is never installed and the paste
keystroke is dropped; without **Microphone** access capture delivers silence.

- `ISystemPermissionService` (Core) reports and requests them. Other platforms register a
  no-op that answers "granted", so callers do not branch on the OS.
- **Accessibility.** At startup, before the first-run tour, the app explains what the
  permission is for and opens System Settings on the right pane. `SharpHookHotkeyService`
  no longer starts a hook it knows will fail: it waits, polls every two seconds, and
  installs the hook when the permission appears. The user is not asked to restart.
- **A dictation made without Accessibility is not thrown away.** The record button on
  the widget needs no hotkey, so a user can dictate before granting it; the first person
  to try the port did exactly that, and the recognized text vanished without a word. The
  paste cannot be posted in that state, so `MacOsClipboardTextInjectionService` leaves the
  text on the clipboard as an ordinary copy instead of restoring the old contents over it,
  and throws `TextInjectionNotPermittedException`; the widget then says the text is on the
  clipboard and offers the way to the switch. The same happens when the permission reads
  as granted and the keystroke is refused anyway.
- **Microphone.** `CoreAudioCaptureService` asks for access before it starts and throws
  `MicrophoneAccessDeniedException` when refused, which the widget turns into a dialog
  with a button to the right Settings pane.
- **CoreAudio is never called first on the UI thread.** The first CoreAudio call in a
  process can show the microphone prompt and block until it is answered
  ([`coreaudio-first-call-can-block`](../knowledge/coreaudio-first-call-can-block.md)).
  The device list therefore starts empty and arrives through `DevicesChanged`, and
  `MicrophoneSettingsViewModel` restores the saved microphone when it does.

### Defaults that differ on a Mac

- **Hotkeys** (amends [ADR-047](047-multi-binding-dictation-hotkeys.md)). A MacBook has no
  Right Ctrl. The macOS set is **hold Right Command** (push-to-talk) and **double-tap
  Ctrl** (toggle). There is no chord: Ctrl+Option+Space is macOS's own "select next input
  source", and a suppressed chord would take that away from the multilingual users this
  app is for. `DictationHotkeyDefaults.ForCurrentPlatform` is what runtime code reads.
- **Which modifier cancels a hold** (amends [ADR-057](057-cancel-dictation-on-command-shortcuts.md)).
  That rule says a keystroke under a command modifier ends a hold whenever it lands. On a
  Mac the command modifiers are Ctrl and Command, and Option composes text like Shift
  does. `HotkeyGestureMatcher` takes the platform as an argument so both rule sets are
  tested on any machine.
- **Key names.** The interface says Option and Cmd on a Mac. Logs keep Core's invariant
  Alt and Win everywhere.
- **Menu bar app.** No Dock icon and no application menu (`LSUIElement`,
  `MacOSPlatformOptions.ShowInDock = false`): the counterpart of living in the tray.
- **Start at login** (amends [ADR-059](059-launch-at-sign-in.md)). Only a copy in
  `/Applications` or `~/Applications` registers itself. A build run from the repository
  or a disk image has a path that is about to disappear. The launch agents folder is
  fixed by the operating system, like the Run key, and is not an `IAppPaths` location.
- **No uninstall choice.** The "delete my data on uninstall" setting is hidden outside
  Windows: nothing runs when a Mac app is dragged to the Trash.

### Wording that differs on a Mac

Amends [ADR-064](064-ui-localization-foundation.md). Most copy is the same everywhere.
Eleven strings name Windows things (the tray, Task Manager, `Setup.exe`, "sign in to
Windows"). Each keeps its key and its Windows wording, and gains a sibling key ending in
`_Mac`. `Localizer.Lookup` returns the sibling on macOS when one exists, so markup and
view models keep asking for the one base key. A `_Mac` key is an ordinary key to every
localization check and is translated into every language.

The alternative, one neutral sentence with the OS name as a placeholder, would have
changed the English of eleven shipped strings and still could not say "menu bar" in one
place and "tray" in another.

One difference was too small for a variant: `llama-server.exe` has no extension off
Windows. File names are never translated, so the token is rewritten in the lookup.

### Which macOS

The stated floor is **macOS 15**, which is what .NET 10 supports and what the Whisper.net
and sherpa-onnx binaries declare. Apple Silicon builds require it.

Intel builds also run **below macOS 14.4**, with one engine missing (verified on 13.7;
amends [ADR-042](042-parakeet-default-language-ux.md)). The ONNX Runtime that sherpa-onnx
ships for macOS does not load below 14.4
([`macos-native-library-os-floors`](../knowledge/macos-native-library-os-floors.md)).
That takes out Parakeet, and it takes out Silero VAD with it, which runs before every
engine. On those systems:

- `LegacyOnnxRuntime` points the VAD at Microsoft's ONNX Runtime 1.18.1, the version
  SileroVad was written against, which runs on macOS 11. It is copied to a folder of its
  own and loaded by path, and only where the bundled runtime cannot load.
- The default engine is Whisper instead of Parakeet (`SpeechEngineDefaults`).
  Parakeet, if selected by hand, fails with a message that names the reason.

This costs one package reference (`Microsoft.ML.OnnxRuntime`, macOS builds only, 28 MB in
an Intel bundle) and an OS-version branch. It exists because the last Intel Macs are
exactly the ones that cannot upgrade, and because without it the port could not have been
run end to end on the machine it was written on. Apple Silicon gets no fallback: every
such Mac can run macOS 15.

### Packaging

`scripts/build-macos-app.sh` publishes self-contained for one architecture, assembles
`Parlotype.app` from `packaging/macos/Info.plist`, and signs it, ad hoc by default. The
bundle identifier `dev.parlotype.Parlotype` is permanent in the way the Velopack pack id
is: the privacy grants, the keychain items and the login item are filed under it.

Three publish filters in `Directory.Build.targets` assumed Windows and are corrected
(amends [ADR-051](051-publish-only-target-rid-runtimes.md)): the Whisper folder names,
and the Windows ONNX Runtime that the GPU package copies into every output.

### API keys

Amends [ADR-043](043-cloud-speech-providers-v1.md): on macOS, keys are stored in the login
keychain rather than base64-encoded in `secrets.json`. Linux keeps the base64 fallback.

## Consequences

- **Verified on macOS 13.7, Intel.** The solution builds with no warnings; all tests
  pass; both bundles build (Intel and, cross-built, Apple Silicon); the Intel bundle
  starts as a menu bar app in about 1.5 s, lists input devices, and waits correctly for
  both permissions. Silero VAD and Whisper transcribe in the test suite through the
  fallback runtime. `llama-server` tarballs unpack with their links and executable bits.
- **Seen working in a first hands-on run** (Intel, macOS 13.7, the bundle): the
  microphone prompt, live capture from the widget's button, the Whisper model download,
  and recognition of Russian and English speech.
- **Not yet seen working.** That run was made without the Accessibility permission, so
  everything behind it is still unconfirmed: the hotkey firing, the Cmd+V paste, and
  focus returning to the target application. So is how the windows and the menu bar icon
  look. The code for these follows the Windows flow step for step.
- **Not verified, because it needs other hardware or another macOS.** Anything on Apple
  Silicon; Parakeet on macOS (14.4 and later); the Text Input Sources path on macOS 14
  and later, where a call off the main thread aborts the process.
- **Not done.** The release pipeline has no macOS row, there is no Developer ID signing
  or notarization, and no Velopack package, so nothing here reaches a user yet and
  auto-update is untested on macOS. The menu bar icon is the colour app icon, not a
  template image. The icon is upscaled from 256 px artwork. The microphone usage sentence
  in `Info.plist` is English only. These are tracked in
  `plans/2026-11-14-macos-release-readiness/`.
- **No test touches CoreAudio.** Its first call hangs inside the test host; capture and
  device listing are verified by running the app.
- **Tests no longer depend on the machine they run on.** The UI test assembly pins the
  Windows wording, key names, default hotkeys and default engine once at load
  (`TestPlatformDefaults`), and the macOS answers are tested through overloads that take
  the platform as an argument. Two test seams in Core exist for that
  (`DictationHotkeyDefaults.PinnedSet`, `SpeechEngineDefaults.PinnedDefault`).
- **Two ONNX Runtime versions are referenced** on a Mac build machine. They are never
  loaded into one process: the fallback is used only where the other cannot load.
- **Adding Linux** now means the same list again: capture, clipboard, focus, secrets,
  login item, and a look at every `_Mac` key for whether Linux needs its own wording.
