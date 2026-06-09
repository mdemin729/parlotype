---
title: Language settings & quick-picker — design brief (start from scratch)
status: brief
created: 2026-06-01
audience: product/UX designer
---

# Design Brief — "Language" Settings & Quick Picker

> **This is a design prompt, not an implementation spec.** Design the UI/UX from
> scratch. Two earlier in-house iterations exist (see *Prior art* at the end) — treat
> them as **reference only**. Do **not** feel bound by their layout; we were not happy
> with either result and want a fresh take. Engineering will adapt to your design, not
> the other way around.

---

## 1. What we're asking you to design

Design the components that let a user set their **Language** preferences with the
**fewest possible actions**, across **two surfaces**:

1. **Settings → Language page** — the full, authoritative control.
2. **Quick picker on the Transcribe window** — a compact, always-visible control for
   fast access to the translation languages, without opening Settings.

Two headline goals, in priority order:

- **Convenience** — minimum number of actions to read the current state and to change it.
- **Slick design** — clean, modern, confident; feels effortless.

Deliver hi-fi mockups + an interactive prototype (Figma or equivalent) covering every
state listed in §6.

---

## 2. Product context (so your design is realistic)

**Parlotype** is a **local-by-default desktop voice-to-text app** (Windows/macOS/Linux,
built on .NET + Avalonia). The user speaks; we transcribe on-device; the text is typed
into whatever app they're focused on.

Two concepts drive this brief:

- **Source language** — the language the user *speaks* (what we recognise).
- **Target language** — the language we *output*. If translation is off, output = source.

The **Transcribe window** is a tiny, always-on-top widget (currently ~240×128 px) with a
big record button and a settings cog. It floats over the user's other apps. Real estate
is *extremely* tight — the quick picker must be space-efficient and unobtrusive, but
reachable in one click.

### Visual language (current app cues — match or evolve tastefully)
- Predominantly **dark UI**, with a light theme also supported — design for **both**.
- Accent **blue** `#378ADD` is currently used for "active/focused" affordances and for the recording state on the Transcribe window.
- Iconography is simple line-style (e.g. a gear/cog for settings).
- Type is clean and modern; generous spacing; rounded controls (record button is a pill).

You may refine this palette.

---

## 3. Hard requirements (the model decides what's available)

The set of options is **not fixed** — it depends on the active recognition **model**.
Your design must gracefully handle **any model** and a **variable number** of supported
languages (from **1** up to **~hundreds**).

### Source language — the picker must offer:
1. **System keyboard layout** — use the OS keyboard layout as the spoken-language hint.
   *(This is a new, first-class option — give it a clear, friendly treatment.)*
2. **Auto** — let the model auto-detect the spoken language.
3. **A list of languages** the active model supports as source audio.

### Target language — the picker must offer:
1. **Disabled (no translation)** — output stays in the **same language as Source**.
2. **A list of languages** the active model supports translating **to**.

### Special case — collapse to a toggle:
- If the target side has **only two options** (e.g. *Disabled* and *English*), a **simple
  toggle** is preferable to a full picker. Your design should show both the **toggle form**
  (2 options) and the **full-picker form** (many options), and make the transition between
  them feel natural — the same model switch can change which form is shown.

### Reality of the lists (use this to size your components):
| Model family | Source options | Target options |
|---|---|---|
| Whisper (typical local model) | Keyboard / Auto / **~99 languages** | **Disabled + English only** → *use the toggle form* |
| LLM model (e.g. Gemma) | Keyboard / Auto / **full language list (hundreds)** | **Disabled + full language list** → *full picker* |
| Some models | translation **not supported at all** | only **Disabled** (target side is effectively off) |

So your components must scale from "two options → toggle" all the way to "hundreds →
needs search/filtering", and handle "translation unavailable" cleanly.

A language is shown as **"English"** when its English and native names match, otherwise
**"English — Native"** (e.g. *"Russian — Русский"*). Codes are ISO-style (`en`, `ru`, …)
but never shown to the user.

---

## 4. The "minimum actions" mandate (most important)

Optimise relentlessly for the common journeys. For each, propose the **shortest** path and
show the click/keystroke count:

- **J1 — Glance:** the user wants to *know* the current source → target at a glance,
  without interacting. State must be legible at rest, on both surfaces.
- **J2 — Flip translation on/off:** the single most frequent action. Should be ~**1 action**.
- **J3 — Change target language** (multi-target models): from any state to a new target in
  as few actions as possible; long lists need fast search/scan.
