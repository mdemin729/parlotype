---
title: "Send feedback…" window
status: completed
created: 2026-10-17
started: 2026-10-17
completed: 2026-10-17
---

# "Send feedback…" window

## Problem

There is no way to tell the developer about a bug or an idea from inside Parlotype. The
user has to find the GitHub repository, have an account, and open an issue — most people
who hit a problem simply stop using the app.

parlotype.dev now has `POST /api/feedback` in production. Its contract is
`docs/feedback-api.md` in the website repository (mdemin729/parlotype-website); this plan
implements the app side against it and changes nothing on the site.

A second, older problem surfaced on the way: Settings → Updates says the update check is
"the only network request Parlotype makes while all your speech engines are local". That
was already false — models download from huggingface.co and the llama.cpp release list is
read from api.github.com — and a feedback request would make it more false.

## Approach

Contract-first, per the `implement-feature` skill (decision record: ADR-070).

- **Core** (`Parlotype.Core/Feedback/`): `IFeedbackService`, `FeedbackSubmission`,
  `FeedbackKind`, `FeedbackSendResult`/`FeedbackSendOutcome`, `FeedbackDiagnostics`,
  `FeedbackLimits` (the contract's field limits) and `FeedbackLinks` (website-form and
  GitHub Issues URLs). No HTTP in Core.
- **Platform**: `HttpFeedbackService` — one `POST`, 15 s timeout, no retries, no
  `User-Agent`, owns its own `HttpClient` so a test can hand it a fake
  `HttpMessageHandler`. Classifies the response on `status` **and** `error`, exactly as the
  contract's table says. `OperatingSystemDescription` formats `Windows 11 24H2
  (10.0.26100)`. The endpoint can be overridden only in a Debug build
  (`PARLOTYPE_FEEDBACK_ENDPOINT`) or by a test constructing the service.
- **Desktop**: `FeedbackWindow` + `FeedbackViewModel` (singleton, so an unsent draft
  survives closing the window for the rest of the session). Entry points: a button on
  Settings → Help and a tray item under Settings. Fallback: copy the message, open
  `https://parlotype.dev/<lang>/feedback/` (English without a prefix), or GitHub Issues.
- **Network disclosure** rewritten honestly in Settings → Updates, README, AGENTS.md,
  `IUpdateService` docs and the vault.

## Workplan

- [x] Read the contract and the worker implementation; nothing missing
- [x] ADR-070
- [x] Core contract, Platform client, DI registration
- [x] Window, view model, entry points (Help page, tray)
- [x] Network-disclosure copy (Settings → Updates) and docs
- [x] Strings in all 25 languages through the brief/import pipeline
- [x] Unit tests (every response row, timeout, offline, unticked fields, limits) and headless
      tests (MaxLength, culture switch, fallback, draft retention)
- [x] End-to-end against a local `wrangler dev` of the website: 201, 200 duplicate, 400
      email, 429 rate_limited, 503 app_paused, 429 daily_limit, server down
- [x] Vault, CHANGELOG, INDEX; PR against master

## Outcome

Verified end to end with the real `HttpFeedbackService` (Debug build,
`PARLOTYPE_FEEDBACK_ENDPOINT`) against `wrangler dev` of parlotype-website `origin/main`:

| Scenario | Result |
|---|---|
| all fields | 201 → Sent, id 1; D1 row holds exactly the sent fields, CRLF stored as LF |
| same text again | 200 duplicate → Sent, id 1 |
| `tester@localhost` | 400 `invalid_field`/`email` → InvalidEmail |
| nothing ticked, no email | 201 → Sent; D1 row has null email/app_version/os/ui_language |
| fourth valid send in a minute | 429 `rate_limited` → RateLimited |
| `FEEDBACK_APP_PAUSED=1` | 503 `app_paused` → AppPathClosed (fallback) |
| `FEEDBACK_DAILY_LIMIT=1` | 429 `daily_limit` → AppPathClosed (fallback) |
| Worker stopped | connection refused → NetworkError (fallback) |
| port held by a hung `workerd` | no answer in 15 s → Timeout (fallback) |

The window was rendered headlessly in en, fr, hr, ru and el with the fallback and the
email error showing: no clipping.

Not done here:

- **Clicking through the running app** — no desktop automation in this session; the
  window's behaviour is covered by headless tests (`FeedbackWindowTests`,
  `FeedbackViewModelTests`) and the HTTP path by the harness above.
- **The website's privacy page** must mention this request in all 19 languages; this
  plan changed nothing on the site.
- **Native-speaker review** of the 40 new strings, as for every ADR-069 string.
