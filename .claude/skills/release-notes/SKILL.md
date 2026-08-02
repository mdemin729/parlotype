---
name: release-notes
description: Use when preparing a Parlotype release — drafting the CHANGELOG.md entry for a new version from git history and the ADRs it references, and opening the review PR. Covers what counts as user-facing, the wording rules, and where to stop.
---

# Release Notes Skill

Produces the `CHANGELOG.md` entry for a version and opens a PR for review.
**It never tags and never publishes.**

`CHANGELOG.md` is the source of truth: `.github/workflows/release.yml` copies the
section matching the tag into the GitHub Release body ([ADR-054](../../../docs/decisions/054-curated-release-notes.md)).
If the section is missing or empty when the tag lands, the release build fails
before it starts.

## Before You Start

- Read `docs/RELEASING.md` — you are producing step 1 of *Cutting a release*.
- Read the last two entries in `CHANGELOG.md` for voice and level of detail.
- The user picks the version number. Ask if it was not given; do not infer it
  from the changes.

## 1. Establish the range

```bash
git describe --tags --abbrev=0        # previous released tag
git log --oneline <prev>..HEAD
```

Everything in that range is a candidate. Nothing outside it is.

## 2. Gather evidence

Commit subjects alone are not enough. About a fifth of them are squash-merged PR
titles with no conventional-commit prefix, and those are usually the *most*
significant changes in the release — `Velopack packaging, auto-update, and app
data management (ADR-053) (#11)` and `Drop CUDA for Vulkan-only Whisper (#7)`
both look like noise next to a tidy `fix(desktop):` line.

```bash
git log --format='===== %s%n%b' <prev>..HEAD    # full bodies, where the detail is
git diff --stat <prev>..HEAD -- src/            # what actually moved
```

Then **read every ADR referenced in the range**. Nearly every substantive commit
here cites one (`(ADR-047)`, `(ADR-044/045/046)`), and an ADR's `## Context` and
`## Consequences` sections are already written as "why this matters" prose — that
is the raw material for a user-facing bullet. `## Context` in particular usually
states the user's problem in the user's terms.

Squash commits also carry `(#N)`. Pull the PR if the commit body is thin.

## 3. Classify

**User-facing** — anything a person running Parlotype could notice:

- A new capability, engine, setting, or UI surface
- Changed defaults or behaviour, including anything that overrides a choice they
  already made
- A fixed symptom they could have hit
- Performance, memory, download size or accuracy they can feel or see
- Anything requiring them to act
- Privacy and security posture (what is logged, what is verified, what leaves
  the machine)

**Internal** — real work, but invisible from outside: tests, `memory/` vault,
`plans/`, refactors, benchmark harness, CI, docs, dependency bumps with no
behavioural effect.

Every user-facing change gets a bullet. Internal work goes in the collapsed
**Under the hood** block or nowhere.

## 4. Write

### Structure

```markdown
## [X.Y.Z] — YYYY-MM-DD

### Highlights                                  (max 3)
### ⚠️ Action required if …                     (only when it applies)
### Added
### Changed
### Fixed

<details>
<summary>Under the hood</summary>
</details>
```

Drop any section with nothing in it. Target: the whole entry readable in about
30 seconds, with the Highlights readable in five.

### Rules

- **Every bullet answers "what changed for me?"**, not "what did we implement".
- **Highlights are what the user can now do**, one sentence each, no jargon.
- **Never name a type, file, interface or namespace** in a bullet. `IAppPaths`,
  `VelopackApp.Build().Run()`, `HotkeyGestureMatcher`, "registered in DI",
  "introduced", "refactored", "wired up", test counts — all out. Those belong in
  the ADR, which the *Under the hood* block links to.
- **Name features by their UI label**: "Settings → Speech engine → Whisper
  runtime", "hold Right Ctrl", "the Cloud badge" — not the class behind them.
- **Keep the numbers the user can see**: `253–385 MB → 82 MB`, `~3 GB of RAM per
  failed attempt`, `~10× less memory while recording`. Drop numbers they cannot:
  publish-output byte counts, WER deltas, allocation-per-callback figures.
- **Say what it means, not just what happened.** "The CUDA option is gone" is a
  fact; "the CUDA option is gone — it only ever worked if you separately
  installed the ~3 GB NVIDIA toolkit, and NVIDIA cards are still accelerated
  through Vulkan" is a release note.
- **Anything requiring user action goes in its own section at the top**, with
  the exact command or click path. Migrations, removed settings that get
  rewritten, retired defaults.
- **Retired defaults are user-facing even when the code change is small.** If a
  default hotkey, engine or model changes, say what it was, what it is, and what
  happens to people who had customised it.
- **Never claim something not in the diff.** If a bullet cannot be traced to a
  commit or ADR in the range, cut it.

### Worked example

The `v0.4.0` commit body reads:

> Integrate Velopack 1.2.0 as the installer and auto-update framework […]
> IAppPaths (Core): single source of truth for all user data paths, rooted
> outside the Velopack pack folder to survive uninstall […] VelopackApp.Build().Run()
> in Program.Main as the first statement, before Avalonia/DI/logging

That is accurate and useless to a user. What it means to them:

> **Parlotype installs and updates itself.** A real installer replaces the
> hand-unzipped folder, and Parlotype now checks for new versions in the
> background and updates in place — no more downloading a fresh zip every time.

And the consequence the commit buries, which is the single most important line
in the whole release for an existing user:

> Parlotype's data folder moved from `%LOCALAPPDATA%\parlotype` to
> `%LOCALAPPDATA%\parlotype-data`, because the installer now owns the old
> location and wipes it on uninstall. **There is no automatic migration.**

Same for ADR-049: not "RuntimePreference is now Auto/Vulkan/Cpu and
WhisperRuntimeBootstrap loses the CUDA branch", but "GPU acceleration for Whisper
is Vulkan-only — NVIDIA cards are still accelerated, and if you had selected CUDA
you are moved to Auto".

## 5. Update CHANGELOG.md

Promote anything under `## [Unreleased]` into a new `## [X.Y.Z] — YYYY-MM-DD`
section directly below it, then add the release's own content. Leave
`## [Unreleased]` in place and empty.

Add the version's link-reference definition at the bottom of the file and repoint
`[Unreleased]` at the new tag.

**Links inside a version section must be absolute URLs**
(`https://github.com/mdemin729/parlotype/blob/master/…`). The section is copied
verbatim into the GitHub Release body, where relative paths do not resolve.

Then confirm the workflow can find it, using the same extraction the CI gate runs:

```bash
awk -v ver=X.Y.Z 'BEGIN { want = "## [" ver "]"; n = length(want) } !inside && substr($0,1,n) == want { inside = 1; next } inside && substr($0,1,3) == "## " { exit } inside && substr($0,1,1) == "[" && index($0, "]: http") { exit } inside { print }' CHANGELOG.md
```

It must print the section and stop at the next heading.

## 6. Open the PR, then stop

```bash
git switch -c release-notes/vX.Y.Z
git commit -m "docs(changelog): notes for vX.Y.Z"
gh pr create --title "Release notes for vX.Y.Z" --body "…"
```

Report the PR URL and stop there.

## Guardrails

- **Never `git tag`, never push a tag, never `gh release create` or
  `gh release edit`.** The tag is the publish trigger and is the user's to pull;
  CI applies the notes on its own once the tag lands.
- Editing an *already published* release body is a separate, outward-facing
  action — ask first, every time.
- If the range contains no user-facing change at all, say so rather than padding
  the entry. A release can legitimately be "maintenance only".
