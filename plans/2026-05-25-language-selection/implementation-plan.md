# Implementation Plan — Source & Target Language Selection

This is a phased plan. Each phase is independently buildable/testable. Phases 0–1 deliver the
Whisper source-language story; Phases 2–4 deliver Gemma 4 translation + UI. Phase 5 closes out
tests and docs.

## Current state (grounding facts)

- `WhisperOptions` (`src/Parlotype.Core/Speech/WhisperOptions.cs`) **already** has `Language`
  (default `"auto"`) and `TranslateToEnglish`. The gap: `AudioPipelineService.CacheSettingsAsync()`
  (`src/Parlotype.Platform/Audio/AudioPipelineService.cs`, ~line 404) **hard-codes** `Language = "auto"`.
- `TranscriptionResult.DetectedLanguage` already returns Whisper's detected language.
- Whisper.net builder: `.WithLanguage(code)` and `.WithTranslate()` — **translate is to English only**.
- Gemma 4: `PromptTemplate.Render(language)` substitutes a `{language}` token (ADR-030 seam);
  served by `LlamaCppSpeechRecognizer` (`src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs`).
- Engine routing: `SpeechEngine` enum, `SpeechRecognizerFactory`, `DelegatingSpeechRecognizer`.
- Post-processing seam: `ITextProcessor` / `TranscriptionTextProcessor` (`src/Parlotype.Platform/Speech/`).
- Settings: `JsonSettingsService` serializes any type, so a `List<string>` MRU works.

## Engine capability matrix

| Pipeline | Source language | Target language | Status |
|---|---|---|---|
| 1. Whisper → text | Auto or explicit (~99) | Same as source **or** English | Source-select: plan now; EN-translate exists |
| 2. Whisper → LLM (no ASR) → text | from Whisper | Any LLM-supported | Future |
| 3. LLM-with-ASR (Gemma 4) | Auto or explicit | Any LLM-supported via prompt | Plan now |

---

## Phase 0 — Core contracts (`Parlotype.Core`)

**Language catalog**
- `LanguageInfo` record: `Code` (ISO 639-1, e.g. `"en"`), `NativeName`, `EnglishName`.
- `LanguageCatalog` static helper:
  - **Whisper set** — curated static list of Whisper's ~99 supported languages (codes + names).
    Whisper's set is fixed and not exposed by the library, so it must be hand-curated.
  - **Full fallback list** — built from
    `System.Globalization.CultureInfo.GetCultures(CultureTypes.NeutralCultures)`
    (ISO codes + `NativeName` / `EnglishName`) for engines that don't declare a fixed set.
  - Lookup by code; safe handling of unknown/`auto`.

**Capability surface (engine-aware)**
- `LanguageCapabilities` (record or interface property) describing:
  - `SupportsAutoDetect` (bool),
  - `SupportedSourceLanguages` (`IReadOnlyList<LanguageInfo>?`; `null` ⇒ use full list),
  - `SupportsArbitraryTranslation` (bool) and/or `SupportedTargetLanguages`.
  - **Whisper:** `SupportsAutoDetect = true`, source = Whisper set, targets = `{ source, English }`.
  - **Gemma 4:** `SupportsAutoDetect = true`, source = full list, `SupportsArbitraryTranslation = true`.
- Expose capabilities via `ISpeechRecognizer` (new read-only member) **or** a small
  `ISpeechEngineCapabilities` resolved per `SpeechEngine`. Prefer the latter to avoid forcing
  every recognizer to know about UI concerns.

**Translation intent**
- Add a `TranslationMode` enum: `None` | `English` | `TargetLanguage`, **or** model it as a
  nullable `SelectedTargetLanguage` (where `null` ⇒ no translation, `"en"` on Whisper ⇒ existing
  `TranslateToEnglish`). Pick one during implementation; the enum is clearer for UI binding.

**Settings keys** (`src/Parlotype.Core/Settings/SettingsKeys.cs`)
- `SelectedSourceLanguage` (string; `"auto"` default).
- `SelectedTargetLanguage` (string) **or** `TranslationMode` (enum-as-string).
- `RecentLanguages` (serialized `List<string>`, max 5).

> **ADR trigger:** new Core records/enums + behavior diverging by engine ⇒ ADR required at
> implementation time (extends ADR-021/030/033).

---

## Phase 1 — Whisper source-language wiring (`Parlotype.Platform`)

- `AudioPipelineService.CacheSettingsAsync()` (~line 404): read `SelectedSourceLanguage` from
  settings and assign to `WhisperOptions.Language` instead of the hard-coded `"auto"`.
- Keep `TranslateToEnglish` for the English target; it is already gated by `SupportsTranslation`
  (ADR-033). When `TranslationMode == English` and the model supports it, set the flag.
- No change needed to `WhisperSpeechRecognizer` itself — it already honors `options.Language`
  and `options.TranslateToEnglish`.

