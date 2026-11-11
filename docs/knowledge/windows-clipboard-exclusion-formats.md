---
title: "Windows clipboard exclusion formats"
area: "windows"
summary: "Three registered clipboard formats, set in the same OpenClipboard session as the text, keep it out of Win+V history, cloud sync and clipboard monitors."
verified: 2026-11-11
---

# Windows clipboard exclusion formats

To keep clipboard content out of Clipboard History (Win+V), cross-device
Cloud Clipboard sync, and third-party clipboard monitors, set these
`RegisterClipboardFormat` formats **in the same OpenClipboard session** as the
content itself (after `SetClipboardData(CF_UNICODETEXT, …)`, before
`CloseClipboard`):

| Format name | Payload | Semantics |
|-------------|---------|-----------|
| `ExcludeClipboardContentFromMonitorProcessing` | any (DWORD 0 fine) | presence alone tells monitors to skip |
| `CanIncludeInClipboardHistory` | DWORD 0 | 0 = keep out of Win+V history |
| `CanUploadToCloudClipboard` | DWORD 0 | 0 = never sync cross-device |

Notes:
- `EmptyClipboard` (e.g. on restore) clears the flags along with the content —
  restored user content keeps normal behaviour automatically.
- Each `SetClipboardData` hands ownership of the `GlobalAlloc` block to the
  system on success; free it only on failure.
- Headless/CI can't verify this (global shared clipboard) — check manually:
  dictate, press Win+V, the injected text must not appear.
- Used by `ClipboardTextInjectionService` since the 2026-07 security audit
  (S4, ADR-046).
