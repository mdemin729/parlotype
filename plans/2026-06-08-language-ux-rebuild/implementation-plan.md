# Implementation Plan — Language UX Rebuild

Evolves the ADR-035 implementation to match the prototype. Two phases:
**Phase 1 = Settings → Language page**, **Phase 2 = Transcribe window quick picker**.
A small shared refactor (P0) precedes both so the two surfaces stay consistent.

Legend: 🟢 Core · 🔵 Platform · 🟣 Desktop · 🧪 Tests

---

## Phase 0 — Shared domain + capability model (foundation)

Goal: model the three target **forms**, the **keyboard-layout** source, and a single shared
language-relationship surface both windows consume.

1. 🟢 **`LanguageCatalog`** — add `KeyboardLayoutCode = "keyboard"` sentinel + `IsKeyboardLayout(code)`.
2. 🟢 **`LanguageCapabilities`** — add a derived `TranslationForm` enum (`None | Toggle | Full`):
   - `SupportsArbitraryTranslation` ⇒ `Full`
   - `FixedTranslationTargets.Count == 1` ⇒ `Toggle`
   - else ⇒ `None`.
   Add a forward-looking `SpeechEngineCapabilities` branch comment for a transcribe-only engine
   (no engine wired; capability path exists for `None`).
3. 🟢 **`IKeyboardLayoutService`** (new, `Core/Speech` or `Core/Input`) →
   `KeyboardLayoutInfo? Detect()` returning `(LanguageCode, FriendlyName)` (e.g. `("en",
   "English (US)")`). *(ADR trigger: new Core interface.)*
4. 🔵 **`Win32KeyboardLayoutService`** — P/Invoke `GetKeyboardLayout` + `GetKeyboardLayoutName`
   / `GetLocaleInfoEx`; map primary lang id → `CultureInfo` → code + friendly name. Non-Windows
   returns `null`. Register in `PlatformServiceExtensions.cs`. *(ADR triggers: P/Invoke,
   PlatformServiceExtensions, OS-conditional behaviour.)*
5. 🟣 **`LanguageRelationshipViewModel`** (new shared VM) — owns: engine capabilities, source
   state (keyboard/auto/lang), target `{on, code}`, `lastTarget`, per-role MRU, persistence,
   the connector/glyph/summary derivations, and the **fallback-on-switch** logic. The settings
   page VM and the transcribe VM both delegate to it. (Replaces the monolithic logic currently
   inside `LanguageSelectionSettingsViewModel`.)
6. 🔵 **`AudioPipelineService.CacheSettingsAsync`** — resolve `source == keyboard` via
   `IKeyboardLayoutService` to the detected code (fallback auto/none) before building
   `WhisperOptions` / Gemma prompt. *(ADR trigger: audio pipeline.)*
7. 🧪 `LanguageCapabilitiesTests` (form derivation), `LanguageCatalogTests` (keyboard sentinel),
   a fake `IKeyboardLayoutService` for VM/pipeline tests.

**Verify:** `dotnet test src/Parlotype.Tests` green; capability/form table matches §Spec 11.

---

## Phase 1 — Settings → Language page

8. 🟣 **`LanguageDisplayItem`** — enrich: `IconKind` (keyboard/sparkle/globe/off), optional
   `SecondaryText` (native name / special sub-hint), `IsSpecial`, keep `IsRecent`/`IsSelected`.
9. 🟣 **`LanguageRowFactory`** — support **multiple leading specials** (keyboard + auto) with
   icons + sub-hints; emit group labels (`Recent`, `All languages`); hide search/grouping for
   short lists (≤ a threshold matching `> 8` rule); produce target "Off — no translation" row.
10. 🟣 **`LanguagePickerView`** — convert from inline-expand to a **floating popover**
    (`Popup`/`Flyout` anchored to the field, 300px, overlay). Conditional search box, rich
    rows (icon tile + native subname + check), `Recent`/`All` headers, empty-state. Auto-focus
    search; Escape/outside-click close.
11. 🟣 **Target form switching** in the page view/VM:
    - `Toggle` → a labelled `ToggleSwitch` bound to translation-on (replaces the Whisper target
      picker button).
    - `Full` → picker button → popover.
    - `None` → disabled target card + amber note + locked connector.
