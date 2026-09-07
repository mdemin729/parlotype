---
status: accepted
date: 2026-09-06
---

> **Amended by [ADR-066](066-drop-the-vestigial-llama-server-directory.md).**
> The "derive the hint from `IAppPaths`" decision below did not survive review of
> the value it derived: `LlamaServerDirectory` named a folder nothing ever created,
> and has been removed. The manual box now starts empty with an instructional
> placeholder. Everything else here — the pack-folder detection, the warning, and
> the decision not to migrate stored paths — stands unchanged.

# 065. Manual llama-server folders inside the pack folder are warned about, not migrated

## Context

ADR-053 moved every path Parlotype writes to out of `%LOCALAPPDATA%\parlotype`,
because Windows paths are case-insensitive and that folder *is* the Velopack pack
folder `%LOCALAPPDATA%\Parlotype`, which Velopack deletes wholesale on uninstall
**and** on a re-run of `Setup.exe`. The data root became `parlotype-data`, and
`AppPathsTests` fails the build if any `IAppPaths` member drifts back inside the
pack folder.

That guarantee covers paths *Parlotype* composes. It never covered paths the
**user** supplies — and the app has one of those: Settings → Speech engine →
llama.cpp → **Manual install**, backed by `SettingsKeys.LlamaCppServerFolder`.

The placeholder in that box read:

```
%LOCALAPPDATA%\parlotype\llama-server
```

which is the pre-ADR-053 default that the managed default `LlamaServerDirectory`
(`…\parlotype-data\llama-server`) replaced. So the UI was instructing users to put
a hand-downloaded llama-server build — several hundred MB of it — in the one folder
the uninstaller and the installer both erase. This is not hypothetical: at least
one real `settings.json` in the wild stores
`C:\Users\<user>\AppData\Local\parlotype\llama-server\`.

Three questions followed: fix the hint; do anything about the values already
stored; and say anything in the UI about a manual install that lives there.

## Decision

### The hint is derived, not written down

`PlaceholderText` now binds to `LlamaCppSettingsViewModel.DefaultServerFolderHint`,
which returns `AppPaths.Default.LlamaServerDirectory`. Correcting the literal to
`parlotype-data` would have fixed today's bug and left the same class of bug in
place — a second copy of a path that has already moved once. A hint read from
`IAppPaths` cannot drift from it, and it is right on macOS and Linux too, where the
literal was never right at all. It costs the `%LOCALAPPDATA%` shorthand: the box
now hints the fully expanded path, which matches the expanded path the picker
writes into it anyway.

### Stored pack-folder paths are **not** migrated

A stored `LlamaCppServerFolder` under `…\parlotype\…` is left exactly as the user
wrote it. Rewriting it to `parlotype-data` would point the recognizer at a folder
that does not contain `llama-server.exe` and break a manual install that currently
works; *moving* the binaries to match is the several-hundred-MB half-completing
migration ADR-053 already declined to ship for the model cache, and it would be
Parlotype relocating files it did not download. The path is the user's statement of
where their build is, and it is still true.

Note also that a stored value is not automatically *in use*: the registry
(ADR-managed installs) decides between Managed and Manual, and the manual path only
resolves when Manual is active.

### The risk is surfaced instead

`VelopackPackFolder` (Core, BCL-only, alongside `AppPaths`) holds the pack id and
answers `Contains(path)`. `LlamaCppSettingsViewModel` recomputes
`IsServerFolderInsidePackFolder` on every change to `ServerFolder` — so the warning
appears both for a value loaded from `settings.json` and as the user types or
browses — and the manual-install panel shows an amber panel naming the pack folder
and what removes it.

The copy is three keys (`…_PackFolderWarningHeading` / `…Body` / `…PathLabel`) in
English, Russian and Spanish. The **path itself is bound as a value beside a
translated label rather than substituted into the sentence** — a mid-sentence slot
would hand Russian a noun it must inflect and cannot, which is the composite-format
trap ADR-064 documents. `PackFolderRoot` on the view model supplies it; a path is
data, and data is never translated.

The warning does not block Save, disable the box, or refuse the path. It is the
user's machine and their build; what was missing was that they had no way to know
the folder was volatile.

Two details in `Contains` matter:

- **Case-insensitive**, because the collision it detects only exists because of
  case-insensitivity.
- **The separator is part of the prefix.** `parlotype-data` shares the pack
  folder's first nine characters, so a bare `StartsWith` would flag every managed
  install as at-risk. A test pins this.

`Root` is `null` off Windows, where there is no Velopack install root yet, so the
check is inert there rather than guessing.

## Consequences

**Easier**

- The manual-install hint can no longer disagree with `IAppPaths`; there is one
  place that knows where llama-server goes.
- A user already in this situation is told, on the page where they can act, in
  terms of the actual consequence ("erased when Parlotype is uninstalled").
- `VelopackPackFolder` gives any future user-supplied path — a model directory, an
  export target — the same check for one line.

**Harder / accepted**

- Users who ignore the warning still lose their build on uninstall. Nothing short
  of moving their files prevents that, and moving them is what this ADR declines
  to do.
- The hint is a long absolute path rather than `%LOCALAPPDATA%\…`.
- `VelopackPackFolder.PackId` duplicates the literal in `AppPathsTests`. That is
  deliberate: the test's copy is an independent assertion about production code and
  loses its value if it reads the constant it is checking.
