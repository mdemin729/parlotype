---
title: "Session: 2026-10-17 send feedback window"
type: session
status: active
tags: [feedback, network, localization]
created: 2026-10-17
summary: Added the "Send feedback…" window (ADR-070) against parlotype.dev's public feedback API, and corrected the "only network request" claim on Settings → Updates
---

# Session: 2026-10-17

## Active Focus
"Send feedback…" window posting to `https://parlotype.dev/api/feedback` (contract: website repo `docs/feedback-api.md`), plus an honest network-request disclosure.

## Decisions Made
- ADR-070: `IFeedbackService` in Core, `HttpFeedbackService` in Platform; one POST per Send, 15 s, no retries/secret/`User-Agent`/redirects; classify on status **and** `error`.
- `Rejected` (app-bug responses) also offers the website fallback — the spec only asked for a generic error, but otherwise the user's message is stranded.
- Draft retention via a singleton view model; the window itself is rebuilt per open.
- `UserDialogService` now prefers the *active* window as owner, so the thank-you dialog sits over the feedback window rather than the always-on-top widget.
- `docs/network-requests.md` is the single list of outbound requests; the llama.cpp release list is disclosed as "when you open Settings" because that is when `LlamaCppSettingsViewModel` fetches it.

## Facts Learned
- See `memory/knowledge/local-feedback-worker-testing.md` (local D1 long-path failure; orphaned `workerd`).
- Rate limiter on the Worker counts only requests that pass validation.

## Open Blockers
- The website's privacy page (19 languages) must mention the feedback request — out of scope for this repo.
- Native-speaker review of the 40 new strings (translator notes in the PR).

## Documentation Status
- ADR: done — ADR-070
- Vault (services/architecture): done — core/platform/desktop profiles, subsystems "Feedback", decisions index
- Knowledge (non-derivable facts): done — `local-feedback-worker-testing.md`

## Next Action
Review the PR; then update parlotype-website's privacy page and, if wanted, one live send to production with the owner's consent.
