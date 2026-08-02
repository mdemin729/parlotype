---
title: Velopack's pack folder is destructive — never store user data under %LOCALAPPDATA%\{packId}
type: knowledge
tags: [velopack, packaging, paths, windows, data-loss]
created: 2026-08-01
summary: Velopack deletes the entire %LOCALAPPDATA%\{packId} folder on uninstall AND on a Setup.exe re-run; Windows case-insensitivity means a data folder differing only in case is the same directory — the collision that forced Parlotype's data root to move to parlotype-data (ADR-053)
---

# Velopack's pack folder is destructive

## The rule

Velopack installs to `%LOCALAPPDATA%\{packId}` on Windows and treats that whole
directory as its own:

```
%LOCALAPPDATA%\{packId}\
  current\          <- replaced wholesale on every update
  packages\         <- nupkg cache
  Update.exe
  {AppName}.exe     <- stub that launches current\
```

Two things get destroyed there, not one:

| Event | What is removed |
|---|---|
| Update | `current\` only |
| **Uninstall** | **the entire `{packId}` folder** |
| **Re-running `Setup.exe`** | **the entire `{packId}` folder** ([velopack#120](https://github.com/velopack/velopack/issues/120)) |

The reinstall case is the one that surprises people: a user re-running the
installer to "repair" the app is a routine act, and it wipes everything stored
there. Velopack's own docs say only files stored **outside** `RootAppDir`
survive.

## The trap: Windows paths are case-insensitive

This is what nearly shipped in Parlotype. The packId was to be `Parlotype`, and
the existing data root was `%LOCALAPPDATA%\parlotype`:

```
%LOCALAPPDATA%\Parlotype    <- Velopack pack folder
%LOCALAPPDATA%\parlotype    <- Parlotype's models, settings, DPAPI API keys
```

On Windows these are **the same directory**. The two look distinct in a diff, in
a config table, and in review — nothing about them reads as a collision until you
remember NTFS is case-insensitive. Shipping it would have deleted multi-gigabyte
model caches and the user's encrypted cloud API keys on the first uninstall or
repair-install.

Resolution (ADR-053): keep packId `Parlotype`, move the data root to
`%LOCALAPPDATA%\parlotype-data`, which shares no prefix under any casing.

## How this is prevented from recurring

- All write paths resolve through `IAppPaths` / `AppPaths` (Core) — nothing
  composes a data path by hand.
- `AppPathsTests` asserts, **case-insensitively**, that no `IAppPaths` member
  equals or is nested inside `%LOCALAPPDATA%\Parlotype`. A future path added
  under the wrong root fails the test suite.

## Related

- Velopack's own guidance: data that should survive uninstall goes in
  `%AppData%\{packId}`; data that should die with the app goes one level up from
  `current\` — i.e. still inside the pack folder. Parlotype uses neither, because
  models are far too large for a roaming profile and far too expensive to lose.
- See [[decisions/_index|ADR-053]].
