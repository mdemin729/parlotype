---
title: HttpClient locks BaseAddress after the first request
type: knowledge
tags: [dotnet, httpclient, speech, llamacpp, cloud]
created: 2026-09-07
last_updated: 2026-09-07
summary: BaseAddress and DefaultRequestHeaders throw once a request has gone out, so a field-initialized HttpClient is single-use — it must be recreated per Initialize, not reconfigured
---

# HttpClient locks BaseAddress after the first request

`HttpClient.BaseAddress` (and `DefaultRequestHeaders`, and `Timeout`) are guarded
by an internal `CheckDisposedOrStarted()`. The setter throws
`InvalidOperationException` — *"This instance has already started one or more
requests. Properties can only be modified before sending the first request."* —
the moment any request has been issued on that instance.

## Why it bites this codebase specifically

A recognizer that is initialized more than once per process — a different port
after a server respawn, a different base URL after the user edits settings — will
try to point the same client somewhere new on the second `InitializeAsync`. A
`private readonly HttpClient _http = new();` field is therefore **one-shot**: it
works on first initialize and throws on every later one, so the bug only appears
when a user changes a setting or a managed server restarts. First hit was
`LlamaCppSpeechRecognizer` (user stack trace); the same shape then applied to the
cloud path, where the API key lives in a default header.

The fix in both places is to **recreate the client** in `InitializeAsync` rather
than reconfigure it:

- `src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs` — "Recreated on each
  Initialize because HttpClient locks BaseAddress after first"
- `src/Parlotype.Platform/Speech/OpenAiCompatibleSpeechRecognizer.cs` — same, and
  extends it to default headers

## Testing note

`LlamaCppServerInfo.ProbeAsync` uses its **own internal** `HttpClient`, not the
recognizer's. The adopt-an-existing-server path therefore never touches the
recognizer's client and does **not** reproduce the crash — a test that only
exercises probing will pass against the buggy form. Drive `TranscribeAsync` or
the spawn/health-poll path to actually cover it.
