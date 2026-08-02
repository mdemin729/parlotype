---
status: accepted
date: 2026-08-02
---

# 054. Curated Release Notes from CHANGELOG.md

## Context

The GitHub Release body for `v0.4.0` is empty:

```bash
gh release view v0.4.0 --json body   # => {"body": ""}
```

`vpk upload github` ([ADR-053](053-velopack-packaging-and-auto-update.md)) creates the
release with a title and assets, and nothing in `.github/workflows/release.yml` ever passes
notes. With an empty body GitHub falls back to rendering the tagged commit's message, and
because releases are cut from squash-merged PRs that message is the whole engineering
commit body — `IAppPaths`, `VelopackApp.Build().Run()`, DI wiring, per-phase test counts.
Accurate, and useless to someone deciding whether to install the update.

Nothing else filled the gap. There was no `CHANGELOG.md`, no `.github/release.yml`
auto-generation config, no release-drafter, and `docs/RELEASING.md` went straight from
"master is green" to `git tag`.

Three properties were wanted that pull against each other:

- **Quality.** Good notes require judgement — which of seventeen commits is a headline,
  which is invisible, what an ADR's Context section means for a user. That is model work,
  not a template.
- **Reviewability.** Notes are the most-read artefact of a release and are effectively
  permanent once published. They should be seen by a human before they are public.
- **Determinism at publish time.** The step that writes the release body should not be
  able to produce different text on a re-run.

GitHub's own `generate_release_notes` was considered and rejected: it emits a
"What's Changed" list of PR titles, which for this repo means `Velopack packaging,
auto-update, and app data management (ADR-053) (#11)` — the same contributor-facing
register, just shorter.

## Decision

Split the work at the reviewability boundary.

**`CHANGELOG.md` at the repo root is the source of truth.** Keep a Changelog structure,
user-facing wording, `Highlights` capped at three, an `⚠️ Action required` section when
one applies, and internal churn confined to a collapsed `Under the hood` block that links
the ADRs. Backfilled for `0.1.0`–`0.4.0`. Links inside a version section are absolute
URLs, because the section is copied verbatim into a release body where relative paths do
not resolve.

**CI extracts and applies it, with no model involved.** `.github/workflows/release.yml`
gains two steps:

- `Extract release notes`, immediately after version resolution and *before* restore, so a
  missing section fails in seconds rather than after a 20-minute publish. On a tag push it
  writes `release-notes.md` and fails with an actionable `::error::` if the section is
  absent or blank; on a dry run, where no version section can exist yet, it only checks
  that the `## [Unreleased]` heading the extractor keys off is still present.
- `Set release notes`, after `Upload to GitHub Releases`, running
  `gh release edit --notes-file release-notes.md`.

**Drafting is a local Claude Code skill**, `.claude/skills/release-notes/SKILL.md`,
invoked as `/release-notes`. It reads `git log` bodies over the tag range plus every ADR
those commits reference, classifies user-facing versus internal, writes the section under
explicit wording rules, and opens a PR. It is forbidden from tagging, pushing a tag, or
touching a published release.

Rejected alternatives:

- **`anthropics/claude-code-action` in CI on tag push.** Fully hands-off, but requires an
  `ANTHROPIC_API_KEY` secret in a repo that today needs none beyond the built-in
  `GITHUB_TOKEN`, and publishes wording no human has read.
- **A `--releaseNotes` flag on `vpk upload github`.** The Velopack 1.2.0 CLI reference for
  that command is not reachable, so the flag cannot be confirmed. `gh` is preinstalled on
  GitHub-hosted runners, `permissions: contents: write` is already granted, and writing the
  body after upload is independent of the Velopack version.
- **Generating notes at tag time without a repo file.** Nothing to review before the tag
  exists, and no changelog history.

The extractor uses plain `substr`/`index` string comparison rather than a regex. Versions
contain dots and the headings contain brackets; escaping those survives neither YAML nor
every `awk` implementation intact, which was demonstrated during implementation.

## Consequences

- **Tagging now has a precondition.** A tag pushed without a merged changelog section fails
  the build in its first minute. Nothing is uploaded and no release is created, but the tag
  has to be deleted and re-pushed at a commit that contains the notes. `docs/RELEASING.md`
  documents the failure and the recovery.
- **Release bodies are reviewed before they are public**, and the published body is exactly
  the text that was reviewed.
- **`gh release edit` overwrites whatever `vpk` wrote.** Idempotent under `--merge true`, so
  uncommenting the macOS/Linux matrix rows needs no change here — every row writes the same
  body.
- **The changelog is a maintained file.** It has to be kept accurate; a wrong entry is now
  a published wrong entry. The skill's guardrail against claiming anything not in the diff
  exists for this reason.
- **`CHANGELOG.md` is reusable.** The same file can later feed the website, or an in-app
  "What's new" panel on Settings → Updates. Neither is built here.
- Only the release workflow depends on the file's shape: the `## [X.Y.Z]` heading form and
  the `## [Unreleased]` heading. Everything else about the format is editorial.
