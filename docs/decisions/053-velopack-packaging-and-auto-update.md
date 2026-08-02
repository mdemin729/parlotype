---
status: accepted
date: 2026-08-01
---

# 053. Velopack packaging and auto-update

## Context

Parlotype has shipped as a zip attached to a GitHub release. Users unzip it
somewhere and run the exe. That has three problems:

1. **No update path.** Users find out about new versions by chance, and updating
   means re-downloading ~80 MB and manually replacing a folder.
2. **No install identity.** No Start Menu entry, no Add/Remove Programs entry, no
   stable install location.
3. **Download size.** Every release is a full download even when a single
   assembly changed.

We also carry an unusual constraint: the app ships large native dependencies
(whisper.cpp, sherpa-onnx, Vulkan runtime) and downloads speech models that run
from hundreds of MB to several GB. Those models are *not* bundled, are expensive
to re-acquire, and are the user's property.

## Decision

Adopt **Velopack 1.2.0** (`vpk` CLI + `Velopack` NuGet package, kept in lock-step)
as the installer and update framework.

### Pack id: `Parlotype`

Permanent. Velopack derives the install root `%LOCALAPPDATA%\Parlotype` from it,
so changing it later would orphan every existing install.

### Self-contained, always

`--self-contained true` on the publish, **and** `<SelfContained>` in
`Parlotype.Desktop.csproj` so the setting survives someone editing the CI script.
On .NET 8+ a RID no longer implies self-contained, so a lost flag produces a
framework-dependent build that passes CI and fails on the user's machine.

The csproj property is conditioned on a RID being present
(`Condition="'$(RuntimeIdentifier)' != ''"`) because the SDK rejects
self-contained without one (NETSDK1031); that keeps plain `dotnet build` and
`dotnet test`, which pass no RID, working.

`--framework` is deliberately **not** passed to `vpk pack`. It is only for
non-self-contained apps, it would add an elevation prompt to setup, and it is
unavailable on macOS and Linux — which would force two divergent packaging
models the moment those platforms land.

### Per-user install, no elevation

Setup installs to `%LOCALAPPDATA%` with no UAC prompt. No step in the pipeline
requires administrator rights. The `--msi` machine-wide bootstrapper is not used.

### The data directory had to move: `%LOCALAPPDATA%\parlotype-data`

This is the sharp edge of the whole change. Velopack owns
`%LOCALAPPDATA%\{packId}` outright: it replaces `current\` on every update, and it
deletes the **entire** pack folder on uninstall *and* on a re-run of `Setup.exe`
([velopack#120](https://github.com/velopack/velopack/issues/120)).

Every piece of user data used to live in `%LOCALAPPDATA%\parlotype` — settings,
DPAPI-encrypted API keys, window state, saved prompts, logs, and the multi-GB
model cache. Windows paths are case-insensitive, so with packId `Parlotype` that
folder **is** the pack folder. Shipping that combination would have deleted every
downloaded model and the user's API keys the first time anyone uninstalled or
re-ran the installer.

The data root therefore moved to `%LOCALAPPDATA%\parlotype-data`, which shares no
prefix with the pack folder under any casing.

All path logic now lives behind a single `IAppPaths` (Core), with a BCL-only
`AppPaths` implementation:

| Data | Windows | macOS | Linux |
|---|---|---|---|
| Models | `%LOCALAPPDATA%\parlotype-data\models` | `~/Library/Application Support/Parlotype/models` | `$XDG_DATA_HOME/parlotype/models` |
| Settings | `%LOCALAPPDATA%\parlotype-data` | `~/Library/Application Support/Parlotype` | `$XDG_CONFIG_HOME/parlotype` |
| Logs | `%LOCALAPPDATA%\parlotype-data\logs` | `~/Library/Logs/Parlotype` | `$XDG_STATE_HOME/parlotype/logs` |

`AppPaths` sits in Core rather than Platform, against the usual rule, because
`ParakeetModelInfo` and `Gemma4ModelInfo` are Core types that must resolve the
model cache, and Core cannot reference Platform. It depends on nothing but the
BCL, so the rule's actual purpose — keeping platform packages out of Core — is
not violated. `AppPathsTests` asserts that no path resolves inside the pack
folder, compared case-insensitively.

**No automatic migration ships.** Existing installs keep data at the old
`%LOCALAPPDATA%\parlotype`; moving it is a documented manual step
(`docs/RELEASING.md`). A migration that half-completes while moving several GB of
models is worse than one the user performs deliberately.

### One channel

One packaging channel per platform (`win`, later `osx`/`linux`) — Velopack's
default. No stable/beta split: it doubles the release matrix and the test surface
for a single-maintainer project. Pre-release *tags* (`v1.2.0-beta.1`) still work;
they upload as GitHub pre-releases and are skipped by the updater, which requests
stable releases only.

### Update checks default to on

`Updates.CheckAutomatically` defaults to **true**. Parlotype's promise is that
*audio* never leaves the machine, not that the process never opens a socket. The
check is an unauthenticated GET of a public release feed
(`https://api.github.com/repos/mdemin729/parlotype/releases`) carrying no machine
identifier, install id, or usage data. Defaulting it off means most users never
receive fixes — a worse outcome than one anonymous request every six hours.

It is disclosed in the README, stated in the Settings page next to the toggle,
and switching it off stops all updater traffic.

### Entry point

