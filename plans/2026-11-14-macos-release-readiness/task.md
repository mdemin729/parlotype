---
title: macOS release readiness
status: planned
created: 2026-11-14
started:
---

# macOS release readiness

## Problem

[ADR-077](../../docs/decisions/077-macos-support.md) added the macOS platform layer and it
was checked as far as one Intel Mac on macOS 13.7 and no person at the keyboard could take
it: the app builds, passes its tests, starts as a menu bar app and waits for its two
permissions. Everything behind those permissions is unverified, nothing has run on Apple
Silicon, and there is no way to ship a Mac build to a user.

## Approach

Verify on real machines first, because the remaining code risk is all in what could not be
watched. Only then spend on signing and the release pipeline. Each item below says what
"done" looks like.

## Workplan

### 1. Verify on an Intel Mac (macOS 13)

Build with `scripts/build-macos-app.sh`, then `open artifacts/macos/x64/Parlotype.app`.

- [x] Microphone prompt appears; after Allow, the log says
      `Microphone enumerator initialized`. (2026-11-14, first hands-on run. Capture from
      the widget's button, the Whisper download and recognition of Russian and English
      also worked. The paste did not: Accessibility was off, which is what the next items
      are about.)
- [ ] Without Accessibility, a dictation started from the widget's button ends with the
      "Your text is on the clipboard" dialog, and Cmd+V pastes it.
- [ ] Accessibility dialog appears; "Open System Settings" lands on the Accessibility
      list with Parlotype in it; after switching it on, the log says
      `Accessibility access granted — global hotkeys are now active` **without a restart**.
- [ ] Hold Right Command: the widget appears without taking focus, records, and on release
      the text is pasted into the app that had focus (try TextEdit, a browser field, a
      terminal).
- [ ] Double-tap Ctrl toggles. Esc cancels. Right Cmd+C during a hold discards it.
- [ ] Clicking the widget's record button (Parlotype becomes frontmost) still pastes into
      the previous app.
- [ ] The clipboard holds what it held before, and a clipboard manager (if one is
      installed) did not record the dictated text.
- [ ] Refusing the microphone shows the "Microphone access is switched off" dialog and its
      button opens the right Settings pane.
- [ ] The Transcribe widget is frameless, rounded, on top, draggable, and remembers its
      position. The menu bar icon is visible in light and dark menu bars.
- [ ] Settings: the Startup page reads sensibly for a copy outside `/Applications`; hotkey
      rows say Cmd / Option; recording a new chord with Cmd works; the Data page has no
      uninstall switch.
- [ ] A cloud API key saved in Settings appears in Keychain Access under "Parlotype" and
      survives a restart.
- [ ] Gemma 4: an `llama-server` build installs from the list (tarball) and starts. The
      upstream Intel build may itself need a newer macOS; record what happens.
- [ ] Copy the bundle to `/Applications`, enable "Start at login", log out and in.

Known limit of this machine: Parakeet cannot run (needs macOS 14.4), so the default engine
here is Whisper.

### 2. Verify on Apple Silicon (macOS 15 or later)

- [ ] `scripts/build-macos-app.sh --arch arm64` on the machine itself, then everything in
      section 1.
- [ ] Parakeet is the default and transcribes; Silero VAD loads from sherpa-onnx's own
      ONNX Runtime (no "fallback ONNX Runtime" line in the log).
- [ ] Source language "Keyboard layout" resolves and does not crash: Text Input Sources
      abort the process on macOS 14+ if called off the main thread, and the dispatch path
      has only been exercised through its timeout.
- [ ] Whisper: which backend loads (CPU or Metal), and how fast.
- [ ] `dotnet test` on that machine, including that nothing hangs.

### 3. Things known to be unfinished

- [ ] Menu bar icon: ship a monochrome template image and set
      `MacOSProperties.IsTemplateIcon`; today it is the colour app icon.
- [ ] App icon: real 1024 px artwork; today it is upscaled from the 256 px `.ico`.
- [ ] `NSMicrophoneUsageDescription` is English only. Localize it through
      `InfoPlist.strings` per language, generated from resx so it stays in the pipeline.
- [ ] Hide or disable the Parakeet card on macOS older than 14.4 instead of letting it be
      selected and fail (needs new copy in every language).
- [ ] Decide whether `HotkeyConflictDetector`'s reserved list should be per-OS: it blocks
      Windows shortcuts (Win+L, Win+E…) on a Mac, where those are ordinary Cmd shortcuts.
- [ ] "Login Items" was renamed "Login Items & Extensions" in macOS 15, and in some
      languages the new name does not start with the old one (see
      `docs/localization/outstanding.md`).

### 4. Ship it

- [ ] Developer ID certificate; sign with the hardened runtime and
      `packaging/macos/Parlotype.entitlements`; notarize and staple. Confirm the microphone
      and the JIT work under the hardened runtime.
- [ ] `osx-x64` and `osx-arm64` rows in `.github/workflows/release.yml`, with an `.icns`,
      `--plist packaging/macos/Info.plist` and macOS signing steps.
- [ ] Velopack on macOS: pack, feed on the `osx` channel, a real update from one version to
      the next, and `IUpdateService` reporting "installed".
- [ ] Decide what "installed" means for launch at login once Velopack is in: today it is
      "lives in an Applications folder".
- [ ] README download section, CHANGELOG entry without the "build it yourself" caveat,
      website.
