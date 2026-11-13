---
status: proposed
date:
---

# 074. Prefer the llama.cpp build that upstream's versioned release names

## Context

`GitHubLlamaServerCatalog` lists the ten newest llama.cpp builds. Two places choose from that
list: the llama.cpp server page, where the user picks, and speech cleanup
([ADR-073](073-speech-cleanup-s1-mini.md)), which installs the newest CPU build without
asking which one. Upstream publishes a build every hour or two, so "newest" is whatever was
pushed last, and two people who turn cleanup on the same afternoon get different builds.

Since late 2026 upstream separates the two things
([ggml discussion 1579](https://github.com/ggml-org/ggml/discussions/1579),
[knowledge note](../knowledge/llama-cpp-release-assets.md)). `b{N}` builds are nightlies,
flagged as pre-releases. A versioned release (`v1.0.0`, 2026-11-13; `v0.9.0` nine days
earlier) is "just a semantic version tag" that "points to a respective nightly build", which
"has all the corresponding assets". A maintainer starts it by hand, and the workflow runs an
API/ABI compatibility check against the previous release first. It is not a separately
tested binary: it is the nightly upstream chose to call a release. That is still a better
default for an install the user did not choose than a build that is minutes old, and it
stays the same for days.

What the live API showed on 2026-11-13:

- A versioned release leaves the first page of `/releases` in about a day (the list is
  ordered by creation time and gains about ten entries a day). `GET /releases/latest` always
  answers with it: GitHub defines "latest" as the newest release that is neither a draft
  nor a pre-release.
- Its one asset, `nightly-tag.txt`, holds the build tag and a newline (`b12359\n`). The API
  answer does not include the content, but it includes the file's SHA-256 (`digest`), and
  the release notes name the same build. `sha256("b12359\n")` equals the digest, so the
  build can be read from the notes and proven against the pointer file without downloading
  it. Upstream's workflow skips the file when the release commit has no nightly, and marks
  the notes as unfinished, so either can be missing.
- The build a versioned release names leaves the first page on the same schedule. After
  that its assets come from `GET /releases/tags/b{N}`, which has the same shape as a list
  entry. `b12110` (`v0.9.0`) still had all its assets nine days later. Upstream states no
  retention period.
- A `304 Not Modified` costs one request against the unauthenticated limit of 60 an hour
  (measured).

## Decision

When upstream's newest versioned release names a build, Parlotype treats that build as the
one to install by default. In this document it is the **released build**.

### Resolving it

Every catalog refresh (at most once an hour on its own, and on "Check for updates") makes one
more request after the list, to the same host and with the same headers:

1. `GET /repos/ggml-org/llama.cpp/releases/latest`, with `If-None-Match` when the answer is
   cached. On `304` the cached pointer stands. On `200` the catalog takes the version from
   `tag_name`, collects the build tags (`b` and digits) that appear in the notes, and accepts
   the one whose `sha256(tag + "\n")` equals the `digest` of the `nightly-tag.txt` asset. No
   match, no asset or no digest means there is no released build.
2. The released build's assets come from the list when the build is on its first page.
   Otherwise they come from the cache, and failing that from
   `GET /repos/ggml-org/llama.cpp/releases/tags/b{N}`, once, parsed by the code that parses
   a list entry.

That is two requests per refresh instead of one, and a third about once per upstream
release. All of them go to `api.github.com`; no file is downloaded to find the build.

### Where it appears

- `LlamaServerReleaseGroup` gains `string? ReleaseVersion`: `"v1.0.0"` on the released
  build, null on every other. It is a property on an existing record, not a new type.
- `ILlamaServerCatalog.FetchAsync` returns the released build first, then the other builds
  newest first, with no build listed twice.
- `LlamaServerVariantPicker.PickCpuVariant` returns the released build's CPU variant when
  there is one, wherever that group sits in the list, and otherwise the newest CPU build as
  today. Speech cleanup therefore installs the released build. It still keeps an install
  that is already on disk and never replaces one on its own.
- On the llama.cpp server page the released build's rows are at the top and carry a badge
  with the version (`v1.0.0`) beside the build number. The badge text is upstream's tag, so
  it needs no translation. A tooltip that explains it would be one new string in every
  language; whether to add it is left to the owner.
- The "Update available" banner follows the released build: it shows when a released build
  is known and every installed build has a lower number. Today it compares against the
  newest nightly by exact tag, so it is on nearly all the time, and with the released build
  first it would otherwise offer a downgrade to someone on a newer nightly. Its wording
  does not change.

### When it cannot be resolved

Nothing fails because of the released build; the list is still the list.

| Situation | Result |
|---|---|
| Offline, cache present | The cached list and the cached released build, as the list is served today. |
| `/releases/latest` fails, is rate-limited, or there is no versioned release | The cached pointer if there is one, otherwise no released build. |
| No `nightly-tag.txt`, no digest, or no build in the notes matches it | No released build. Logged as a warning. |
| `/releases/tags/b{N}` fails | No released build this time; tried again on the next refresh. |
| The released build has no asset for this OS and architecture | No released build. |

With no released build, the catalog returns the newest builds unmarked, the picker takes the
newest CPU build and the banner behaves as it does now.

### Cache

`.cache/releases.json` gains the `/releases/latest` ETag, the version, the build tag and, when
they were fetched by tag, that build's assets. `GitHubLlamaServerCatalog.CacheVersion` goes
from 2 to 3, so a cache written by the current code is ignored and fetched afresh.

### New outbound requests

| Request | Host | Trigger | What is sent |
|---|---|---|---|
| `GET /repos/ggml-org/llama.cpp/releases/latest` | `api.github.com` | Each refresh of the llama.cpp build list: the first time Settings opens in a session and at most once an hour, "Check for updates" on the llama.cpp server page, and turning speech cleanup on with no server installed when the cached list is older than an hour. | An anonymous GET with `User-Agent: parlotype/<assembly version>`, `Accept: application/vnd.github+json` and, when cached, `If-None-Match`. |
| `GET /repos/ggml-org/llama.cpp/releases/tags/b{N}` | `api.github.com` | The same refresh, only when the released build is neither on the first page of the list nor cached. About once per upstream release. | The same headers without `If-None-Match`. The build tag in the path came from GitHub's own answer. |

Both follow the same user actions as the existing list request and go to the same host, so
`docs/network-requests.md` gains the two paths in the existing row, and the sentence in the
README and in Settings → Updates ("reads the list of llama.cpp server builds from
api.github.com") stays true as written.

### Alternatives not taken

- **Download the pointer file** from
  `github.com/ggml-org/llama.cpp/releases/latest/download/nightly-tag.txt`. One request, no
  API quota, no parsing of notes. It adds `github.com` and
  `release-assets.githubusercontent.com` to what opening Settings contacts, which changes
  the Settings → Updates sentence in 25 languages, and it does not give the version name.
- **Trust the notes without the digest.** The notes are free text that upstream intends to
  rewrite. With the digest, a changed format produces "no released build", never a wrong one.
- **Pin a build in Parlotype**, as models are pinned. No requests and a build we tested, but
  it ages with each Parlotype release, upstream promises no retention for old builds, and
  the server page would still need to tell users which build is the release.

## Consequences

- Speech cleanup installs the same build for everyone until upstream releases again. That
  build can be a week or more behind; a fix that landed since is one manual install away on
  the llama.cpp server page. If upstream stops making versioned releases, the released build
  grows old with nothing to flag it.
- A refresh costs two requests of the 60 an hour instead of one, three about once per
  upstream release and on the first refresh after this change. "Check for updates" can be
  pressed about thirty times an hour before GitHub refuses, down from sixty.
- The "Update available" banner appears about as often as upstream releases, not constantly.
- The catalog now depends on two upstream habits that are not promised: the notes naming
  the build, and `nightly-tag.txt` existing. Losing either falls back to today's behaviour,
  and a warning in the log is the only sign.
- `ILlamaServerCatalog` no longer returns a strictly newest-first list; its contract says so.
- Tests need a recorded `/releases/latest` answer and a `/releases/tags/b{N}` answer next to
  the existing fixture, whose `v1.0.0` entry was trimmed of its notes.
