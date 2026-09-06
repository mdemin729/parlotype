---
title: UI localization (Russian, Spanish, and a repeatable path to more)
status: in_progress
created: 2026-09-05
started: 2026-09-05
completed:
---

# UI localization

## Problem

Parlotype's interface is English-only and its copy is hardcoded. ADR-056 externalized
30 keys for the onboarding tour and Help section into `Resources/Strings.resx`, and left
a note that translations would arrive later as satellite resx files. Everything else —
211 literal attributes across 27 `.axaml` files, ~200 literal strings in 48 view models,
18 settings-section titles, the tray menu, dialogs and toasts — is still baked into
markup and C#.

We want Russian and Spanish first, then German, French and others. That second sentence
is the harder requirement: the ninth language must cost a translation file and nothing
else. Adding it must not require touching markup, view models, or the settings UI, and
a UI change made months from now must not be allowed to silently leave eight locales
stale.

Two constraints from the current code make this non-trivial:

1. **`Strings.cs` is hand-written.** Fine for 30 keys, unmaintainable at ~450. It needs
   to be generated, and the generation must survive `TreatWarningsAsErrors` on a plain
   CLI build (the reason ADR-056 avoided the designer in the first place).
2. **`x:CompileBindings="True"` is mandatory and `{ReflectionBinding}` is banned**
   (CLAUDE.md). Live language switching wants a binding to a localizer indexer, which
   is exactly the shape the ban targets. This needs an explicit, narrow exemption or a
   restart-required design — a decision, not an implementation detail.

## Approach

**Resource model.** Keep resx + satellite assemblies: `Strings.resx` (neutral, English),
`Strings.ru.resx`, `Strings.es.resx`, one file per added language. Keys stay flat and
area-prefixed (`Settings_Theme_Title`, `Transcribe_Status_Recording`), matching the
`Onboarding_*` / `Help_*` convention already in the file. Interpolated copy becomes
`string.Format` with numbered placeholders so word order can move — Russian and German
both need that.

**Accessor.** `Strings.cs` becomes generated output of `scripts/gen-strings.ps1`, run
manually and verified by a test that regenerates in memory and compares. Deterministic,
no MSBuild code generation, no warnings-as-errors exposure.

**Culture selection.** New `SettingsKeys.UiLanguage`, values `system` (default) or a
culture name (`ru`, `es`). Resolved once at startup before the first window is built:
`system` reads `CultureInfo.InstalledUICulture` and falls back down the chain
`ru-RU → ru → neutral English` — resx satellite lookup already does this, so an
unsupported OS language lands on English for free. `CurrentUICulture` drives copy;
`CurrentCulture` stays on the OS regional setting so dates and numbers keep following
Windows, not the interface language.

**Markup.** A `{loc:Tr Settings_Theme_Title}` markup extension, used everywhere in AXAML.
Its internals are the one place that decides static-vs-live; the markup never changes if
that decision is revisited. Recommended: it returns a binding to a `Localizer` singleton
indexer, giving live switching with no restart — a scoped, documented exemption from the
`{ReflectionBinding}` ban rather than a general loosening. See
[implementation-plan.md](implementation-plan.md) for the alternative and the trade-off.

**Guardrails before the work they guard.** The parity script, the parity test and the
Claude Code hook land in Phase 2 — before the ~450-key extraction and before any
translation exists — so they grow correct automatically as locales are added instead of
being retrofitted onto a stale corpus.

**Scope boundary.** Log messages, exception messages, engine and model identifiers
(`Whisper`, `Parakeet TDT v3`, `Vulkan`, `gpt-4o-mini-transcribe`), file paths and
benchmark CLI output stay English — they are developer- or protocol-facing. Speech
source/target language names are localized by ICU (`CultureInfo.DisplayName`), never by
hand: ~99 names × N locales is not a translation task we should own.

## Workplan

Six phases, each becoming its own `plans/` folder when started. Phases 4 and 5 are
independent of each other; everything else is sequential.

- [x] **Phase 1 — Foundation.** *Done 2026-09-05 — [ADR-064](../../docs/decisions/064-ui-localization-foundation.md).*
      `UiLanguageService` + `Localizer`/`LocalizedString` + `{loc:Tr}`, `UiLanguage` setting
      with system detection and drop-a-language fallback, `SupportedUiLanguages` registry in
      Core, Settings → Appearance → Interface language, `scripts/gen-strings.ps1`, Theme
      section migrated as the pilot, `WaitTimeOption` display copy moved out of Core.
      Also shipped `Strings.ru.resx` / `Strings.es.resx` covering all 45 current keys, so
      "every key exists in every locale" holds from the start rather than being retrofitted
      in Phase 4/5 — those phases now fill in, they do not create.
