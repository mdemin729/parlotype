# Specification — Language UX Rebuild

> Derived from the hi-fi prototype `tmp/parlotype-language.html` + `tmp/parlotype-language-spec.md`
> (designer response to [`../2026-06-01-language-settings-redesign/design-brief.md`](../2026-06-01-language-settings-redesign/design-brief.md)).
> This document restates the prototype's behaviour as an engine-agnostic specification that
> the .NET/Avalonia implementation must satisfy. It supersedes the UX defined in ADR-035.

## 1. Mental model

A single left-to-right **relationship**, read as one sentence:

> **You speak `[Source]` → Parlotype types `[Target]`.**

- The **connector** between Source and Target *is* the translation on/off control.
- Glyph encodes state: **`→`** (accent, translation **on**) ⇄ **`=`** (muted, translation **off**) ⇄
  **`=` locked / 50 % opacity** (translation **unavailable** for the active model).
- A plain-language **summary line** restates the relationship in words at rest.

Both surfaces (Settings page and Transcribe widget) use the **same grammar, glyphs, accent,
labels and iconography**. The widget is a miniature of the page, not a separate model.

## 2. Surfaces

### 2.1 Settings → Language page (authoritative)
Layout: three columns — **Source card** | **Connector** | **Target card/toggle** — followed
by the **summary line**, an optional **unavailable note**, and a transient **toast** area.
Opening a card reveals a **popover** (floating, anchored to the field, overlays content).

### 2.2 Transcribe window quick picker (Phase 2)
A compact **strip** under the record button: `source chip · connector · target chip`. The
connector toggles translation in **one click**. Tapping a chip opens a **flyout above the
widget** that leads with the **target** control (toggle or full picker) and shows **source as
a read-only secondary row** (full source editing stays in Settings).

## 3. Source side — three first-class resting states

| State | Sentinel / code | Icon | Sub-hint | Pinned |
|-------|-----------------|------|----------|--------|
| **System keyboard layout** | `keyboard` (new) | keyboard | `Detected: <layout>` (e.g. "English (US)") | top |
| **Auto-detect** | `auto` | sparkle | "Let the model identify the language" | 2nd |
| **Explicit language** | ISO code (`en`, `ru`, …) | globe | "Spoken language" | in list |

- The two specials are **pinned above** the searchable list and **hidden while a search query
  is active** (the user is hunting a language at that point).
- "System keyboard layout" is the **new default-friendly** option. Its sub-hint shows the OS
  keyboard layout resolved to a language + region name.

## 4. Target side — model decides the form

The active engine's capabilities pick one of three forms; switching engines **morphs the form
in place**:

| Form | Trigger | Rendering |
|------|---------|-----------|
| **toggle** | exactly 2 outcomes: *Disabled* + one fixed target (e.g. English) | a labelled **Switch**, no list. (Whisper, mono models) |
| **full** | arbitrary translation (many targets) | **picker button → popover**: "Off — no translation" pinned, then **Recent** cluster, then searchable list. (Gemma) |
| **none** | engine cannot translate at all | target card **disabled** + **amber inline note** naming the model + **locked connector** (`=`, 50 %). |

## 5. Naming rule (applied everywhere)

- Show **`English`** when English name == native name.
- Otherwise **`English — Native`** (e.g. `Russian — Русский`).
- ISO codes are internal only and **never surfaced** to the user.

## 6. Picker (popover) behaviour

- **Search box** appears only when the list is **long** (> 8 entries). Lists of 1–3 show no
  search box and no Recent/All grouping — just the pinned specials + rows.
- **Group labels** when searching is available: `Recent` (intersection of role MRU ∩ supported,
  hidden when empty) then `All languages`.
- **Specials + Recent** are shown only when **no query** is active.
- **Row**: leading icon tile, primary name, secondary native name (when it differs), trailing
  **check** on the selected row (accent-soft background + accent label).
