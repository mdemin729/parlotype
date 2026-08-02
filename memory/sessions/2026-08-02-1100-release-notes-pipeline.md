---
title: "Session: 2026-08-02 — Curated release notes pipeline"
type: session
status: active
tags: [release, changelog, ci, github, velopack, skills, adr-054]
created: 2026-08-02
summary: "Releases shipped with an empty body; GitHub was rendering the squash-merge commit message. Added CHANGELOG.md as source of truth, a CI extract-and-apply gate, and a local /release-notes drafting skill (ADR-054)."
---

# Session: 2026-08-02 — Curated release notes pipeline

## Active Focus

Fixing what the `v0.4.0` release page shows. Reported as "release notes contain
full commit messages"; the actual state was that the release had **no body at
all** and GitHub was falling back to the tagged commit's message.

Files: `CHANGELOG.md` (new), `.github/workflows/release.yml`,
`.claude/skills/release-notes/SKILL.md` (new), `docs/RELEASING.md`, `README.md`,
`.gitignore`, `docs/decisions/054-curated-release-notes.md` (new).

## Decisions Made

- **`CHANGELOG.md` is the source of truth**, not a generator running at tag time.
  Notes are reviewable in a PR before the tag exists, and the published body is
  exactly the reviewed text. Backfilled `0.1.0`–`0.4.0`.
- **The AI half runs locally, the CI half is deterministic.** A `/release-notes`
  skill drafts and opens a PR; CI does string extraction and
  `gh release edit --notes-file`. No `ANTHROPIC_API_KEY` in a repo that
  otherwise needs no secrets, and no unreviewed wording reaching the public.
- **The agent stops at the PR.** No tagging, no touching published releases —
  the tag is the publish trigger.
- **`gh release edit` after `vpk upload`**, not a `vpk --releaseNotes` flag:
  the flag is unconfirmable (Velopack CLI docs 404), and writing after upload is
  idempotent under `--merge true` for future matrix rows.
- **The gate runs before `Restore`** so a missing section costs seconds, not a
  20-minute publish.
- In-app "What's new" on Settings → Updates was considered and left out of scope.

## Facts Learned

- **An empty GitHub Release body renders the tagged commit's message.** This is
  why four releases looked like they had verbose commit-dump notes while
  `gh release view --json body` returned `""`. Distilled to
  `memory/knowledge/github-release-empty-body-fallback.md`.
- `vpk upload github` sets `--releaseName` and the assets, and no body.
- **Backslash-bearing awk regexes are not portable through the tool/shell/YAML
  stack.** `"^## \\[" ver "\\]"` silently became a bracket expression locally and
  matched nothing — no error, just an empty extraction. The extractor now uses
  `substr`/`index` only. Worth remembering as a class of bug: in CI this would
  have published a release with no notes, i.e. the exact failure being fixed.
- Relative Markdown links do not resolve inside a GitHub Release body, so links
  inside a `CHANGELOG.md` version section have to be absolute.
- Concrete release-size history for the notes: `v0.3.0` shipped two zips
  (385 MB full / 253 MB lite); `v0.4.0` ships one 82 MB `Setup.exe`.

## Open Blockers

None blocking. Two things cannot be verified from here:

- The dry-run job on the PR (needs CI).
- The first real tag — the only end-to-end proof that the body lands.

## Documentation Status

- ADR: done — `docs/decisions/054-curated-release-notes.md`
- Vault (services/architecture): done — `memory/decisions/_index.md` row;
  `memory/knowledge/agent-skills.md` notes the new skill. No service or
  subsystem profile applies (no C# changed).
- Knowledge (non-derivable facts): done —
  `memory/knowledge/github-release-empty-body-fallback.md`

## Next Action

Ask the user before running the backfill: `gh release edit v0.4.0 … v0.1.0` with
the sections now in `CHANGELOG.md`. It edits public pages, so it is gated on an
explicit yes; `v0.4.0` alone is enough if the older ones are not wanted.

After that, the next release is the real test — tag, then confirm
`gh release view <tag> --json body --jq .body` returns the changelog section.
