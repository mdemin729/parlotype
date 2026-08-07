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
3. **Write the release notes and merge them before tagging** — see
   [Writing release notes](#writing-release-notes) below. The tag build fails in
   its first minute if `CHANGELOG.md` has no section for the version.
4. Tag and push:

```bash
git tag v1.2.0 && git push origin v1.2.0
```

The `Release` workflow then, per matrix row:

publish → `vpk download` (existing feed, for deltas) → `vpk pack` → `vpk upload github`

A tag containing `-` (e.g. `v1.2.0-beta.1`) is published as a GitHub
**pre-release**. The updater requests stable releases only, so pre-releases are
not offered to users automatically.

### Required secrets

Tag builds sign the output with **Azure Artifact Signing**
([ADR-058](decisions/058-installer-code-signing.md)). Dry runs and pull requests
do not, and still pass with no secrets at all — the signing steps are gated on
`DRY_RUN == 'false'`.

| Kind | Name | Value |
|---|---|---|
| Secret | `AZURE_CLIENT_ID` | App registration (client) id |
| Secret | `AZURE_TENANT_ID` | Directory (tenant) id |
| Secret | `AZURE_SUBSCRIPTION_ID` | Subscription holding the signing account |
| Variable | `SIGN_ENDPOINT` | Region URI, e.g. `https://weu.codesigning.azure.net` |
| Variable | `SIGN_ACCOUNT` | Artifact Signing account name |
| Variable | `SIGN_PROFILE` | Certificate profile name |

No client secret is stored. The workflow authenticates by OIDC, and the app
registration carries a federated credential with subject
`repo:mdemin729/parlotype:environment:release` plus the **Artifact Signing
Certificate Profile Signer** role on the signing account.

> The GitHub environment named `release` must have **no** deployment branch or tag
> policy. One would block the PR and dispatch dry runs, which declare the same
> environment but never sign.

**`SIGN_ENDPOINT` must name the region the signing account *and* the certificate
profile live in.** A mismatch fails as a bare 403 from `SignerSign()` that says
nothing about regions — it is the single most common setup error.

Signing runs inside `vpk` (via `VPK_AZURE_TRUSTED_SIGN_FILE`), never over
`Releases/` afterwards: `Update.exe` is signed before it is embedded in the
`.nupkg` and `Setup.exe` after its payload is appended, so a post-hoc `signtool`
or `azure/artifact-signing-action` pass can break the installer. Everything in the
package is signed, not just `Setup.exe` — see ADR-058 for why.

If a release must ship unsigned, remove the `Enable code signing` step; the `Pack`
step needs no edit, because it never mentions signing.

### Dry runs

Either:

- **Actions → Release → Run workflow**, optionally with a version. It packs,
  validates the feed, and uploads the result as a build artifact — no GitHub
  release, no upload to the feed.
- Open a PR touching `src/Parlotype.Desktop/**`, `src/Parlotype.Core/**`,
  `src/Parlotype.Platform/**`, or `Directory.Build.*`. The same dry-run path runs
  automatically.

A dry run cannot check the version section — there is no tag yet — so it only
verifies that `CHANGELOG.md` still has its `## [Unreleased]` heading, which is
what the extractor keys off.

**Dry runs pack `9999.0.0-dryrun` by default, and the absurd number is load-bearing.**
The delta step downloads the live release feed into `Releases/`, and `vpk pack`
refuses to build a version equal to or lower than anything already sitting there.
A plausible-looking `0.0.1-dryrun` worked only while no Velopack release existed;
it fails against a real feed with:

```
There is a release in channel win which is equal or greater to the current
version 0.0.1-dryrun. Please increase the current package version or remove
that release.
```

If you dispatch a dry run with an explicit version, pick one above the latest
published release for the same reason. The upside of packing against a real feed
is that dry runs now build a genuine delta, so `Verify pack output` reporting
"Delta package built" is real evidence that delta generation still works.

---

## Writing release notes

[`CHANGELOG.md`](../CHANGELOG.md) is the source of truth for what a release says
([ADR-054](decisions/054-curated-release-notes.md)). The workflow copies the
section matching the tag into the GitHub Release body — nothing else does. Left
empty, `vpk` publishes a release with no body, and GitHub falls back to rendering
the tagged commit's message, which for a squash-merged PR is the entire
engineering commit body. That is what `v0.4.0` shipped with.

Draft the entry with the `/release-notes` skill
([`.claude/skills/release-notes/SKILL.md`](../.claude/skills/release-notes/SKILL.md)).
It reads the commits since the last tag plus every ADR they reference, sorts
user-facing changes from internal churn, writes the `## [X.Y.Z]` section, and
opens a PR. It stops there by design: **it never tags and never edits a
published release.**

Review the wording yourself before merging — the notes are the most-read part of
a release, and the agent cannot know which changes you consider headline
material. Then merge, then tag.

### When the tag build fails on the notes

```
Error: CHANGELOG.md has no non-empty '## [1.2.0]' section.
Run /release-notes and merge that PR before tagging.
```

The gate runs before restore, so this costs seconds rather than a full publish,
and nothing was uploaded — no release exists yet. Add the section on `master`,
delete the tag locally and on the remote, then re-tag: the tag has to point at a
commit that already contains the notes.

To check extraction before pushing a tag, run the same awk the gate runs — it is
the `Extract release notes` step in `.github/workflows/release.yml`. It must
print your section and stop at the next `## ` heading.

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