- **J4 — Change source language:** same, less frequent than J3.
- **J5 — From the Transcribe window:** change the translation language/toggle *without*
  opening full Settings.

Ideas you may use (your call — not mandates): a most-recently-used / "Recent" cluster
pinned to the top of long lists; type-to-filter search; keyboard-first navigation; sensible
defaults so first enable needs zero extra picking.

---

## 5. The two surfaces

### 5.1 Settings → Language page
The authoritative control. Has room to breathe. Must express, in one coherent mental model:

- Source → (translation on/off) → Target, as a **single left-to-right relationship**, not
  three disconnected controls.
- The source picker (Keyboard / Auto / list) and the target picker (Disabled / list **or**
  toggle).
- Clear, inline feedback when translation is **unavailable for the current model** (don't
  hide the affordance and leave the user guessing — explain why it's off).

### 5.2 Transcribe window quick picker
A compact component embedded in / attached to the ~240×128 floating widget. Requirements:

- One-click access to change the **translation** language and/or toggle translation.
- Must not crowd out the record button or grow the widget unreasonably (a popover/flyout
  anchored to the widget is acceptable; show how it opens and where).
- Should reflect current source→target at a glance even when collapsed.
- Consider that the widget is **topmost over other apps** and may be small — the picker
  must remain usable when the list is long (search/scroll).

Show how the two surfaces stay **consistent** (same iconography, same labels, same mental
model) without the quick picker being a cramped copy of the full page.

---

## 6. States & edge cases to mock (please cover all)

1. **Translation OFF** (target = Disabled) — output equals source. Show how the target
   side looks when inert.
2. **Translation ON, multi-target** — full target picker, a target selected.
3. **Translation ON, two options only** — the **toggle** form (Disabled ⇄ English).
4. **Translation unavailable** — model can't translate; target side disabled with a clear
   reason.
5. **Source = System keyboard layout** vs **Auto** vs **explicit language** — three distinct,
   recognisable resting states.
6. **Long list** (~99 / hundreds) — search/filter active, empty-search "no matches" state,
   and a "Recent" cluster if you use one.
7. **Short list** (1–3 options).
8. **Model switch** — the available options changed; the previously selected language is no
   longer supported (how do you fall back / inform the user?).
9. **Picker open vs closed**, **focused/active** styling, **hover**, **keyboard focus**.
10. **Dark theme and light theme** for the above.

---

## 7. Constraints & non-goals

- **Platform:** desktop (Windows/macOS/Linux) via Avalonia + Fluent-style theming. Favour
  standard control metaphors (buttons, search box, list, toggle, popover) that map to a
  desktop toolkit — avoid web-only patterns that won't translate.
- **No backend/data changes implied** — the available options come from the model; you don't
  design where the list comes from, only how it's presented and chosen.
- Out of scope: the navigation pane / overall Settings taxonomy, model selection itself,
  and any non-language settings.
- Don't design for touch; this is mouse + keyboard.

---

## 8. Deliverables

1. **Hi-fi mockups** for both surfaces, covering every state in §6, in **dark + light**.
2. **Interactive prototype** demonstrating J1–J5 from §4, with the click/keystroke count
   annotated for each.
3. The **toggle ⇄ full-picker** transition shown explicitly.
4. A short **rationale** (a few sentences) per key decision, focused on the convenience goal.
5. **Redlines/spacing** sufficient for engineering to build (sizes, paddings, the accent
   colour for active/selected, empty/disabled treatments).

### Acceptance criteria
- A first-time user can understand current source→target **at a glance** on both surfaces.
- Toggling translation is **one action**.
- Changing the target on a hundreds-long list takes **≤ 2 actions + typing** and never
  requires scrolling a wall of items blindly.
- The 2-option case uses a **toggle**, not a list.
- "Translation unavailable" is **explained**, not silently missing.
- Works visually in **dark and light** themes.

---

## 9. Prior art (reference only — do not copy)

Two previous iterations live in the repo for context on what we tried:

- `plans/2026-05-25-language-selection/` — first take: stacked source/target pickers,
  engine-aware option lists, a 5-item "recent languages" MRU. (Keyboard-layout source was
  deferred there; it is now an explicit requirement.)
- `plans/2026-05-31-language-settings-ux-redesign/` — second take: a `[Source] → [Target]`
  button row where the **arrow itself toggled translation**, with an inline shared picker
  (search + list + "Recent") expanding below, and the Whisper "translate to English"
  behaviour folded into the unified target picker.

We were **not satisfied** with either. Please start fresh and propose your own structure —
borrow only what genuinely serves the **convenience** and **slick design** goals above.
