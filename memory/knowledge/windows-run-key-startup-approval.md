---
title: Windows Run-key autostart and the StartupApproved veto
type: knowledge
tags: [windows, registry, startup, velopack, autostart]
created: 2026-08-08
summary: The HKCU Run key is only half the story — Explorer records the user's Task Manager enable/disable decision in a separate StartupApproved blob that silently overrides it
---

# Windows Run-key autostart and the `StartupApproved` veto

Learned while implementing launch at sign-in ([[059-launch-at-sign-in]]).

## Two keys decide whether an app starts, not one

| Key | Owner | Role |
|---|---|---|
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | the app | The entry itself: value name → command line |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run` | Explorer | The user's Task Manager → Startup apps decision |

Disabling an app in **Task Manager → Startup apps** does **not** delete the
`Run` value. Explorer writes a veto into `StartupApproved\Run` under the *same
value name* and stops honouring the entry. An app that reads only its own `Run`
value therefore reports "autostart is on" while nothing launches — the exact bug
a naive bool-valued setting ships with.

`StartupApproved` values are 12-byte `REG_BINARY` blobs. The **low bit of byte 0
is the disabled flag**:

- `02 00 …` — enabled
- `06 00 …` — enabled (seen after the user toggled it back on)
- `03 00 …` — disabled

So `(blob[0] & 1) == 1` means blocked. An absent value means never touched, i.e.
enabled.

**Do not write this key.** The format is undocumented, Explorer caches it, and
forging an approval overrides a decision the user made deliberately. Detect it,
report it, and send the user to Task Manager — the only thing that reliably
lifts its own veto.

## `Microsoft.Win32.Registry` needs no package on a plain TFM

On `net10.0` (no `-windows` suffix) the registry APIs resolve from the shared
framework with **no `PackageReference`**. The only cost is CA1416
("only supported on: 'windows'"), cleared by `[SupportedOSPlatform("windows")]`
on the type — which matters under `TreatWarningsAsErrors`. Verified by
compiling, not assumed.

## Register the Velopack stub, not the versioned exe

A real Velopack install root (`%LOCALAPPDATA%\Parlotype`) contains:

```
Parlotype.exe     ~420 KB   <- stub launcher; the stable path
Update.exe        ~3.9 MB
current\                    <- replaced wholesale on every update
packages\
```

The stub is what an autorun entry (or any shortcut) should point at. Anything
under `current\` is replaced on update. `VelopackLocator.Current` exposes
`RootAppDir`, `CurrentlyInstalledVersion` and `IsPortable`; a null
`CurrentlyInstalledVersion` is the signal for "not installed by Setup.exe"
(`dotnet run`, IDE, unpacked zip), where registering a temporary path would
leave a broken autorun entry outliving the app. `VelopackLocator.Current` throws
when `VelopackApp.Run()` never ran, so wrap the read.

See also [[velopack-pack-folder-is-destructive]].

## Testing against the real registry is cheap

Point the service at a scratch HKCU subkey (`Software\<App>.Tests\<guid>`)
instead of mocking the registry — the production code path runs for real, and
`DeleteSubKeyTree` in `Dispose` leaves nothing behind. Delete the empty parent
non-recursively afterwards so parallel instances don't clobber each other.
