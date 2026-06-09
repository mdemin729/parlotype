# Requirements — Language UX Rebuild

Traceability: each requirement maps to the prototype spec (`tmp/parlotype-language-spec.md`)
and the design brief states §6 (S1–S10) / journeys J1–J5.

## Functional requirements — Phase 1 (Settings → Language page)

### Source
- **FR-S1** Source picker offers three first-class states: **System keyboard layout**,
  **Auto-detect**, and an **explicit language** from the engine's source list.
- **FR-S2** *System keyboard layout* (new `keyboard` sentinel) is pinned at the top and shows
  a sub-hint with the **detected OS layout** (e.g. "Detected: English (US)"). On platforms
  where detection is unavailable, the sub-hint degrades gracefully (generic text) and the
  resolved language falls back to Auto/none without error.
- **FR-S3** *Auto-detect* is pinned second; both specials are **hidden while a search query is
  active**.
- **FR-S4** The source **button** reflects the resting state with icon + name + sub-hint.

### Target
- **FR-T1** The target side renders one of three **model-driven forms**: `toggle`, `full`, or
  `none` (§Spec 4).
- **FR-T2** `toggle` form (exactly *Disabled* + one fixed target) renders a **Switch**, not a
  list (covers Whisper → English). *(brief acceptance: 2-option case is a toggle.)*
- **FR-T3** `full` form renders the picker button → popover with **Off**, **Recent**, then the
  searchable list (covers Gemma 4).
- **FR-T4** `none` form **disables** the target card, shows an **amber inline note** naming the
  model, and **locks the connector** to `=`. *(brief acceptance: unavailable is explained.)*

### Connector / translation toggle
- **FR-C1** The connector is the **single translation on/off control** (J2 = **1 action**).
- **FR-C2** Glyph swaps `→` (on, accent) ⇄ `=` (off, muted) ⇄ `=` locked (unavailable).
- **FR-C3** Re-enabling translation restores the **last-used** target; first enable picks the
  model default (English / most-recent) with **zero** extra selection.

### Picker
- **FR-P1** Presentation is a **floating popover** anchored to the field, overlaying content.
- **FR-P2** Search box appears only when the list is **long (> 8)**; 1–3 item lists show no
  search and no grouping.
- **FR-P3** Rows show icon + name + native subname (when differing) + selected **check**;
  group labels **Recent** / **All languages** when searching is available.
- **FR-P4** Empty filter shows `No languages match "x".`; match is case-insensitive over
  English name / native name / ISO code.
- **FR-P5** Per-role **MRU** (source vs target) drives the Recent cluster (existing keys
  `RecentSourceLanguages` / `RecentTargetLanguages`).

### Summary + fallback
- **FR-M1** A **summary line** under the cards reads "You speak X → Parlotype types Y" (or
  "…(no translation)").
- **FR-M2** Engine/model switches that invalidate a selection **fall back and emit a one-line
  toast** per §Spec 8 (source → keyboard layout; target → off/forced/reset).

### Persistence + pipeline
- **FR-D1** Selections persist via existing keys plus the new `keyboard` source sentinel;
  `TranslationEnabled` remains the single source of truth for translation.
- **FR-D2** The audio pipeline resolves `source = keyboard` to the detected layout language
  code (fallback: auto/none) when building `WhisperOptions` / the Gemma prompt.

## Functional requirements — Phase 2 (Transcribe window quick picker)

- **FR-W1** A **quick-picker strip** under the record button shows `source · connector ·
  target` and reflects the current relationship **at rest** (J1 = 0 actions).
- **FR-W2** The strip connector toggles translation in **one click** (J5), with the same
  glyph states as the page.
- **FR-W3** Tapping the source or target chip opens a **flyout above the widget** that leads
  with the **target** control (toggle/full picker reusing the Phase 1 component) and shows the
  **source as a read-only secondary row** (editing source directs to Settings).
- **FR-W4** Changing the translation language from the widget takes **≤ 2 actions + typing**
  and never requires blind scrolling (search/Recent reused).
- **FR-W5** The widget and settings page stay **consistent** (same labels, glyphs, accent,
  iconography); the widget grows only as needed to host the strip and the flyout is wider than
  the widget (≈268px).

## Non-functional requirements

- **NFR-1** Zero-warning build (`TreatWarningsAsErrors`); `dotnet test` green.
- **NFR-2** Avalonia conventions: `x:CompileBindings`, `x:DataType`, `[ObservableProperty]` /
  `[RelayCommand]`, partial VMs; background→UI mutations dispatched to `Dispatcher.UIThread`.
- **NFR-3** Interfaces in **Core**, implementations in **Platform**, registered in
  `PlatformServiceExtensions.cs` (singletons).
- **NFR-4** Keyboard-layout detection is a **Core interface** with a **Windows** implementation
  now and a **null/graceful** path on macOS/Linux (no exceptions, no crash).
- **NFR-5** Works in **dark and light** themes.
- **NFR-6** Shared language-relationship logic is **not duplicated** between the settings page
  and the transcribe window.

## Acceptance criteria (from the brief)

- AC-1 First-time user understands current source→target **at a glance** on both surfaces.
- AC-2 Toggling translation is **one action** (page connector, strip connector, switch).
- AC-3 Changing target on a hundreds-long list ≤ **2 actions + typing**; no blind scroll.
- AC-4 The **2-option case uses a toggle**, not a list.
- AC-5 **"Translation unavailable" is explained**, not silently missing.
- AC-6 Renders correctly in **dark and light** themes.
- AC-7 Model switch that invalidates a selection **falls back and informs** the user.

## Out of scope

- New recognition engines/models or pipelines (capability path is built; no transcribe-only
  engine is wired up).
- Cross-platform (macOS/Linux) keyboard-layout detection beyond a graceful fallback.
- Settings navigation taxonomy / model selection UI / non-language settings.
- Touch interaction (mouse + keyboard only).