- [x] **Phase 2 — Guardrails.** *Done 2026-09-05.* `scripts/check-localization.ps1`
      (key parity, placeholder parity, hardcoded-literal scan against a shrink-only
      baseline in `scripts/localization-baseline.json`); `LocalizationParityTests`
      mirroring it in xUnit — plus generated-accessor freshness and a
      verbatim-English-translation check — so `dotnet test` and the release gate enforce
      it, not just Claude sessions; `.claude/skills/localization/SKILL.md`;
      `PostToolUse` + `Stop` hooks in a new checked-in `.claude/settings.json` via
      `scripts/hooks/localization-guard.ps1`; CLAUDE.md Localization section and a sixth,
      non-deferrable Definition-of-Done item; memory vault updated.
      No separate CI step: the repo has no general workflow, and `release.yml`'s existing
      `dotnet test` gate now covers all three checks.
- [x] **Phase 3 — String extraction. Complete.** Started at 183 AXAML literals across
      22 files and finished at **0**. `scripts/localization-baseline.json` now has an empty
      `pendingFiles`, so a new hardcoded literal in any `.axaml` fails the check outright
      instead of being measured against a debt allowance. 331 keys × 3 languages.
      - [x] **3a widget/tray/dialogs** — *2026-09-05*, 38 keys. `TranscribeWindow` (7 → 0),
        the `StatusText` machine and cloud error/badge strings in `TranscribeViewModel`,
        `App.axaml` tray menu (3 → 0), `ModelDownloadDialog` + `ModelDownloadViewModel`
        (interpolated prompts → composite formats). Tray headers bind to new `AppViewModel`
        label properties re-raised on `Localizer.CultureChanged` — `NativeMenu` is built
        once at load, so no binding reaches it otherwise.
      - [x] **3b settings shell + simple sections** — *2026-09-06*, 97 keys. Window title
        and nav heading, all 5 category headers, **all 16 remaining section titles** (so the
        left pane is fully translated even where a page body is not yet), and the
        Microphone, Silence timeout, Startup, Updates, Data, Whisper runtime and Hotkeys
        pages. The Runtime "restart required" note was five `<Run>`s with two bound runtime
        names interleaved — a sentence no translator can reorder — and became one composite
        format on a new `RestartRequiredNote`, at the cost of the bold on those two names.
        Both scanners also learned `OnContent`/`OffContent`, which nothing had been checking.
      - [x] **3c speech-engine sections** — *2026-09-06*, 119 keys in two passes. First the
        model/engine pages (Engine + its five engine cards, Whisper model, Whisper output,
        Gemma 4, Parakeet, `ApiKeyBox`), then the three big ones: llama.cpp (42 → 0),
        Prompts (28 → 0), Cloud providers (23 → 0). The Prompts help text had literal
        `{speech_lang}` / `{text_lang}` tokens interleaved with prose across seven `<Run>`s;
        where a token leads its sentence the Runs stay (bold token, translatable prose),
        and the one with three tokens mid-sentence collapsed to a single string carrying
        the tokens inline. Model catalog names and disk sizes stayed English — identifiers,
        not prose.
      - [x] **3d Language section** — *2026-09-06*, 33 keys. The 9 remaining AXAML literals
        plus `LanguageRelationshipViewModel`'s composed sentences, including the nested
        summary (a paused-suffix format substituted into the "you speak X, types Y" format)
        and all six toasts. Every slot that takes a **language name** was shaped so it needs
        no grammatical case — leading the sentence, or after a colon or dash — which is
        what makes them survivable when phase 6 localizes those names; the one exception is
        documented in [terminology.md](terminology.md).
        `Transcribe_ChooseTranslatingModel` became `Language_ChooseTranslatingModel`:
        ADR-061 puts that button on two surfaces, so it gets one key that cannot drift.
- [x] **Phase 4 — Russian.** *Done 2026-09-06.* Every key was translated in the same change
      that extracted it, so `Strings.ru.resx` was never behind. The review is done too:
      `LocalizedLayoutReviewTests` renders all 19 settings pages at the real content width
      (900 − 200 nav − 48 padding) in every shipped language and writes PNGs to
      `reports/localized-layout/`, and the Russian set was read page by page.
      **No clipping anywhere** — the pages are built from wrapping `StackPanel`s with no
      fixed label widths, so ~35 % expansion just reflows. No layout fixes were needed,
      which is a property of the existing markup rather than luck worth assuming next time.
      Terminology recorded in [terminology.md](terminology.md).
- [x] **Phase 5 — Spanish.** *Done 2026-09-06.* Same: `Strings.es.resx` complete, rendered
      and reviewed, no clipping.
