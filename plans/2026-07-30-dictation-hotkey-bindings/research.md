# Research: dictation hotkeys in comparable applications

User-provided competitive research (2026-07-30) on hotkey conventions in native
and third-party dictation tools. The adopted feature set is summarised in
[task.md](task.md); design consequences are worked through in
[implementation-plan.md](implementation-plan.md).

---

## What the built-in tools actually use

| Platform | Native STT | Default trigger |
|---|---|---|
| Windows 11 | Voice Typing | Win+H (Win+Alt+H opens it in keyboard-nav mode) |
| Windows 10 | Speech Recognition | Win+Ctrl+S |
| macOS | Dictation | Double-tap Globe/Fn on Apple Silicon built-in keyboards; double-tap Control on older or third-party keyboards; single press of the F5 microphone key on 2021+ MacBook Pro/Air |

Third-party references worth knowing: Wispr Flow defaults to hold-Fn on Mac with
push-to-talk on hold and double-tap for hands-free mode, WhisperWriter uses
`ctrl+shift+space`, VoiceToText24 uses Ctrl+Alt+Q, and superwhisper ships with
no default at all (you pick during onboarding).

Two things stand out. First, **Win+H is off the table** — it's shell-reserved,
and SharpHook/libuiohook won't get it reliably. Second, macOS's own default is
*double-tap a bare modifier*, and Windows/Linux have no equivalent convention,
which means double-tap-modifier is the one gesture you can make identical
everywhere.

## Recommendation

**Ship two bindings, not one**, because dictation has two genuinely different
modes:

**Push-to-talk (hold) — the primary default:** `Hold Right Ctrl`

Same physical key on all three platforms. No chord, comfortable to hold for
10 seconds, and because `VC_CONTROL_R` is distinguished from `VC_CONTROL_L`,
every normal Ctrl/Cmd shortcut keeps working untouched. This matches Wispr
Flow's interaction model without borrowing its Fn key (which can't be captured
cleanly anyway — see below).

**Toggle (hands-free) — secondary default:** `Double-tap Ctrl`

This is literally Apple's default on external keyboards, so Mac users switching
from native Dictation need to learn nothing. On Windows and Linux it collides
with nothing. Consistent muscle memory across all three OSes.

**Explicit chord fallback:** `Ctrl+Alt+Space` (Win/Linux) / `Ctrl+Option+Space`
(macOS)

Needed for environments where modifier-only detection isn't available — chiefly
Wayland (below) — and it echoes WhisperWriter's convention.

**Cancel:** `Escape`. Both macOS Dictation and Wispr Flow use it; don't invent
something else.

## Conflicts to avoid, with reasons

- **Ctrl+Shift+Space** — tempting (WhisperWriter uses it), but it's *Parameter
  Info* in Visual Studio and signature help in VS Code. The audience is
  developers. Skip it. *(Note: this is Parlotype's current shipped default —
  this plan replaces it.)*
- **Ctrl+Alt+\<letter\>** — AltGr on European layouts *is* Ctrl+Alt, so a
  Polish or German user hitting AltGr+P for `ó` would fire the hotkey. Space is
  the safest member of that family, and events where right-Alt is physically
  down should still be filtered.
- **Super+H on Linux** — GNOME uses it to hide the focused window.
- **Win+Space / Ctrl+Win+Space** — input-source switching; a multilingual app
  specifically shouldn't fight the layout switcher.
- **Double-tap Ctrl on macOS** — a real collision if the user still has Apple
  Dictation on its default. Detect it during onboarding and offer to either
  switch Parlotype to *double-tap Right Command* (a stock preset in Apple's own
  shortcut menu, so it's idiomatic and free) or point them at System Settings →
  Keyboard → Dictation.

## Implementation notes for the SharpHook/Avalonia stack

**Wayland is the hard constraint.** SharpHook can't grab global keys under
Wayland — no compositor lets an arbitrary client do that. The path there is
`org.freedesktop.portal.GlobalShortcuts`, where the *compositor* owns the
binding UI and the user picks the chord. That portal has no concept of
"double-tap a bare modifier," so on Wayland the chord fallback becomes the only
option. Design the settings UI so it can degrade to "your desktop manages this
shortcut — open system settings" rather than showing a capture field that
silently does nothing.

**Modifier-tap detection needs a filter:** only count a Ctrl press/release pair
as a "tap" if no other key went down in between, and the release came within
~250 ms. Otherwise `Ctrl+C` followed quickly by `Ctrl+V` will look like a
double-tap. Use ~300–400 ms for the inter-tap window.

**macOS requires Accessibility and Input Monitoring (TCC) permission** for
libuiohook to see anything. First-run should explain this before the OS prompt
appears, not after.

**Don't try to use Fn/Globe.** Wispr Flow does it via a private mechanism;
through libuiohook there are no consistent events for it.

**Borrow Wispr Flow's settings design:** multiple bindings per action,
validation against system reservations and existing bindings before accepting a
shortcut, and a tooltip on the recording indicator showing the current
push-to-talk key. That last one is cheap and removes most "what was my hotkey
again" support traffic.