`VelopackApp.Build().Run()` is the first statement in `Program.Main`, before
Avalonia, DI, ZLogger, and the argument parser. Velopack re-invokes the same
executable with hook arguments during install/update/uninstall and expects it to
handle them and exit within 15–30 seconds; anything initialising first either
blows that budget or paints a window during a silent install. `vpk pack` verifies
this statically and fails the build if the call is missing.

Hooks: `OnFirstRun` pre-creates the data directories.
`OnBeforeUninstallFastCallback` (Windows-only) handles uninstall cleanup.

### Downloading stages; it does not install

`DownloadUpdatesAsync` only places the package in the local packages folder.
Velopack installs nothing until an apply call runs — its own docs say
`UpdatePendingRestart` returns an asset that *"requires a call … to be applied"*.
An ordinary quit-and-relaunch therefore does **not** pick up a downloaded update;
it stays staged indefinitely.

The first cut of this ADR got that wrong: it downloaded, reported "ready", and
told the user a restart would finish the job, when in fact only the Settings
button ever applied anything. Users who never opened Settings would have
re-downloaded every release forever and never installed one.

`IUpdateService` therefore has two apply paths:

| Path | Call | When |
|---|---|---|
| `ApplyOnExit()` | `WaitExitThenApplyUpdates(silent: true, restart: false)` | Every shutdown, from `App`'s `Exit` handler |
| `ApplyAndRestartAsync()` | `ApplyUpdatesAndRestart(asset)` | The "Install and restart now" button |

`ApplyOnExit` is **synchronous** on purpose. A shutdown handler is not a reliable
place to await a continuation, so the hand-off must complete inline; it only needs
to launch `Update.exe`, which then waits for this process to exit, so there is
nothing worth awaiting anyway. A `_applying` latch keeps the shutdown path from
racing an explicit restart that is already exiting through Velopack.

The user-visible wording follows the real behaviour: "installs when you quit
Parlotype", not "restart to finish updating".

### Uninstall cleanup: consent recorded in advance

Leaving several GB of models behind forever is untidy; deleting them without
asking is data loss. Velopack's hook cannot resolve this directly — it may not
show UI, so it cannot ask at uninstall time.

The resolution is to move the consent earlier rather than give up on it.
`SettingsKeys.UninstallRemovesUserData` (default **false**) is set from
Settings → Application → Data, where a real toggle and a real confirmation dialog
are available. The hook does not decide anything; it reads that flag and executes
a decision the user already made. Anything ambiguous — missing file, corrupt JSON,
absent key, unexpected value type — reads as false, so data is only removed on an
unambiguous opt-in.

The hook runs before DI exists, so it parses `settings.json` directly with
`System.Text.Json` rather than through `ISettingsService`.

Because that flag gates a destructive action, the write is **not** fire-and-forget
like every other settings toggle. `DataSettingsViewModel` awaits the write, reads
it back to confirm it landed, and reverts the toggle with a visible warning if it
did not — a switch showing "keep my data" while `settings.json` still says "delete
everything" is silent data loss. The pending write is exposed as `PendingWrite` and
awaited during shutdown so the process cannot exit with the two disagreeing.

Default-off matters: a large share of uninstalls are really troubleshooting
reinstalls, where discarding the model cache is the wrong outcome. The same page
also offers "Delete downloaded models…" for reclaiming disk space at any time —
that one unloads the recognizer and stops the `llama-server` sidecar first, since
Windows locks a loaded model file.

Both are Windows-only for now: macOS and Linux have no uninstall hooks at all
(uninstalling there is just deleting the bundle), so the toggle has no effect
until those platforms grow an equivalent.

## Consequences

**Easier**

- Users get a real installer, Start Menu entry, and Add/Remove Programs entry.
- Updates are incremental: the 0.1.0 → 0.1.1 delta measured 96 KB against a
  77 MB full package.
- Adding macOS/Linux is a new row in the CI matrix; nothing outside `matrix`
  names a RID, runner, or channel.
- User data is now behind one abstraction with a test that fails if anything
  drifts back inside the pack folder.

**Harder / accepted costs**

- The pack id can never change.
- Existing users must move `%LOCALAPPDATA%\parlotype` to `parlotype-data` by hand,
  or re-download their models. This is the deliberate cost of not shipping an
  unattended multi-GB migration.
- Builds are unsigned, so Windows SmartScreen warns on first run. Code signing is
  planned separately; the pipeline has a documented slot for it and must keep
  working without credentials.
- The app now makes a network request by default. Mitigated by disclosure and a
  one-click opt-out, but it is a real change to the app's behaviour.
- Update operations only work in an installed build. `dotnet run` reports
  `UpdateState.NotInstalled` and no-ops — correct, but it means the update path
  cannot be exercised from the IDE.

## Alternatives rejected

- **Squirrel.Windows** — effectively unmaintained; Velopack is its successor by
  the same author with a cross-platform story.
- **MSIX** — requires a signing certificate before anything installs at all, and
  its container model fights an app that manages multi-GB caches and a sidecar
  process.
- **Inno Setup / WiX + a hand-rolled updater** — gives an installer but no delta
  updates, and the updater is the part most likely to be got subtly wrong.
- **Keeping packId `Parlotype` and leaving data in `%LOCALAPPDATA%\parlotype`** —
  data loss on uninstall/reinstall. Not viable.
- **Renaming the packId to dodge the collision** (e.g. `Parlotype.Desktop`) —
  would have avoided touching any data paths, but bakes an awkward name into the
  install directory permanently. Moving the data folder was judged the better
  permanent trade.
