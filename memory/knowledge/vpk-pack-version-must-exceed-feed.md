---
title: vpk pack Refuses Any Version At or Below the Local Releases Folder
type: knowledge
status: active
tags: [velopack, vpk, ci, release, dry-run]
created: 2026-08-02
last_updated: 2026-08-02
summary: "`vpk pack` fails if --packVersion is <= any release already present in --outputDir, so the CI dry-run version has to sort above the live feed the delta step just downloaded."
---

# `vpk pack` Refuses Any Version At or Below the Local Releases Folder

## The fact

`vpk pack` hard-fails when `--packVersion` is equal to or lower than any release
present in `--outputDir`:

```
[FTL] There is a release in channel win which is equal or greater to the current
version 0.0.1-dryrun. Please increase the current package version or remove that
release.
```

This couples two steps in `.github/workflows/release.yml` that look independent.
`vpk download github` populates `Releases/` with the live feed so `vpk pack` can
build deltas — and in doing so it constrains what versions `vpk pack` will accept.

## Why it bit

The dry-run default version was `0.0.1-dryrun`, chosen when **no Velopack feed
existed**. `vpk download` was `continue-on-error` and had failed on every run, so
`Releases/` was always empty and the low version never mattered. The comment in
the workflow even said so: "Expected to fail on the very first release, when no
feed exists yet."

`v0.4.0` was the first release published through Velopack. The very next PR dry
run was therefore the first time `vpk download` *succeeded* — it pulled
`Parlotype-0.4.0-full.nupkg` — and `Pack` immediately failed. The trigger was
publishing a release, not any change to the workflow, so it surfaced on an
unrelated PR.

Fixed by defaulting dry runs to `9999.0.0-dryrun`. The number is deliberately
absurd: anything plausible breaks again the moment it falls behind the real
version. See [[054-curated-release-notes]] for the PR this surfaced on.

## Consequences worth knowing

- **A dry run that packs above the feed now builds a real delta**, so
  `Verify pack output` logging "Delta package built" is genuine evidence that
  delta generation works — the failure mode `docs/RELEASING.md` warns about
  (every user silently downloading ~80 MB instead of a patch) is finally
  observable in CI.
- Dry-run artifacts now also contain the downloaded full `.nupkg` from the live
  feed, so they are ~77 MB larger than before.
- Dispatching a dry run with an explicit version needs the same care: pick one
  above the latest published release.

## See also

- `docs/RELEASING.md` → "Dry runs"
- [[velopack-pack-folder-is-destructive]]
- [[github-release-empty-body-fallback]]
