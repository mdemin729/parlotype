---
title: Curated release notes — CHANGELOG.md, CI notes gate, /release-notes skill
status: completed
created: 2026-08-02
started: 2026-08-02
completed: 2026-08-02
---

## Problem

The GitHub Release body for `v0.4.0` is empty:

```bash
gh release view v0.4.0 --json body   # => {"body": ""}
```

`vpk upload github` (`.github/workflows/release.yml`) creates the release with a
title and assets but never passes notes. With an empty body GitHub falls back to
rendering the tagged commit's message — and because releases are cut from
squash-merged PRs, that message is the full multi-section engineering commit body
(`IAppPaths`, `VelopackApp.Build().Run()`, DI wiring, test counts). That is a
changelog for contributors, not for someone deciding whether to install an update.

There is no release-notes step in the pipeline at all: no `CHANGELOG.md`, no
`.github/release.yml`, no notes tooling, and `docs/RELEASING.md` never mentions
writing notes.

## Approach

Split the work between an AI half that is local and reviewable, and a CI half
that is deterministic text extraction:

```
/release-notes → CHANGELOG.md entry → PR → human review → merge → git tag vX.Y.Z
                                                                       ↓
                             release.yml: gate → build → vpk upload → gh release edit
```

Decisions taken with the user before implementation:

| Question | Decision |
|---|---|
| Source of truth | `CHANGELOG.md` at repo root; CI extracts the section matching the tag |
| Where the agent runs | Local Claude Code skill — no `ANTHROPIC_API_KEY` in CI |
| Automation depth | Agent drafts + opens a PR, then stops. Tagging stays human |
| In-app "What's new" | Out of scope |

`gh release edit --notes-file` after `vpk upload`, rather than a
`vpk upload --releaseNotes` flag: the Velopack 1.2.0 CLI docs for `upload github`
are unreachable so the flag cannot be confirmed, `gh` is preinstalled on
GitHub-hosted runners, `permissions: contents: write` is already granted, and
overwriting the body afterwards is naturally idempotent under `--merge true` when
the macOS/Linux matrix rows are uncommented.

## Workplan

- [x] `CHANGELOG.md` at repo root, Keep a Changelog structure, backfilled for
      `0.1.0`–`0.4.0` from git history + the ADRs those commits reference
- [x] `.github/workflows/release.yml`: notes gate immediately after
      *Resolve version and mode* (fails in seconds, not after a 20-minute
      publish) + *Set release notes* after *Upload to GitHub Releases*
- [x] `.claude/skills/release-notes/SKILL.md` — the drafting procedure and its
      writing rules, ending at "open a PR and stop"
- [x] `docs/RELEASING.md`: *Writing release notes* as step 1 of Cutting a release
- [x] `README.md`: link the changelog from Download / Releases
- [x] ADR-054 + `memory/decisions/_index.md` row + skill listed in
      `memory/knowledge/agent-skills.md` +
      `memory/knowledge/github-release-empty-body-fallback.md`
- [x] Verify: gate script lifted verbatim out of the YAML and run against the
      real `CHANGELOG.md` — all four paths (tag/version present, tag/version
      missing, dry run, dry run with the `[Unreleased]` heading removed);
      `dotnet build` zero warnings, `dotnet test` 1064 passing
- [ ] **Deferred, needs the user:** backfill the four published release bodies
      with `gh release edit`. Left undone because it edits public pages; the
      changelog sections they would use are written and verified.
- [ ] **Not verifiable locally:** the dry-run job on the PR, and the first real
      tag. `release-notes.md` is gitignored.

## Notes

The extractor originally used a regex (`$0 ~ "^## \\[" esc "\\]"`). It silently
matched nothing when tested locally — a shell layer had halved the backslashes,
turning `\[` into a bracket expression. That failure mode produces an empty
extraction rather than an error, which in CI would mean a release published with
no notes for exactly the reason this plan exists. Rewritten with
`substr`/`index` string comparison: no backslashes anywhere, so nothing to eat.
