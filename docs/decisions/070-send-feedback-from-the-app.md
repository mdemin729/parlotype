---
status: accepted
date: 2026-10-17
---

# 070. Send feedback from the app

## Context

Users had no way to report a bug or an idea from inside Parlotype. parlotype.dev now
accepts submissions at `POST https://parlotype.dev/api/feedback`; the contract lives in the
website repository, `docs/feedback-api.md`, and released app versions depend on it.

Parlotype is local-by-default (ADR-032) and documents every network request it makes
(ADR-053 set the model with the update check). A feedback request is a new outbound
request, so where it goes, when it happens and what it carries are product decisions,
not implementation details.

The same review found that Settings → Updates had been overstating the case: it called
the update check "the only network request Parlotype makes while all your speech engines
are local", while model downloads (huggingface.co) and the llama.cpp release list
(api.github.com, read the first time Settings opens in a session) were already
outbound requests.

## Decision

### Contract in Core, HTTP in Platform

`Parlotype.Core/Feedback/` holds `IFeedbackService` and its data: `FeedbackSubmission`
(kind, message, optional email and the three optional diagnostics), `FeedbackSendResult`
with a `FeedbackSendOutcome`, `FeedbackDiagnostics`, `FeedbackLimits` (the contract's
field limits, so the dialog enforces exactly what the server would) and `FeedbackLinks`
(the website form and GitHub Issues URLs).

`HttpFeedbackService` (Platform, registered in `PlatformServiceExtensions`) sends one
`POST` with `Content-Type: application/json; charset=utf-8` and nothing else:

- **No secret.** Anything shipped in the binary can be extracted; the server protects the
  app path with per-IP and daily limits and a kill switch instead.
- **No `User-Agent`.** It would carry the app version even when the user unticked it.
- **15 s timeout, no retries, no queue.** Nothing is sent in the background, on a crash
  or at a later time. Each Send press is exactly one request.
- **Own `HttpClient`.** The shared one has a one-hour timeout for model downloads; a
  per-service client also lets tests hand in a fake `HttpMessageHandler`.
- **Endpoint fixed in Release.** `PARLOTYPE_FEEDBACK_ENDPOINT` overrides it only in a
  `DEBUG` build (for a local `wrangler dev`); tests pass a URI to the constructor.

Responses are classified on status **and** `error`, as the contract requires:

| Response | Outcome | Dialog |
|---|---|---|
| 201, 200 `duplicate` (`ok: true`) | `Sent` | thank, close |
| 400 `invalid_field` + `field: "email"` | `InvalidEmail` | highlight the email field |
| 400 other, 403 `turnstile_failed`, 405, 413, 415 (with their contract error) | `Rejected` | generic error + log + fallback |
| 429 `rate_limited` | `RateLimited` | "try again in a minute", text stays |
| 429 `daily_limit`, 503 `app_paused` | `AppPathClosed` | fallback |
| 500, 503 `unavailable`, anything else, non-JSON | `ServerError` | fallback |
| timeout | `Timeout` | fallback |
| no network / DNS / TLS | `NetworkError` | fallback |

`Rejected` also offers the fallback: it means a bug in this build, and the user's message
should still reach the developer.

### The dialog

A non-modal `FeedbackWindow`, opened from Settings → Help and from the tray menu. Kind
(Feedback / Bug report), message (`MaxLength` 5000), optional email (`MaxLength` 254), and
a "Will be sent" block showing the real `appVersion`, `os` and `uiLanguage` values, each
with its own check box — an unticked value is left out of the request. A short note says
where the data goes (parlotype.dev only), what is stored there, and that the IP address is
not.

The view model is a DI singleton and the window is recreated on each open, so closing the
window with unsent text keeps the draft for the rest of the session. A successful send
clears it.

The fallback copies the message to the clipboard and opens
`https://parlotype.dev/<lang>/feedback/` — `https://parlotype.dev/feedback/` for English;
the site redirects the six app languages it lacks — or GitHub Issues. Nothing goes into the
URL.

### Diagnostics

- `appVersion`: the Desktop assembly's informational version (`-p:Version` in
  `release.yml`), reduced to the contract's alphabet and 64 characters.
- `os`: on Windows, `Windows 10|11 <DisplayVersion> (<major>.<minor>.<build>)`, with
  `DisplayVersion` read from `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion` (11 from
  build 22000). Elsewhere `RuntimeInformation.OSDescription`, one line, 128 characters.
  This is the OS-dependent behaviour this ADR records.
- `uiLanguage`: the interface language's neutral culture name (`de`, `pt`), the same code
  the website URL uses.

### Network disclosure

Settings → Updates now states what Parlotype contacts on its own (the update check), what
it contacts when Settings opens (the llama.cpp release list), and what only follows an
explicit action (model and server downloads, cloud engines, feedback). README, AGENTS.md,
`IUpdateService` and the vault say the same.

## Consequences

- Users can report a problem in a few seconds, without a GitHub account.
- The app depends on a public contract; changing a field or a status code needs
  `/api/feedback/v2` and an app release. `FeedbackLimits` must never be tightened below the
  server's limits.
- The privacy page on parlotype.dev (all 19 languages) must mention this request; the app
  cannot change that.
- A Cloudflare rule that challenges requests without a `User-Agent` would turn every send
  into a fallback. The fallback still works, so the failure is visible but not lossy.
