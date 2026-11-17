---
title: "The first CoreAudio call in a process can block"
area: "macos"
summary: "Initialising the audio hardware layer shows the microphone prompt for an app that has not been asked yet and does not return until it is answered; in the vstest host it never returns at all."
verified: 2026-11-14
---

# The first CoreAudio call in a process can block

Observed on macOS 13.7 (Intel, built-in audio through `AppleHDA`) while porting Parlotype
([ADR-077](../decisions/077-macos-support.md)). Not derivable from Apple's documentation,
which describes the microphone prompt as something that appears when recording starts.

## What happens

The first call into the CoreAudio hardware-abstraction layer (HAL) in a process
initialises it: `AudioObjectGetPropertyData`, `AudioObjectAddPropertyListener` and
`AudioQueueNewInput` all do. That initialisation opens the built-in audio driver:

```
AudioObjectAddPropertyListener
  HALSystem::CheckOutInstance → HALSystem::InitializeDevices
    HALC_ShellDevice::_InitializeDriverPlugIn
      AudioDriverPlugInOpen (AppleHDAHALPlugIn)
        IOServiceOpen → mach_msg2_trap        ← waits here
```

For an application the user has **not yet answered the microphone question for**, macOS
shows its permission prompt at this point, and the call does not return until the prompt is
answered. Merely listing input devices is enough; nothing has to be recorded. `tccd` logs it:

```
AUTHREQ_PROMPTING: service=kTCCServiceMicrophone, subject=Sub:{dev.parlotype.Parlotype}
```

Once the question has an answer (either one) the same call returns in milliseconds.

## Consequences for the code

- **Never make the first CoreAudio call on the UI thread.** The first build of the macOS
  bundle listed microphones while constructing a view model at startup and froze behind
  the prompt before drawing anything. `CoreAudioMicrophoneEnumerator` now makes that call
  on a background thread, reports no devices until it returns, and then raises
  `DevicesChanged`. `CoreAudioCaptureService` creates its queue on a background thread
  for the same reason.
- **A consumer must tolerate an empty first list.** `MicrophoneSettingsViewModel` used to
  select and persist "the first device" whenever nothing was selected, which with a late
  list overwrote the saved microphone on every launch. It now falls back to the saved id.
- **A process started from a terminal borrows the terminal's answer.** macOS attributes
  the request to the "responsible" application, so `dotnet run` asks on behalf of
  Terminal (or the IDE, or an agent's host application), and passes straight through if
  that application already has microphone access. Only the `.app` bundle shows the
  behaviour a user gets. Build it with `scripts/build-macos-app.sh`.

## The test host never returns

Inside the vstest host (`dotnet test`) the same first call blocked for as long as the run
was left alone (ten minutes), at the same `IOServiceOpen` frame, although `tccd` had
answered "allowed" for the launching application. The identical code returned at once in
a plain C program, in a .NET console app (through the app host and through `dotnet x.dll`),
and in the desktop app started from a shell. Why the test host differs was not found.

So **no test touches CoreAudio**. Device listing and capture are verified by running the
app. A test that only sometimes hangs is worse than no test, and on a hosted macOS runner
it would take the whole job with it.

## How to see it

```bash
sample <pid> 2 -file /tmp/stack.txt          # look for IOServiceOpen under AudioObject*
log show --last 2m --info --predicate 'process == "tccd" AND eventMessage CONTAINS[c] "microphone"'
```
