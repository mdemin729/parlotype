---
title: SharpHook distinguishes left and right modifiers, in both KeyCode and EventMask
type: knowledge
tags: [sharphook, hotkeys, modifiers, altgr]
created: 2026-07-30
last_updated: 2026-07-30
summary: VcLeftControl/VcRightControl arrive as distinct key codes, and EventMask has separate Left*/Right* bits whose Ctrl/Alt/Shift/Meta names are composites — so test with & and never HasFlag
---

# SharpHook distinguishes left and right modifiers

## Fact

Verified empirically against SharpHook 7.1.1 on Windows 11 by driving a real
`SimpleGlobalHook` with `EventSimulator` and logging what came back.

**1. Modifier key codes are side-specific.** A left-Ctrl press reports
`KeyCode.VcLeftControl` (40977) and a right-Ctrl press reports
`KeyCode.VcRightControl` (45073). Same for Alt, Shift and Meta. This is what
makes "Hold Right Ctrl" a usable dictation gesture that leaves ordinary
left-Ctrl shortcuts untouched (ADR-047).

**2. `EventMask` also resolves sides**, which is *not* obvious from the enum's
usage in most examples:

| Member | Value |
|---|---|
| `LeftShift` / `RightShift` | `0x0001` / `0x0010` |
| `LeftCtrl` / `RightCtrl` | `0x0002` / `0x0020` |
| `LeftMeta` / `RightMeta` | `0x0004` / `0x0040` |
| `LeftAlt` / `RightAlt` | `0x0008` / `0x0080` |
| `Shift` / `Ctrl` / `Meta` / `Alt` | `0x0011` / `0x0022` / `0x0044` / `0x0088` |

**The unqualified names are composites of their two side bits.** So
`mask.HasFlag(EventMask.Ctrl)` is wrong — it demands *both* Ctrl keys be down.
Test with `(mask & EventMask.Ctrl) != 0` instead. `KeyCodeMapper.ToHotkeyModifiers`
does this correctly; the comment there exists to stop someone "simplifying" it
to `HasFlag`.

**3. A modifier's own key-down includes itself in the mask; its key-up does
not.** Pressing left Ctrl yields `DOWN … mask=0x0002`, releasing it yields
`UP … mask=0x0000`. Chord matching can therefore read `HeldModifiers` straight
off the mask on both edges without accumulating state.

## Why This Matters

- The AltGr defence in ADR-047 depends on point 2: European layouts send AltGr
  as left-Ctrl + right-Alt, so a `Ctrl+Alt+Space` binding would fire while the
  user types accented characters. Checking `(mask & EventMask.RightAlt) != 0`
  is enough to tell AltGr apart from a deliberate Ctrl+Alt.
- Point 1 was the load-bearing assumption of the whole multi-gesture design —
  worth re-verifying if SharpHook or libuiohook is ever upgraded.

## Testing note

Simulated events *are* visible to the hook: `EventSimulator` goes through
`SendInput`, and libuiohook does not filter injected events. They arrive with
`EventMask.SimulatedEvent` set — which, confusingly, is `0x0000`, so it shows up
in `ToString()` output but cannot be tested for with a bitwise check. This makes
it practical to drive the real hook from an automated harness rather than
pressing keys by hand.

## Source

- Spike run 2026-07-30, SharpHook 7.1.1, Windows 11 Pro 26200. See ADR-047 and
  `plans/2026-07-30-dictation-hotkey-bindings/`.
- Related: [[sharphook-suppress-event]]
