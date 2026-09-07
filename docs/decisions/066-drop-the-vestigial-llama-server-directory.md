---
status: accepted
date: 2026-09-06
---

# 066. Drop the vestigial `LlamaServerDirectory`; the manual folder starts empty

## Context

ADR-065 corrected the manual llama-server placeholder, which had been naming the
Velopack pack folder. Reviewing the *value* it should name instead turned up a
better question: what does `IAppPaths.LlamaServerDirectory` actually point at?

Nothing, as it turns out. There were two llama directories one letter apart:

| Path | Written by |
|---|---|
| `…\parlotype-data\llama-server**s**\<installId>\` | Every managed install. `LlamaServerInstaller` stages, extracts and atomically renames here; `<installId>` is e.g. `b9198-win-vulkan-x64` |
| `…\parlotype-data\llama-server\` | **Nothing.** No code path creates or populates it |

`LlamaServerDirectory` (singular) is a survivor of the pre-ADR-026 manual-only era.
Managed installs replaced it, and it was left behind holding two jobs it was no
longer suited for:

1. **The default value of the manual folder box.** `InitializeAsync` pre-filled the
   box with it, so a first-time user saw an authoritative-looking path to an empty
   directory. Selecting *Use this* got them a file-not-found at the next recording.
2. **The last-resort fallback in `GetServerPathAsync`.** With no managed install and
   no manual folder, the app reported `llama-server not found at
   '…\parlotype-data\llama-server\llama-server.exe'` — naming a path the user had
   never chosen, for a folder that has never existed on any machine.

Both are the same failure: presenting "unconfigured" as "configured, but broken".

## Decision

**`IAppPaths.LlamaServerDirectory` is removed.** `LlamaServerInstallsDirectory` is
now the only llama-server location Parlotype writes; a manual build lives wherever
the user put it, recorded in `SettingsKeys.LlamaCppServerFolder`.

### The manual folder box starts empty

`ServerFolder` initialises to `""` when the setting is unset, and *Reset* clears it
rather than restoring a path. An empty box with a placeholder states the truth —
you have not chosen a manual build — which is the normal condition for everyone on
a managed install.

`PlaceholderText` becomes `{loc:Tr Settings_LlamaCpp_ManualFolderPlaceholder}` —
instructional copy rather than a path. This supersedes ADR-065's "derive the hint
from `IAppPaths`" in the narrower sense that there is no longer a path to derive;
the principle it was defending — no hard-coded data path in the view — is better
served by the view containing no path at all. It also makes the hint translatable,
which a derived absolute path could never have been (ADR-064).

### `GetServerPathAsync` returns `string?`

`null` means "nothing configured". `InitializeAsync` turns that into its own
message — *"No llama-server is configured. Install one from Settings → Speech
engine → llama.cpp, or point the manual folder at a build you downloaded
yourself."* — distinct from the existing "found a path, no file there" message. A
resolver that cannot answer should say so rather than inventing a plausible answer
for the caller to trip over.

### Two consequences of empty being the default state

Both are cases where "empty" was previously unreachable and is now the norm:

- **Save no longer rejects an empty folder.** It used to error with "Server folder
  cannot be empty", which after this change would block *every* managed-install
  user from saving a port change. Empty now saves as empty, meaning "no manual
  build".
- **`SetActiveManualAsync` refuses when no folder is saved**, with an inline
  message, instead of activating a manual install the recognizer cannot resolve.
  It checks the *saved* `ManualFolderPath`, not the text box: switching on unsaved
  text would activate a path the recognizer cannot see.

### No migration

A stored `LlamaCppServerFolder` — including one that happens to equal the old
default — is read and honoured exactly as before. This ADR removes a *default*, not
a setting. Users who deliberately placed a build in `…\parlotype-data\llama-server`
keep working; ADR-065's pack-folder warning is unaffected and still fires for the
paths that deserve it.

## Consequences

**Easier**

- One llama-server path in the interface instead of two names differing by an `s`.
- The unconfigured state is legible in the UI and in the error message, instead of
  being disguised as a broken configuration.
- `AppPathsTests`, `MockAppPaths` and `VelopackPackFolderTests` each lost a case
  covering a directory that never existed.

**Harder / accepted**

- `IAppPaths` is a public Core interface; removing a member is a breaking change
  for anything outside this repo that implements it. Nothing does.
- The manual box no longer suggests *where* to put a build. That was never advice
  Parlotype was entitled to give about a folder it does not manage — and when it
  did give it, it pointed at the folder the uninstaller wipes (ADR-065).
- A benchmark config with neither `serverFolder` nor a managed install now fails
  with an explicit message rather than a path-not-found. Documented in
  `src/Parlotype.Benchmark/README.md`.
