---
title: "macOS app bundle: signing a .NET layout, and what identity the privacy grants follow"
area: "macos"
summary: "Every file in Contents/MacOS needs its own signature before the bundle is sealed, an ad-hoc signature changes with each build so macOS asks for Accessibility and the microphone again, and an unbundled run uses the launching terminal's grants."
verified: 2026-11-14
---

# macOS app bundle: signing and privacy identity

Learned building `Parlotype.app` ([ADR-078](../decisions/078-macos-support.md),
`scripts/build-macos-app.sh`).

## Signing a self-contained .NET app

A published .NET app keeps its assemblies, JSON files and satellite folders beside the
executable, so they all land in `Contents/MacOS`. `codesign` treats everything there as
code:

- Signing the main executable **is** signing the bundle. Doing it first fails with
  `code object is not signed at all — In subcomponent: …/Microsoft.CSharp.dll`.
- Every other file needs a signature of its own first, including the ones that are not
  Mach-O (`.dll`, `.json`); for those the signature is kept in an extended attribute.
- Order: every file except the main executable, then the bundle. `codesign --verify
  --strict` then passes.

## What the grants are filed under

macOS records Accessibility and Microphone decisions, keychain item access and the login
item against the application's code identity.

- **A Developer ID signature** gives a stable identity: the user answers once.
- **An ad-hoc signature (`--sign -`) is the hash of that build.** Rebuild, and macOS
  has an application it has never seen. Observed: a freshly built bundle is asked about
  the microphone again. Expected for the same reason, not yet observed here: the
  Accessibility switch stops applying until the entry is removed and added again, and
  the keychain asks whether the "new" app may read an item the old one stored. Treat all
  three as normal during development before treating them as bugs.
- **No bundle at all** (`dotnet run`, or the binary under `bin/`): macOS attributes
  requests to the application that launched the process. The grants asked for and used
  are the terminal's or the IDE's, and a missing `NSMicrophoneUsageDescription` in *that*
  application would terminate the process the moment it opened the microphone.

So behaviour seen under `dotnet run` says little about what a user gets. Check anything
involving a prompt with the bundle.

## Accessibility is expected to take effect without a restart

`AXIsProcessTrusted()` is documented to turn true in the running process when the user
flips the switch, and a keyboard event tap created after that works.
`SharpHookHotkeyService` is built on it: it polls every two seconds and installs the hook
when the answer changes, instead of asking the user to relaunch. The waiting half was
observed (the app starts, logs that it is waiting, and keeps polling); the granting half
had not been tried on a real machine when this note was written. If the hook does not
come alive after the switch is flipped, this is the assumption to revisit.

## Overwriting a signed binary in place gets it killed

Unrelated to Parlotype's own bundle but met while setting up: running `dotnet-install.sh`
twice into `~/.dotnet` overwrote the `dotnet` launcher in place, and every start then died
with `Killed: 9`. The crash report says `SIGKILL (Code Signature Invalid)` and the kernel
log `embedded signature doesn't match attached signature`: the kernel caches a signature
per file identity, and the new bytes did not match the cached one. `codesign --verify`
reports the file as valid. Replacing the file rather than rewriting it clears it:

```bash
cp ~/.dotnet/dotnet ~/.dotnet/dotnet.new && mv -f ~/.dotnet/dotnet.new ~/.dotnet/dotnet
```
