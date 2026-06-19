---
title: Win32 keyboard-layout detection quirks
type: knowledge
status: active
tags: [win32, pinvoke, keyboard-layout, culture]
created: 2026-06-11
last_updated: 2026-06-18
summary: Keyboard layouts are per-thread on Windows — query the foreground window's thread, then drill to its focused input thread via GetGUIThreadInfo.hwndFocus for multi-thread apps; transient LANGIDs have no CultureInfo
---

# Win32 keyboard-layout detection quirks

Learned while building `Win32KeyboardLayoutService` (ADR-036).

- **Keyboard layouts are per-thread on Windows.** `GetKeyboardLayout(0)` returns
  the *calling* thread's layout, which for a background tray app like Parlotype
  can lag what the user is actually typing with. The correct query is
  `GetForegroundWindow()` → `GetWindowThreadProcessId()` →
  `GetKeyboardLayout(thatThreadId)`.
- **…but that thread is not always the one receiving keystrokes.** Modern
  multi-thread apps (Win11 Notepad, WinUI / XAML-island hosts) spread their UI
  across many threads — Notepad showed **12**. `GetForegroundWindow()` →
  `GetWindowThreadProcessId()` returns the **top-level frame** window's thread,
  whose layout is **stale**; the live layout lives on the **focused input
  control's** thread. Symptom: switching layout (Alt+Shift) while focus is in
  another app doesn't update until focus returns to your own app. **Fix:** drill
  into the focus window's thread —
  `GetGUIThreadInfo(frameThreadId, ref gti)` → `gti.hwndFocus` →
  `GetWindowThreadProcessId(hwndFocus)` → `GetKeyboardLayout(focusThreadId)`.
  `GetKeyboardLayout` reads *any* thread's layout accurately, so no
  `AttachThreadInput` dance is needed. Classic single-thread apps are unaffected
  (focus window lives on the frame thread). Proven empirically with throwaway
  thread-enumeration probes: Notepad's `'Notepad'` frame thread reported `en`
  while its `'NotepadTextBox'` input thread held `ru`.
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
