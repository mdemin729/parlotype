---
title: Ukrainian (uk) translation glossary
status: reference
created: 2026-09-17
---

# Ukrainian (uk) glossary

Companion to `brief.md`. Choices made for the 389-key Ukrainian translation in
`<scratch>/uk.json`. Translated from the English source, not from the Russian
`Strings.ru.resx` — Russian was consulted only for its *structural* solutions to
placeholder/case problems (see below), never for word choice. Ukrainian computing
vocabulary is used throughout (e.g. **застосунок**, not **додаток**, for "app").

## Register

**Impersonal/imperative verb forms without a stated pronoun** — "Виберіть тему",
"Додайте гарячу клавішу", "Перевірте ключ" — matching how Windows' own Ukrainian
localization and virtually all Ukrainian desktop software addresses the user: a
polite second-person-plural verb ending with no "ви" written out, rather than
choosing between "ти" and "ви" explicitly. Where a pronoun could not be avoided
(e.g. "Ви говорите" as a fixed UI label, "ваш"/"ваші" in descriptive body text),
the **ви**-form (formal/plural) is used, never "ти". This is consistent across
onboarding, tooltips, error text and settings descriptions, and is never mixed.

## Quotation and typography convention

- **Guillemets** `«…»` for quoted terms, model/prompt names and literal
  fragments — `«{0}»`, `«Хмара»`, `«потім перекласти на ...»` — the standard
  Ukrainian convention, no inner spacing (unlike French).
- **Em dash** `—` used with plain spaces on both sides, mirroring the English
  source's dash-separated clauses (`Готово — оновлення завантажено` style status
  lines).
- **Non-breaking space (U+00A0)** between a number and a unit token that stays in
  English per the never-translate register (`670 MB`, `10 GB`,
  `{1} с`) and before the "6 годин" style spelled-out unit in
  `Settings_Updates_CadenceNote`. This mirrors the French/Spanish precedent of
  tying a number to its unit and prevents an orphaned unit at a line wrap in the
  narrow widget.
