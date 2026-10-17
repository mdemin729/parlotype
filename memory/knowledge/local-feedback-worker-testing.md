---
title: Testing the feedback client against a local parlotype-website Worker
type: knowledge
tags: [feedback, testing, wrangler, windows]
created: 2026-10-17
summary: How to exercise HttpFeedbackService end to end without touching production — and two Windows gotchas with wrangler dev (long-path D1 failure, orphaned workerd holding the port)
---

# Testing the feedback client against a local Worker

Never send test messages to production: every one lands in the developer's inbox
([[../decisions/_index|ADR-070]]). Run the website's Worker locally instead.

## Recipe

1. Use the website repo at `origin/main` (the local checkout may lag; `git archive
   origin/main | tar -x -C <dir>` gives a clean copy without touching it), then
   `npm ci`, `npm run build`, `cp .dev.vars.example .dev.vars`.
2. `npx wrangler d1 migrations apply parlotype-feedback --local --persist-to <short dir>`
3. `npx wrangler dev --port 8787 --persist-to <same short dir>`
4. Debug build of the app (or anything constructing `HttpFeedbackService` with the
   public ctor) with `PARLOTYPE_FEEDBACK_ENDPOINT=http://localhost:8787/api/feedback`.
   Release builds ignore the variable.
5. Scenarios: same text twice → 200 `duplicate`; `tester@localhost` → 400 email; a fourth
   valid send within a minute → 429 `rate_limited` (only requests that pass validation
   count); `FEEDBACK_APP_PAUSED=1` in `.dev.vars` → 503 `app_paused`;
   `FEEDBACK_DAILY_LIMIT=1` with rows already stored → 429 `daily_limit`; Worker stopped →
   connection refused.
6. Inspect what was stored: `npx wrangler d1 execute parlotype-feedback --local
   --persist-to <dir> --json --command "SELECT * FROM feedback"`.

## Windows gotchas

- **Long paths break local D1.** From a deep directory (the Claude scratchpad is ~200
  characters), `d1 migrations apply --local` fails with a bare `internal error;
  reference = …`. `--persist-to` pointing at a short directory fixes it; pass the same
  flag to `wrangler dev`.
- **Stopping `wrangler dev` can orphan `workerd.exe`**, which keeps listening on 8787 and
  accepts connections without answering. The next "server down" test then reports a
  **timeout** instead of a network error. Kill every process whose command line contains
  the site directory before restarting.