- **Empty search**: centred search icon (50 % opacity) + `No languages match "x".`
- **Filter match** is case-insensitive against English name, native name, or ISO code.
- Search input is **auto-focused** on open; Escape / outside-click closes.

## 7. State machine (from the prototype `S` object)

```
S = {
  model,                       // active engine
  source: keyboard | auto | { lang, code },
  target: { on: bool, code },  // on == translation enabled
  lastTarget,                  // restored when translation is re-enabled
  recentsSource[], recentsTarget[],   // per-role MRU, max 4–5
  open: source | target | flyout | none,
  toast
}
```

### Derived rendering
- `targetMode = toggle | full | none` from the model.
- `connector = on ? '→ accent' : (mode==none ? '= locked' : '= muted')`.
- `summary = "You speak <spoken> → Parlotype types <typed>"` where `typed` is the target name,
  or `"<spoken> (no translation)"` when off/unavailable.

### Transitions
| Action | Effect | Cost |
|--------|--------|------|
| **Flip translation** (connector / switch / strip) | `on = !on`; on re-enable, restore `lastTarget` (full) or the single fixed target (toggle). Never re-asks. | **1** |
| **Change target** | open target → (filter) → pick; sets `code`, `lastTarget`, pushes target MRU. | 2 + typing |
| **Change source** | open source → pick special / (filter) → pick; pushes source MRU for explicit langs. | 2 + typing |
| **Select "Off"** in full picker | `on = false`. | 1 |

## 8. Model / engine switch fallback (state 8)

When the active engine or model changes and the current selection is no longer supported, the
app **falls back and explains via a one-line toast** rather than failing silently:

| Situation | Fallback | Toast |
|-----------|----------|-------|
| Source language not supported by new engine | → **System keyboard layout** (always valid) | "`<lang>` isn't a source in `<model>`. Using your keyboard layout." |
| Target mode becomes **none** | translation **off** | "`<model>` can't translate — output now matches your spoken language." |
| Target mode **toggle**, previous target ≠ the single option | force to the single option | "`<prev>` isn't available in `<model>`. Translation set to `<only>`." |
| Target mode **full**, previous target unsupported | reset to default (English/last) | "Previous target reset — not supported by `<model>`." |

Toasts auto-clear after a few seconds and use the accent-soft style.

## 9. Defaults that remove picking

- First time translation is enabled, the target is the **model's sensible default** (English
  for Whisper; most-recent for full models), so "first enable" needs **zero** extra selection —
  still just the single flip action.
- Re-enabling translation restores the **last-used** target.

## 10. Visual tokens (redlines, condensed from §7 of the prototype spec)

- Accent `#378ADD`; accent-pressed `#2D76C2`; accent-soft `rgba(55,138,221,.16/.12)`.
- Warn (unavailable) `#E0A23A` on `rgba(224,162,58,.14)` (dark) / `#B9791B` (light).
- Picker field min-height **60px**, padding 13×14, border 1.5px, icon tile 34×34 r8, gap 11.
- Connector pill **54×38**, 1.5px border; on = accent fill + glow; off = surface-3; locked = 50 %.
- Switch (toggle form): track **42×24**, knob 18×18.
- Popover width **300px**, list max-height **300px**, row padding 9×10.
- Selected row: accent-soft bg + accent label + check.
- Focus ring: `0 0 0 3px accent-soft` + accent border on every interactive control.
- Transcribe widget width **240px**; quick-strip surface-2 r9; flyout width **268px**, opens
  **above** the widget, list max-height **200px**.
- Both **dark and light** themes must be supported.

## 11. Engine capability map (current + forward-looking)

| Engine | Source | Target form | Fixed targets |
|--------|--------|-------------|---------------|
| **Whisper** | keyboard / auto / ~99 langs | **toggle** | English only |
| **Gemma 4** | keyboard / auto / full list | **full** | (arbitrary) |
| **Transcribe-only** (future, e.g. Parakeet) | keyboard / auto / list | **none** | — |
| **Mono** (future, single-language) | short list | **toggle** | English |
