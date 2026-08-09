---
status: accepted
date: 2026-08-08
---

# 059. Launch at sign-in, on by default

## Context

Parlotype is driven by a global hotkey and starts tray-only —
`App.OnFrameworkInitializationCompleted` never sets `MainWindow` (ADR-056). The
consequence is blunt: **if the process is not running, the product does not
exist.** A user who installs Parlotype, restarts their machine and presses their
dictation hotkey gets nothing, with no visible clue why. Every comparable
utility (PowerToys, Ditto, Everything) starts with Windows for exactly this
reason.

Nothing in the app registered itself for startup, and there was no setting for
it.

## Decision

Register Parlotype for launch at sign-in, **on by default**, with a one-click
opt-out at Settings → Application → Startup.

### `HKCU\...\CurrentVersion\Run`, not a Startup-folder shortcut

The per-user `Run` key needs no elevation (matching the per-user Velopack
install of ADR-053), needs no COM, and — the deciding factor — is what Task
Manager → Startup apps lists. That is where users go when they want something to
stop launching, so registering anywhere else hides the entry from the one place
they will look.

A Startup-folder `.lnk` would have required `IShellLink` P/Invoke for no gain.
Task Scheduler buys delayed and elevated starts that this app does not want.

`Microsoft.Win32.Registry` resolves on the plain `net10.0` TFM with **no package
reference**; it only needs `[SupportedOSPlatform("windows")]` to satisfy CA1416
under `TreatWarningsAsErrors`.

### The registered path is the Velopack stub

`%LOCALAPPDATA%\Parlotype\Parlotype.exe`, never
`…\Parlotype\current\Parlotype.exe`. Velopack replaces `current\` wholesale on
every update; the stub at the install root is the launcher that survives it.
The install root also holds `Update.exe`, and both were confirmed present in a
real install rather than assumed from the docs.

The path is resolved from `VelopackLocator.Current`, and resolution returns
**null** — meaning "cannot register" — for anything Velopack did not install:
`dotnet run`, the IDE, an unpacked zip. Registering the path of a build that is
about to move or be deleted would leave a broken autorun entry behind that
outlives the app. This mirrors `UpdateState.NotInstalled` in ADR-053, and the
Settings page greys the toggle out with the reason rather than offering a
control that can only fail.

### Default-on applies to upgrades too, and is disclosed

`SettingsKeys.LaunchAtLogin` follows the house string-bool convention, but
inverted relative to most: **absent or unparsable means on**, the same shape as
`UpdatesCheckAutomatically`. Existing installs therefore adopt launch-at-sign-in
on their first launch after updating, not only fresh installs.

Silently adding an autorun entry would be the wrong way to ship this — it is
precisely the behaviour that erodes trust in an app whose pitch is
local-by-default. The first-run tour (ADR-056) already had a "Parlotype lives in
the tray" step, and it now states that Parlotype starts with Windows and where
to switch that off. The disclosure lives in `Strings.resx` alongside the rest of
the tour copy, so it translates with everything else.

Considered and rejected: enabling only for fresh installs, discriminated via
Velopack's `OnFirstRun`. It would have spared existing users a surprise, but it
leaves the majority of the current user base — the people who already decided
they want this app — without the behaviour that makes the hotkey work, and it
splits one policy into two code paths.

### Four states, because the OS gets a vote

`ILaunchAtLoginService.GetState()` returns an enum, not a bool, because the
stored preference and reality can genuinely disagree:

| State | Meaning |
|---|---|
| `Unsupported` | Not a Velopack install, or not Windows |
| `Disabled` | Nothing registered |
| `Enabled` | Registered and permitted |
| `BlockedByOperatingSystem` | Registered, but switched off in Task Manager |

That last one is the sharp edge. Windows records a user's Task Manager veto in
`…\Explorer\StartupApproved\Run` as a 12-byte blob whose low bit of byte 0 is
the disabled flag — **separately from the `Run` value itself**. Disabling
Parlotype there leaves our entry in place and simply stops honouring it. Without
reading that blob, the Settings toggle would cheerfully read "on" while nothing
launched.

Parlotype **does not write** `StartupApproved`. The format is undocumented and
Explorer caches it, so forging an approval is unreliable and, more to the point,
overrides a decision the user made deliberately. The blocked state is detected,
reported, and explained — the Settings page sends the user to Task Manager,
which is the only thing that can lift its own veto.

A registration pointing at a *different* path is reported as `Disabled` rather
than as its own state. That is what a moved or reinstalled app leaves behind,
and reporting it as "not registered" makes the ordinary reconcile path rewrite
it with no special case.

### One reconciler, two callers

`LaunchAtLoginCoordinator` (Platform) owns the default-on rule and the
"OS may disagree" handling, because two callers need both:

- `App` reconciles once at startup, off the UI thread, fire-and-forget.
- `StartupSettingsViewModel` reconciles when the page loads and writes through
  the coordinator on every toggle.

`ReconcileAsync` writes only when the stored preference and the observed state
differ, so calling it on every launch is cheap, and it is also what repairs a
stale entry. It deliberately does nothing at all when the state is
`BlockedByOperatingSystem`: rewriting would not clear the veto, and flipping the
stored preference would erase a deliberate user decision.

Nothing in this subsystem throws. Launch-at-sign-in is a convenience; a registry
that will not cooperate must never disrupt startup or wedge the Settings page.
Failures are logged and reported as `Disabled`.

### macOS and Linux

`NoOpLaunchAtLoginService` reports `Unsupported`. macOS wants an `SMAppService`
login item and Linux an XDG autostart `.desktop` file; both are real work and
neither ships here.

## Consequences

**Easier**

- The hotkey works from sign-in, which is the only way the product's core
  interaction is reliable.
- The entry is visible and removable in Task Manager, in Settings, and by
  uninstalling — three independent ways out.
- Adding macOS/Linux is one new `ILaunchAtLoginService` implementation and one
  line in `PlatformServiceExtensions`; nothing else knows the mechanism.

**Harder / accepted costs**

- Parlotype now writes to the user's registry outside its own data directory.
  It is one HKCU value under the user's own account — no service, no scheduled
  task, nothing machine-wide — and it is removed when the toggle goes off.
- Existing users get an autorun entry they did not explicitly ask for on their
  next update. Mitigated by the tour disclosure and the one-click opt-out, but
  it is a real behaviour change on machines that already had the app.
- The `StartupApproved` blob is undocumented. It is read defensively — an
  unreadable or malformed value answers "not blocked" so the toggle keeps
  working — but a future Windows change to that format would degrade the
  explanation, not the feature.
- The registered path is coupled to Velopack's install layout. Verified against
  a real install and covered by tests, but a Velopack change to where the stub
  lives would need this revisited.

## Alternatives rejected

- **Startup-folder shortcut** — `IShellLink` COM P/Invoke, and invisible in the
  Task Manager list users actually check.
- **Task Scheduler** — needs COM or `schtasks`, and its selling points (delayed
  start, elevation) are things this app does not want.
- **Default off** — makes the hotkey silently dead after every reboot for anyone
  who never opens Settings, which is most people.
- **Writing `StartupApproved` to force-enable** — undocumented, cached by
  Explorer, and it overrides a decision the user made on purpose.