- **Apostrophe**: the straight `'` is used in words that require one
  (комп'ютер, буфер обміну is apostrophe-free, but `розв'язання`-type words), matching
  standard Ukrainian orthography, which uses the plain apostrophe rather than a
  typographic one.

## Never-translate register

Followed exactly as specified in `brief.md`: `Parlotype`, `Whisper`, `Parakeet`,
`Parakeet TDT v3`, `Gemma 4`, `sherpa-onnx`, `llama.cpp`, `llama-server`,
`Silero VAD`, `Vulkan`, `ONNX`, `OpenAI`, `Groq`, `xAI`, `Grok`,
`gpt-4o-mini-transcribe`, `whisper-large-v3`, `Large v3 Turbo`,
`Ctrl`/`Alt`/`Shift`/`Win`/`Space`/`Esc`/`Tab`/`Enter`, `settings.json`,
`secrets.json`, `%LOCALAPPDATA%`, `Setup.exe`, `vulkan-1.dll`,
`llama-server.exe`, `HKCU Run`, `api.github.com`, and the unit tokens
`WAV`/`GB`/`MB`/`ms`/`s` where they appear as literal English abbreviations
inside a pre-formatted value. Model/build identifiers (`E4B`, `E2B`, `mmproj`,
`INT8`, `fp32`) are also left untouched.

Windows/macOS feature names use the real localized wording those OSes use in
Ukrainian, not literal translations:
- Task Manager → **Диспетчер завдань**
- Startup apps tab → **Автозавантаження**
- File Explorer → **Провідник**
- Run dialog → **Виконати**
- Task View → **Подання завдань**
- Windows Settings app → **Параметри**
- Game Bar → **Ігрова панель**
- Windows Voice Typing → **Голосове введення Windows**

One judgment call: **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) has no
single fixed Ukrainian name across Microsoft's own localization that I could
verify from memory; I used "Меню швидких посилань" as a literal, readable
rendering — flagged for a native check.

## Core glossary

| English term | Ukrainian | Notes |
|---|---|---|
| dictation | **диктування** (verb: диктувати) | App's core verb throughout. |
| transcribe | **розпізнавати** / **розпізнавання** | "recognized text" → "розпізнаний текст". Kept distinct from *перекладати*. |
| translate | **перекладати** / **переклад** | Never used for *transcribe*. |
| recording | **запис** | Also the "Recording..." status ("Запис..."). |
| speech engine | **рушій мовлення** (short: **рушій**) | `Settings_Category_SpeechEngine` and `Settings_Engine_Title` use the short form; `Settings_Engine_Heading` uses the full form. |
| model | **модель** | Appositive, undeclined before an identifier: "модель Whisper", "модель Gemma 4". |
| runtime | **середовище** | "Vulkan/CPU execution backend" → "середовище Vulkan" / "середовище {0}". Matches the concept Russian calls "среда", translated with the natural Ukrainian word, not calqued. |
| source language | **мова джерела** | Also used for "spoken language" everywhere that phrase recurs (one term, one concept). |
| target language | **мова виведення** | The language Parlotype types, whether or not translation is active. `Language_Target_TranslationHint` ("Translation target") also maps to this single term rather than introducing a second word for the same concept. |
| hotkey | **гаряча клавіша** | Standard Ukrainian software term. |
| push-to-talk | **утримання** | Mode badge and mid-sentence noun (`Settings_Hotkeys_Conflict_Mode_PushToTalk`); pairs with "Утримання {0}" for the gesture format. |
| toggle | **перемикання** | Mode badge and mid-sentence noun; verb "перемикати" for the mode-switch tooltip. |
| binding | *(not a separate surfaced term)* | No key in this set needs a standalone word for "binding" as a concept distinct from "гаряча клавіша". |
| tray | **трей** (full: системний трей) | Common Ukrainian tech loanword for the Windows notification area. |
| widget | **віджет** | Kept as the accepted Ukrainian tech term. |
| waveform | *(no key in this set surfaces this string alone)* | Not present as standalone copy. |
| wait time | **час очікування** | `Settings_SilenceTimeout_Title` → "Час очікування"; the Medium/Long/Extended/Very Long options are masculine adjectives agreeing with this noun (Середній, Довгий, Розширений, Дуже довгий). |
| punctuation | **пунктуація** | |
| profanity filter | **фільтр ненормативної лексики** | "ненормативна лексика" used consistently for the masked words themselves too. |
| inject / typed into | **друкувати** (the text "друкується"/"буде надруковано") | One verb for the whole "typed into" concept, matching `Settings_Language_TargetCaption` ("Parlotype друкує") — no separate technical term invented for *inject*. |
| clipboard | **буфер обміну** | (No key in this set surfaces the clipboard directly — noted for completeness.) |
| cloud engine / cloud provider | **хмарний рушій** / **хмарний провайдер** | "провайдер" is an established Ukrainian IT loanword (as in "інтернет-провайдер"), not a Russian calque. The `Transcribe_CloudBadge` badge is simply **"Хмара"**. |
| API key | **ключ API** | |
| onboarding | **тур** | "Guided tour" — `Onboarding_WindowTitle` → "Тур Parlotype", `Onboarding_Nav_Skip` → "Пропустити тур". |
| restart required | **потрібен перезапуск** | |
| prompt (AI instruction, Gemma 4) | **промпт** | Not in the core glossary list but recurs 15+ times; kept as the loanword now standard in Ukrainian AI/LLM discussion, distinct from "підказка" (which would collide with tooltip/hint copy). |

## Case-inflection flags (per brief.md's placeholder rule)

Every key below substitutes a raw, uninflectable value into a slot where the
literal English phrasing would force a Ukrainian case or gender ending. Each was
restructured so the slot sits after a colon, at the edge of the clause, or is
replaced by an invariant verb — listed here per the brief's instruction to
surface these as maintainer-facing signals, not paper over them:

- **`Language_ToggleSwitch_TranslateToFormat`** — `"Translate to {0}"`. Literal
  Ukrainian would need the accusative of the language name after "на"
  (e.g. "іспанську", not the nominative "іспанська" the placeholder actually
  holds). Restructured to **`"Переклад: {0}"`** (label + colon), sidestepping
  the case entirely.
- **`Language_Summary_Format`** — `"You speak {0} → Parlotype types {1}."`.
  Ukrainian idiomatically says "говорити іспанською" (instrumental case) for
  "speak Spanish", which the raw nominative placeholder can't produce.
  Restructured to **`"Ви говорите: {0} → Parlotype друкує: {1}."`**, reusing the
  same colon-based structural fix the Russian file uses for this exact key (the
  *structure* is reused, not the wording).
- **`Language_Toast_TargetUnsupportedFormat`** — `"{0} isn't available in {1}.
  Translation set to {2}."`. A naive adjective rendering ("{0} недоступна/
  недоступний/недоступне в {1}") would force gender agreement on an adjective
  with a language name whose grammatical gender isn't reliably knowable from the
  raw string. Rewritten with an invariant present-tense verb instead of an
  adjective: **`"{0} не підтримується в {1}. Мова перекладу: {2}."`** — "не
  підтримується" doesn't inflect for gender in the present tense, so no
  agreement is needed regardless of what `{0}` turns out to be.
- **`Cloud_Error_ProviderUnavailableFormat`** — `"{0} is unavailable right now
  (HTTP {1})..."`. Same gender-agreement risk on the provider name subject
  (`Cloud_ProviderName_OpenAiCompatible` renders as a Ukrainian phrase headed by
  a masculine noun, but `Cloud_ProviderName_XaiGrok` is an indeclinable foreign
  name with no obvious grammatical gender). Rewritten with a verb instead of an
  adjective: **`"{0} наразі не відповідає (HTTP {1})..."`** ("isn't responding"),
  which is invariant. This follows through on the design the brief's own
  `Cloud_ProviderName_*` comment calls for ("followed by a colon or a verb, so
  the name is always nominative and no language needs to inflect it").
- **`Transcribe_Status_RuntimeUnavailableFormat`** and
  **`Transcribe_Status_RuntimeRestartRequiredFormat`** — `"{0} runtime..."`.
  Not a hard grammatical requirement (`Vulkan`/`CPU` are undeclined foreign
  identifiers either way), but reordered to **`"Середовище {0}..."`** /
  `"...середовище {0}"` for naturalness — an identifier glued in front of a
  Ukrainian noun as if it were an adjective reads foreign, whereas
  noun-then-identifier is the pattern used everywhere else in this file
  ("модель Whisper", "рушій llama.cpp").

No other key in the 389 needed rephrasing for this reason. Genitive-requiring
mid-sentence words (`Settings_Hotkeys_Modifier_LeftFormat`/`RightFormat` →
"лівого {0}"/"правого {0}"; `Settings_Hotkeys_Conflict_Mode_PushToTalk`/`Toggle`
→ "утримання"/"перемикання") did **not** need flagging: `{0}` in the modifier
keys is always an untranslated key name that isn't declined regardless of the
Ukrainian word around it, and "утримання"/"перемикання" are neuter -ння nouns
whose nominative and genitive singular forms are identical, so no rephrasing was
needed there even though the surrounding sentence (`Settings_Hotkeys_Conflict_
AlreadyBoundFormat`, "...в режимі {1}") does put that word in the genitive.

## Things I was unsure about

- **`Settings_Hotkeys_Reserved_QuickLinkMenu`** — see the never-translate section
  above; no verified official Ukrainian Windows term found from memory.
- **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P) — Windows' own Ukrainian
  UI may call this pane by a single verb ("Проєктувати"); I used "Проєктування
  зображення" for a fuller descriptive phrase. Worth checking against current
  Microsoft Ukrainian localization if exact parity matters.
- **"промпт" vs "підказка"** for the Gemma 4 prompt-editor feature — I chose the
  loanword to avoid colliding with tooltip/hint text (also naturally
  "підказка" in Ukrainian), but a native reviewer may prefer a fully native term
  if one is established by the time this ships.
- **Gender of `{0}` in `Cloud_Error_ProviderUnavailableFormat` and
  `Language_Toast_TargetUnsupportedFormat`** — resolved by switching to
  invariant verbs (see above) rather than guessing a gender, per the brief's
  instruction to treat this as a maintainer-facing signal rather than paper over
  it silently.
- **`уникає`** in `Settings_Hotkeys_Conflict_AltGrFormat` ("Ctrl+Alt+Space
  уникає цього") — a slightly compressed rendering of "avoids this"; a fuller
  "дає змогу уникнути цього" reads more naturally but was longer than the
  tooltip-adjacent context seemed to want. Flagging in case the terser form
  reads awkward in context.

## Validation performed

Every key was checked programmatically against `uk.brief.json`: all 389 keys
present with no extras, every `{0}`/`{1}`/`{2}` placeholder set matches the
English source exactly, the literal template tokens `{speech_lang}` and
`{text_lang}` used inside prompt-help copy are preserved verbatim, and no value
over 25 characters matches the English source byte-for-byte. No stray Latin
characters were found embedded inside Cyrillic words (checked with a
mixed-script regex scan) beyond the intended never-translate identifiers.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"За замовчуванням (дослівне розпізнавання)"** — verbatim/word-for-word rendered as *дослівне (literal)*, paired with the already-established *розпізнавання* for transcription; no new terms introduced.
