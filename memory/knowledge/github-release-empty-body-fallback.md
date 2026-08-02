---
title: An Empty GitHub Release Body Renders the Tagged Commit Message
type: knowledge
status: active
tags: [github, releases, velopack, vpk, ci, release-notes]
created: 2026-08-02
last_updated: 2026-08-02
summary: A GitHub Release with an empty body displays the tagged commit's full message instead of nothing — so a release can look like it has notes while its body is literally "".
---

# An Empty GitHub Release Body Renders the Tagged Commit Message

## The fact

A GitHub Release whose body is the empty string does **not** render as an empty
section. GitHub falls back to displaying the message of the commit the tag points
at. For a lightweight tag on a squash-merged PR, that is the entire multi-section
engineering commit body.

This is why `v0.4.0` appeared to have verbose, commit-dump release notes while the
API reported no body at all:

```bash
gh release view v0.4.0 --json body   # => {"body": ""}
```

The consequence for diagnosis: **do not judge whether a release has notes by
looking at the release page.** Ask the API. The page cannot distinguish "notes
that happen to read like a commit message" from "no notes at all", and the fix is
completely different in each case.

## Why it mattered here

`vpk upload github` ([[053-velopack-packaging-and-auto-update]]) creates the
release with `--releaseName` and the assets, and sets no body. Nothing else in
`.github/workflows/release.yml` did either. The fallback made this invisible for
four releases.

`vpk upload github`'s CLI reference page is not reachable in the Velopack docs
(404 at the documented URL), so whether it accepts a `--releaseNotes` flag could
not be confirmed. `gh release edit --notes-file` run *after* the upload sidesteps
the question entirely, is preinstalled on GitHub-hosted runners, needs only the
`contents: write` permission the workflow already has, and is idempotent when
several matrix rows attach to the same release with `--merge true`.
See [[054-curated-release-notes]].

## Related

- Relative Markdown links do not resolve inside a release body. Anything copied
  from `CHANGELOG.md` into a release must use absolute URLs.
- `awk` regexes that need `\[` or `\.` do not survive every shell/YAML layer
  intact — the changelog extractor uses `substr`/`index` string comparison
  instead. The failure is silent: the pattern still compiles, it just never
  matches, and you get an empty extraction rather than an error.

## See also

- `.claude/skills/release-notes/SKILL.md`
- `docs/RELEASING.md` → "Writing release notes"
- [[velopack-pack-folder-is-destructive]]