---

## Phase 2 — Gemma 4 target-language translation (`Parlotype.Platform`)

- Thread the selected source/target language into `PromptTemplate.Render(...)` for
  `LlamaCppSpeechRecognizer`:
  - Source language → existing `{language}` token (transcription hint).
  - Target language → translation instruction. For Phase 2, do it **inline in the ASR prompt**
    (single Gemma call: "transcribe, then translate to {target}"), avoiding a second round-trip.
- Reserve a **separate `ITextProcessor` translation stage** for the future Whisper→LLM pipeline
  (Pipeline 2) rather than building it now.
- Surface Gemma's language capabilities (arbitrary target = true) via the Phase 0 capability surface.

---

## Phase 3 — Recent-languages MRU (`Parlotype.Core` / `Parlotype.Platform`)

- Small helper (e.g. `RecentLanguagesService`, or an inline utility) backed by
  `SettingsKeys.RecentLanguages`:
  - push-to-front on selection, dedupe by code, cap at **5**.
  - read on picker open to pin recents at the top.
- Unit-test the push/dedupe/cap logic.

---

## Phase 4 — Desktop UI (`Parlotype.Desktop`)

Follow the existing model-picker pattern:

- **ViewModel:** `LanguageSelectionSettingsViewModel` (mirror `WhisperModelSettingsViewModel`):
  - `SourceLanguageOptions` and `TargetLanguageOptions` collections.
  - Load saved source/target from settings; apply selection; persist via `ISettingsService`.
  - **Engine-aware:** subscribe to / read the active `SpeechEngine`; when Whisper, restrict target
    options to `Default` / `English` and grey out the rest (reuse the `RestrictToEngine` pattern).
  - Pin the 5 recent languages at the top, then the full / engine-filtered list.
- **Display item:** `LanguageDisplayItem` wrapper (mirror `WhisperModelDisplayItem`):
  observable `IsSelected`, embedded `SelectCommand`, exposes `Code` / `DisplayName`.
- **View:** `LanguageSelectionSettingsView.axaml` (mirror `WhisperModelSettingsView.axaml`):
  `x:CompileBindings="True"`, `x:DataType`, `ItemsControl` of buttons, accent bar on selection.
- Register the section in the settings window navigation (DataTemplate mapping +
  `SettingsSectionViewModelBase` subclass).

---

## Phase 5 — Tests & docs

- **Core/Platform tests** (`Parlotype.Tests`):
  - `LanguageCatalog` (Whisper set lookups, full-list fallback, unknown-code handling).
  - Capability matrix per engine (Whisper targets limited; Gemma arbitrary).
  - MRU push/dedupe/cap.
  - `CacheSettingsAsync` maps `SelectedSourceLanguage` → `WhisperOptions.Language`.
- **Desktop.Tests** (`Parlotype.Desktop.Tests`, headless):
  - picker viewmodel: selection persists (mock `ISettingsService`); engine-aware target filtering;
    recents pinned on top.
- **ADR(s):** new Core contracts + per-engine behavior divergence (reference ADR-021/030/033).
- **Memory vault:** update `memory/services/Parlotype.Core.md`, `Parlotype.Platform.md`,
  `Parlotype.Desktop.md`; add ADR row to `memory/decisions/_index.md`; update
  `memory/architecture/subsystems.md` (speech/translation section).

---

## Future (documented, not built)

- **Keyboard-layout source detection:** read current OS keyboard layout (Win32 `GetKeyboardLayout`)
  as a source-language hint; platform-specific, behind the existing engine abstraction.
- **Pipeline 2 (Whisper → LLM → any language):** add a dedicated translation `ITextProcessor`
  that calls the shared llama-server host (ADR-027) after Whisper transcription. Lets non-ASR LLMs
  translate Whisper output to arbitrary targets.
- **Cloud LLM translation providers:** additional opt-in providers (BYOK), per ADR-032 posture.

## Critical files

- `src/Parlotype.Core/Speech/WhisperOptions.cs`, `TranscriptionResult.cs`, `SpeechEngine.cs`, `PromptTemplate.cs`
- `src/Parlotype.Core/Settings/SettingsKeys.cs`
- `src/Parlotype.Platform/Audio/AudioPipelineService.cs` (`CacheSettingsAsync`, ~line 404)
- `src/Parlotype.Platform/Speech/WhisperSpeechRecognizer.cs`, `LlamaCppSpeechRecognizer.cs`, `SpeechRecognizerFactory.cs`
- `src/Parlotype.Desktop/ViewModels/Settings/WhisperModelSettingsViewModel.cs` + `Views/Settings/WhisperModelSettingsView.axaml` (UI pattern to copy)
- ADRs: `docs/decisions/` 021 (Whisper→EN translation), 025 (Gemma 4), 030 (configurable prompts / `{language}` seam), 032 (online providers), 033 (translation capability per model)
