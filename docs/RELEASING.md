# Releasing Parlotype

Parlotype is packaged and updated with [Velopack](https://velopack.io) 1.2.0
([ADR-053](decisions/053-velopack-packaging-and-auto-update.md)).

- **Pack id:** `Parlotype` — permanent. Velopack derives the install root
  `%LOCALAPPDATA%\Parlotype` from it; changing it orphans every existing install.
- **Install root:** `%LOCALAPPDATA%\Parlotype` — owned by Velopack. Never write
  anything there.
- **User data:** `%LOCALAPPDATA%\parlotype-data` — survives updates and uninstall.
- **Feed:** GitHub Releases on `https://github.com/mdemin729/parlotype`.

---

## Cutting a release

1. Make sure `master` is green: `dotnet build Parlotype.slnx` (zero warnings) and
   `dotnet test`.
2. Pick a **SemVer 2** version. `MAJOR.MINOR.PATCH` with an optional
   `-prerelease` suffix. Velopack does not handle four-part versions
   (`1.2.3.4`) well, and the workflow rejects them.
3. Tag and push:

```bash
git tag v1.2.0 && git push origin v1.2.0
```

The `Release` workflow then, per matrix row:

publish → `vpk download` (existing feed, for deltas) → `vpk pack` → `vpk upload github`

A tag containing `-` (e.g. `v1.2.0-beta.1`) is published as a GitHub
**pre-release**. The updater requests stable releases only, so pre-releases are
not offered to users automatically.

### Required secrets

**None beyond the built-in `GITHUB_TOKEN`.** Code signing is not wired up yet, so
there are no certificates to configure and CI passes on a fork or a PR with no
secrets at all. Unsigned builds trigger a Windows SmartScreen warning on first
run; that is expected until signing lands.

### Dry runs

Either:

- **Actions → Release → Run workflow**, optionally with a version. It packs,
  validates the feed, and uploads the result as a build artifact — no GitHub
  release, no upload to the feed.
- Open a PR touching `src/Parlotype.Desktop/**`, `src/Parlotype.Core/**`,
  `src/Parlotype.Platform/**`, or `Directory.Build.*`. The same dry-run path runs
  automatically.

---

## Testing a real install locally

> **Update operations only work in an installed build.** Run from `dotnet run` or
> the IDE and `IUpdateService` reports `UpdateState.NotInstalled` and no-ops by
> design — it does not throw and does not show an error. The Settings → Updates
> page says so and disables its controls. To exercise the update path you must
> install via `Setup.exe`.

```bash
dotnet tool install -g vpk --version 1.2.0
```

```bash
dotnet publish src/Parlotype.Desktop -c Release -r win-x64 --self-contained true -p:Version=0.1.0 -o publish
```

```bash
vpk pack --packId Parlotype --packTitle Parlotype --packAuthors "Maksim Demin" --packVersion 0.1.0 --packDir publish --mainExe Parlotype.exe --outputDir Releases --icon src/Parlotype.Desktop/Assets/parlotype.ico
```

This produces, in `Releases/`:

| File | What it is |
|---|---|
| `Parlotype-win-Setup.exe` | The installer. Per-user, no UAC prompt. |
| `Parlotype-win-Portable.zip` | Portable build. Cannot self-update. |
| `Parlotype-0.1.0-full.nupkg` | Full package, what the feed serves. |
| `releases.win.json` | The feed itself. Installed clients poll this. |

Run `Parlotype-win-Setup.exe`. It installs to `%LOCALAPPDATA%\Parlotype` with no
elevation prompt.

### Verifying an actual update

To confirm end-to-end updating, you need two versions and a feed the installed
app can reach. Locally, point a second pack at the same `--outputDir` and serve
the folder over HTTP, or push two real tags to a test repository.

```bash
dotnet publish src/Parlotype.Desktop -c Release -r win-x64 --self-contained true -p:Version=0.1.1 -o publish
```

```bash
vpk pack --packId Parlotype --packTitle Parlotype --packAuthors "Maksim Demin" --packVersion 0.1.1 --packDir publish --mainExe Parlotype.exe --outputDir Releases --icon src/Parlotype.Desktop/Assets/parlotype.ico
```

Because `Releases/` already holds 0.1.0, this run also writes
`Parlotype-0.1.1-delta.nupkg`. **If no `-delta.nupkg` appears, delta generation
is broken** — in CI that means the `vpk download` step failed and every user is
getting a full ~80 MB download instead of a small patch. The step is
`continue-on-error` precisely because it must fail on the first release, so check
its log rather than assuming a green run means deltas worked.

### Checking the hooks

Install and update hooks run in a short-lived process with no UI, so they are
invisible unless you look. They log to:

```
%LOCALAPPDATA%\parlotype-data\logs\velopack.log
```

Velopack's own installer log is separate and lives in the pack folder.

---

## Native libraries in an installed build

The installed layout differs from the build output, and native library resolution
is the usual failure point. After installing, verify against a real install (not
the IDE):

- Global hotkeys register (SharpHook).
- Microphone capture works (NAudio/WASAPI).
- Transcription completes on the default Parakeet engine (sherpa-onnx natives).
- Switching to Whisper loads and transcribes (whisper.cpp + Vulkan natives).

`Directory.Build.targets` strips non-target-RID natives and PDBs from publish
output (ADR-051, ADR-052). If a native fails to load only in the installed build,
suspect those filters first.

---

## Rolling back a bad release

Velopack serves whatever the feed advertises, so rolling back means changing what
the feed says — deleting the installer from the GitHub release is not enough.

1. **Stop the bleed.** On the GitHub release for the bad version, either delete
   the release or mark it as a pre-release. The updater only considers stable
   releases, so marking it pre-release removes it from the feed for everyone who
   has not yet updated.
2. **Delete the bad assets**, including `releases.win.json`, from that release.
   The feed asset is what clients read; while it advertises the bad version they
   will keep downloading it.
3. **Ship forward.** The cleanest fix is a new, higher version — `v1.2.1`
   reverting the change. Velopack will not move a client *backwards* by default
   (`AllowVersionDowngrade` is off), so re-uploading an older version does not
   recall clients that already updated.
4. **Users already on the bad version** can reinstall from the previous release's
   `Setup.exe`. Their data in `%LOCALAPPDATA%\parlotype-data` is untouched by
   this.

Never delete and re-push the same tag with different content: clients that
already downloaded it have the old package staged, and the SHA in the feed will
no longer match what they hold.

---

## Uninstalling, and the data directory

Uninstalling removes `%LOCALAPPDATA%\Parlotype` **entirely** — that is Velopack's
behaviour, and it is why nothing user-owned may live there.

`%LOCALAPPDATA%\parlotype-data` — the model cache, settings, and DPAPI-encrypted
API keys — is **kept by default**. Velopack's hook may not show UI, so it cannot
ask at uninstall time; instead the user opts in ahead of time at
**Settings → Application → Data** ("Delete everything when I uninstall
Parlotype"), and the hook executes that recorded choice.

| `UninstallRemovesUserData` | What uninstall does |
|---|---|
| unset / `false` (default) | Logs the location and size, deletes nothing |
| `true` | Deletes `%LOCALAPPDATA%\parlotype-data` entirely |

Anything ambiguous — missing settings.json, corrupt JSON, absent key — is treated
as `false`. Data is only removed on an unambiguous opt-in, and a failure mid-delete
leaves files behind rather than blocking the uninstall. Either way, check
`velopack.log` (see [Checking the hooks](#checking-the-hooks)) to see which branch
ran. To remove leftovers, delete the folder by hand.

Windows only: macOS and Linux have no uninstall hooks, so the toggle has no effect
there.

### Migrating from a pre-ADR-053 install

Versions before this change kept data in `%LOCALAPPDATA%\parlotype`. That folder
is the *same directory* Velopack now installs into on Windows, which is why the
data root moved. **No automatic migration ships.** Move it once, by hand, with
Parlotype closed:

```bash
mv "$LOCALAPPDATA/parlotype" "$LOCALAPPDATA/parlotype-data"
```

Do this **before** installing via `Setup.exe`. If you install first, the
installer will have written into the old folder, and the safe move is instead to
copy `models/`, `settings.json`, `secrets.json`, `window-state.json` and
`prompts.json` across individually. Skipping the migration entirely is also fine
— Parlotype starts with default settings and re-downloads models on demand.
