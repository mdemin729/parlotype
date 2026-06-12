---
title: Win32 keyboard-layout detection quirks
type: knowledge
status: active
tags: [win32, pinvoke, keyboard-layout, culture]
created: 2026-06-11
summary: Keyboard layouts are per-thread on Windows — query the foreground window's thread, not your own; transient LANGIDs have no CultureInfo
---

# Win32 keyboard-layout detection quirks

Learned while building `Win32KeyboardLayoutService` (ADR-036).

- **Keyboard layouts are per-thread on Windows.** `GetKeyboardLayout(0)` returns
  the *calling* thread's layout, which for a background tray app like Parlotype
  can lag what the user is actually typing with. The correct query is
  `GetForegroundWindow()` → `GetWindowThreadProcessId()` →
  `GetKeyboardLayout(thatThreadId)`.
- **The HKL low word is the input-language LANGID**; the high word is a device
  handle (`0xF...` for custom/IME layouts) and must be masked off before
  passing to `CultureInfo.GetCultureInfo(int)`.
- **Transient LANGIDs throw.** Custom layouts can carry LANGIDs in the
  `LOCALE_TRANSIENT` range (0x2000, 0x2400, …) with no culture data —
  `CultureInfo.GetCultureInfo` throws `CultureNotFoundException`. Treat as
  "detection unavailable" (return null), don't crash.
- `CultureInfo.TwoLetterISOLanguageName` can be `"iv"` (invariant) — also treat
  as undetected.

See [[../services/platform|platform]] → `Win32KeyboardLayoutService`.