12. 🟣 **Connector** — glyph swap `→`/`=` with accent-on / muted-off / locked styles
    (`Classes.on` / `Classes.off` / `Classes.locked`), matching redlines.
13. 🟣 **Summary line** + **toast** region on the page; bind to relationship VM derivations.
14. 🟣 **`LanguageSelectionSettingsViewModel`** — slim down to a thin wrapper over
    `LanguageRelationshipViewModel`; wire `UpdateForEngine` / `UpdateTranslationAvailability`
    + the fallback toasts from `SettingsWindowViewModel` model/engine change hooks.
15. 🧪 Desktop headless tests: source two-specials + sub-hints; target form per engine
    (toggle/full/none); connector glyph/locked; popover search/Recent/empty; summary text;
    switch-fallback toasts. Add/refresh `LanguageSettingsScreenshotTests` (dark + light).

**Verify:** `dotnet test` green; manual run of all §Spec 6/8 states; dark + light screenshots.

---

## Phase 2 — Transcribe window quick picker

16. 🟣 **`TranscribeViewModel`** — inject/consume `LanguageRelationshipViewModel`; expose
    `SourceShort`, `TargetShort`, `TranslationOn`, `TargetForm`, `ToggleTranslationCommand`,
    `OpenLanguageFlyoutCommand`; keep recording/audio untouched.
17. 🟣 **`TranscribeWindow.axaml`** — add the **quick-picker strip** under the record button
    (`source chip · connector · target chip`); grow window height to fit; strip styles
    (`on`/`off`/`locked`) mirroring the page connector.
18. 🟣 **Widget flyout** — a `Flyout`/`Popup` opening **above** the widget (≈268px), header
    "Translate to", **target** control (reuse Phase 1 toggle/full picker), **source read-only
    row** that routes to Settings. Reuse `LanguagePickerView`.
19. 🟣 Glanceable-at-rest verification; ensure topmost flyout renders above other apps.
20. 🧪 Desktop headless tests: strip reflects relationship; connector toggles translation;
    flyout opens, filters, selects target; source row routes to Settings. Screenshot scenarios
    for the strip (on/off/locked, dark + light).

**Verify:** `dotnet test` green; manual widget run for J1/J2/J5; consistency with the page.

---

## Cross-cutting: docs, ADR, memory (Definition of Done)

21. **ADR-036** `docs/decisions/036-language-ux-rebuild.md` — supersedes ADR-035; records: the
    three target forms, the keyboard-layout source + `IKeyboardLayoutService` + Win32 P/Invoke,
    the shared `LanguageRelationshipViewModel`, the popover migration, and switch-fallback
    toasts. References ADR-021/033/034/035. Mark ADR-035 `superseded` → 036.
22. **Memory vault** — update `memory/services/core.md` (new interface + catalog/capability
    symbols), `memory/services/platform.md` (Win32 keyboard service), `memory/services/desktop.md`
    (shared relationship VM, popover picker, widget strip), `memory/architecture/subsystems.md`
    (Language & Translation section), `memory/decisions/_index.md` (ADR-036 row).
23. **Knowledge** — capture non-derivable facts (e.g. Win32 keyboard-layout → culture mapping
    quirks; Avalonia popover-above-topmost-window behaviour) under `memory/knowledge/` if found.
24. **Plans** — flip frontmatter to `in_progress`/`completed`; update `plans/INDEX.md`.

## Risks / decisions baked in

- **Keyboard detection** is **Windows-only now** with a graceful null path elsewhere (confirmed).
- **Transcribe-only ("none") state is built now**, capability-driven, though no engine triggers
  it yet (confirmed).
- **Popover** presentation (not inline) for the Settings pickers (confirmed) — main Avalonia
  risk is anchoring/overlay + the topmost flyout on the widget; spike early in Phase 1/2.
- The shared `LanguageRelationshipViewModel` is the linchpin for surface consistency; build it
  in Phase 0 before either UI.

## Suggested commit slices

1. P0 Core (catalog/capabilities/interface) + tests.
2. P0 Platform (Win32 service + pipeline resolution) + tests.
3. P0 shared `LanguageRelationshipViewModel` + tests.
4. P1 picker popover + rich rows.
5. P1 page (forms/connector/summary/toast) + tests + screenshots.
6. P2 transcribe strip + flyout + tests + screenshots.
7. ADR-036 + memory vault + INDEX.