- [ ] **Phase 6 — Polish & playbook.** Plus two clusters of copy that live in
      `Parlotype.Core`, where there are no resources, and so need a presentation move into
      Desktop rather than a resx entry:
      **(a) hotkey presentation** — *done 2026-09-06, and it needed no Core change.*
      New `HotkeyText` in Desktop formats the gesture and the mode badge from resx, while
      Core's `DisplayString` stays as the **invariant** form the logs write: a log that
      changes language with the UI is a log you cannot grep, so the two are deliberately
      different things. Key names ("Ctrl", "Space") are never translated — they are what is
      printed on the keyboard — but the side is a *format*, not a prefix, because Spanish
      puts it after the key ("Ctrl derecho") and Russian wants the genitive
      ("правого Ctrl"). Still open in this cluster: **`HotkeyConflictDetector`'s conflict
      sentences** and its ~16 reserved-shortcut descriptions ("Lock workstation", "Open File
      Explorer"). Those genuinely need a reason enum in Core, so they are the one part that
      warrants an ADR amendment. *Done 2026-09-06 — see below.*
      **(b) cloud exception message bodies** — *done 2026-09-06*, same shape as (a).
      `CloudProviderNotConfiguredException` / `CloudSpeechTranscriptionException` now carry
      the reason (`CloudConfigurationError`, `CloudBaseUrlError`, the engine, the HTTP status
      and the provider's own parsed text) instead of only a finished sentence, and a new
      `CloudErrorText` in Desktop words it. The base-URL hint in Cloud providers settings
      came along: `CloudBaseUrlValidator` returns a `CloudBaseUrlFailure` rather than an
      English fragment.

      **The Core change both clusters needed** — [ADR-064 amendment](../../docs/decisions/064-ui-localization-foundation.md),
      2026-09-06, 36 keys, 376 total. `HotkeyConflictReason` + `ReservedShortcut` in
      Hotkeys, `CloudConfigurationError` + `CloudBaseUrlError` + `CloudBaseUrlFailure` in
      Speech; the English sentences stay in Core as the **invariant** form the logs write,
      exactly like `HotkeyGesture.DisplayString`. Two rules fell out and are now in the
      skill: every `Cloud_*` format opens with the provider slot so no language has to
      inflect a name it is handed verbatim, and each of these enums needs a test that loops
      it — resx parity cannot catch a member added without its key, because the key is
      missing from every language at once and the languages therefore still agree.
      `LocalizedLayoutReviewTests` grew four warning states (three hotkey conflicts, a
      rejected base URL); reading them caught the Russian «в режиме «Вкл./выкл.».», where
      the badge abbreviation's period collided with the sentence's.

      The originally planned polish also remains: pseudo-locale (`qps-ploc`) for catching
      stragglers and truncation, ICU-localized speech-language names, a
      `docs/localization.md` recipe for adding German/French, and a website
      language-support note. **Localizing the language names is not a free change** — it
      alters what gets substituted into ~6 keys, and
      `Language_ToggleSwitch_TranslateToFormat` is the one slot in Russian that then needs
      a case other than nominative. Table in [terminology.md](terminology.md).

## Decisions (resolved in Phase 1, ADR-064)

1. **Live switch, and no `{ReflectionBinding}` exemption after all.** Avalonia 12 renamed
   `Binding` to `ReflectionBinding` and added `CompiledBinding.Create<TIn, TOut>(expr, source)`.
   Giving each key its own `LocalizedString` object makes the binding expression a plain
   property access (`s => s.Value`), so `{loc:Tr}` is a **compiled** binding and the
   CLAUDE.md rule stands untouched. The trade-off the plan priced in was never paid.
2. **Tray menu** — confirmed: `NativeMenu` headers are built once and need an explicit
   rebuild. Deferred to Phase 3a, where the tray copy is extracted; `Localizer.CultureChanged`
   is the hook.
3. **Core display-name constants** — as recommended. `WaitTimeOptionExtensions.GetDisplayName`
   moved to `WaitTimeDisplayItem` (Desktop) and `GetSeconds` stayed in Core; the model
   catalogs keep their English names, which are identifiers rather than prose.

## Carried into later phases

- The UI is bilingual mid-migration: Theme and Interface language are translated, everything
  else is hardcoded English. Expected, and it shrinks with each Phase 3 batch.
- The translated tour copy names controls that are still drawn in English. 3a fixed the tray
  ("Выход" is now real); "Настройки → Приложение → Запуск" waits on 3b.
- ~~**User-facing exception messages are still English.**~~ *Resolved in Phase 6.* The cloud dialogs showed `ex.Message`
  from `CloudProviderNotConfiguredException` / `CloudSpeechTranscriptionException`, built in
  Core/Platform where there are no resources. 3a localized the titles and buttons around
  them and 3c localized the Cloud providers page, leaving the bodies English until Phase 6
  moved the wording into Desktop behind a reason enum. Deferring it was right: it is a
  Core/Platform refactor rather than a copy extraction, and finishing the markup sweep first
  meant the shape it needed was already obvious by the time it came up.
- **The default body for a new custom prompt stays English** (`PromptSettingsViewModel`,
  "Transcribe the following speech in {speech_lang} verbatim…"). It is text sent to an LLM,
  not UI chrome — the same reason the built-in prompt bodies in `JsonPromptTemplateRegistry`
  are not localized.
- `SettingsCategoryExtensions.GetDisplayName` (the nav group headers) is still English —
  Phase 3b.
