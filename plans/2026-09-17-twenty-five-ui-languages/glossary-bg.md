---
title: Bulgarian glossary — core terms, register and conventions
status: reference
created: 2026-09-18
---

# Bulgarian (bg) glossary

Companion to `bg.brief.part1.json` / `bg.brief.part2.json` and the resulting
`bg.part1.json` / `bg.part2.json`. 389 keys translated across two slices, from the
English source — not from the shipped `Strings.ru.resx`, which shares an alphabet and a
lot of cognate vocabulary with Bulgarian but not its computing register. Every choice
below was checked against how Bulgarian Windows itself names the same action (verified
via Microsoft's own bg-bg support pages and other Bulgarian sources) rather than assumed
from the Russian file.

## Formality register

**Formal "Вие"** throughout all prose — imperative verbs (`-ете`/`-айте`: "Изберете",
"Добавете", "Отворете") and 2nd-person present tense (`-ете`/`-ите`: "Говорите"), never
the "ти"-form ("Избери"/"Добави"/"Говориш"), matching how Windows' own Bulgarian
localization and Microsoft 365 address the user. Bulgarian's second-person-plural verb
form doubles as the polite singular address, the same relationship Russian has between
"вы" and "ты" — but this is a grammatical fact independent of Russian, not something
borrowed from it. Used consistently across onboarding, tooltips, error text and settings
descriptions; never mixed with an informal form.

**Exception, by UI convention rather than register:** short button labels (Запази,
Изтрий, Отвори, Премахни, Добави, Провери, and the onboarding nav button "Пропусни тура")
use the terse singular-imperative form standard for Bulgarian buttons, the same
convention `pl` ("Zapisz"/"Usuń") and `hr` ("Spremi"/"Izbriši") follow regardless of their
own declared register. This is a UI-convention carve-out for click targets, not a second
formality register — every string that addresses the user in a sentence, a caption, or an
inline descriptive label is formal, no exceptions.

**Correction (2026-09-18):** an initial pass left seven strings in the informal
"ти"-form, contradicting the declared register above — caught by a coordinator review
against the sibling Slavic files (`ru`/`uk`/`cs`/`sk`/`hr`/`sl` all render "You speak" as
formal; only `pl`, which *declared* informal, does not). Fixed to formal throughout; see
`<scratch>/bg.fix.json` for the exact before/after values. The affected keys:

- `Transcribe_YouSpeakLabel`, `Settings_Language_SourceCaption`,
  `Language_Summary_Format` — "Говориш" → "Говорите" (the "You speak" caption/label,
  three surfaces of the same string).
- `Hotkey_Hint_TalkFormat`, `Hotkey_Hint_DictateFormat` — "...за да говориш"/"диктуваш" →
  "...за да говорите"/"диктувате".
- `Settings_Hotkeys_Gesture_HoldFormat`, `Settings_Hotkeys_Gesture_DoubleTapFormat` —
  "Задръж {0}"/"Натисни двукратно {0}" → "Задръжте {0}"/"Натиснете двукратно {0}". These
  are inline descriptive labels in a hotkey-list row, not clickable buttons, so they fall
  under the general register rather than the button exception above.

A full scan of both files for any remaining 2nd-singular verb (`-аш`/`-еш`/`-иш`
present-tense endings) or informal pronoun/possessive (`ти`, `твой`/`твоя`/`твоите`)
outside the button exception found nothing else to fix.

## Quotation convention

**Low-high double quotes `„…“`** (opening quote on the baseline, closing quote at
cap-height) — confirmed as the Bulgarian standard, matching German/Czech/Slovak, and used
for every quoted phrase, model name and prompt fragment: `„{0}“`, `„Облак“`,
`„после преведи на ...“`.

## Core glossary

| Term | Bulgarian | Notes |
|---|---|---|
| dictation | **диктовка** (verb: диктувам) | App's core verb throughout: "Диктувайте", "клавишна комбинация за диктовка". |
| transcribe / transcription | **транскрибирам** / **транскрибиране** | Kept strictly distinct from *превод* (translation). "Recognized text" → **разпознатият текст**. |
| translate / translation | **превеждам** / **превод** | Never used for *transcribe*. |
| recording | **запис** (status: **Записване...**) | *Запис* the noun ("текущия запис"), *Записване* the in-progress gerund used for the live status line, parallel to *Зареждане на модела...* (loading) and *Проверка за актуализации...* (checking). |
| speech engine | **двигател** (full: **речеви двигател**) | "Engine" is standard Bulgarian IT usage (as in *графичен двигател*, not the car sense). Short form `двигател` for the nav label (`Settings_Engine_Title`), full form `речеви двигател` for the page heading and nav category. |
| model | **модел** | Unchanged, already Bulgarian. |
| runtime / backend | **среда** | "The Vulkan runtime" → *средата на Vulkan* / *средата {0}*; `Settings_Runtime_Description`'s "Whisper.net backend" reuses the same word — one concept, one term, matching the cs/sk `prostředí`/`prostredie` precedent (an independently-motivated Slavic root, not a Russian calque — Russian's own word here is *среда* too, but that overlap is coincidental shared vocabulary, the same way English and German share *engine*/*Engine*). |
| source language | **изходен език** | |
| target language | **целеви език** | |
| hotkey / binding | **клавишна комбинация** | The term Bulgarian Windows itself uses for "keyboard shortcut" (Settings' own "Клавишни комбинации" page). Also covers *binding* — no separate word was needed. |
| push-to-talk | **задържане** (noun) | Mode badge and mid-sentence noun ("вече е обвързана със **задържане**"); gesture line uses the imperative "Задръж {0}". |
| toggle | **превключване** (noun) | Mode badge and mid-sentence noun; gesture line uses "Натисни двукратно {0}". |
| tray | **системен трей** (short: **трей**) | Established loanword in Bulgarian desktop software; shorter and more current than the formal *област за уведомяване*. |
| widget | **джаджа** | Microsoft's own Bulgarian term for a desktop widget (Windows Vista/7 "Gadgets" → "джаджи"). Feminine, definite *джаджата*. |
| wait time / silence timeout | **време на изчакване** | One term for both concepts, per the brief's note that they're the same thing. The Medium/Long/Extended/Very Long options are neuter-agreeing adjectives (Средно/Дълго/Удължено/Много дълго), implicitly agreeing with the elided *време*. |
| punctuation | **пунктуация** | |
| profanity filter | **филтър за псувни** | *псувня* is the plain, natural Bulgarian word for a swear word — no euphemism needed. |
| inject / typed into | **напечатвам** ("се напечатва в") | Distinct verb from *пиша* (used nowhere in this set) so it never collides with the *промпт* text-entry vocabulary. "Parlotype types" (short caption) → **Parlotype напечатва**. |
| clipboard | **клипборд** | Microsoft's own Bulgarian Windows term. |
| cloud engine / cloud provider | **облачен двигател** / **облачен доставчик** | *доставчик* ("supplier/provider") is ordinary Bulgarian, not a Russian loan (Russian says *поставщик*). Badge `Transcribe_CloudBadge` is the plain native word **Облак**, matching the pattern other languages followed (Chmura/Nube/Хмара) where a short native word exists — coincidentally close to Russian *Облако*, but that's the unavoidable literal translation of "cloud" in any closely related Slavic language, not a calque. |
| API key | **API ключ** | *ключ* is masculine — watch its definite forms (see below). |
| onboarding | **тур** | "Тур на Parlotype", "Пропусни тура". |
| restart required | **Необходим е рестарт** | |
| prompt (Gemma 4 AI instruction) | **промпт** (loanword, masc.; plural промптове) | *Not* in the brief's core-glossary table, but recurs ~15 times in `Settings_Prompts_*`. Deliberately **not** *подсказка* ("hint") — see the collision note below. |

### A word deliberately avoided: "подсказка" for two different things

Early drafts used *подсказка* ("hint/tip") for the Gemma 4 "prompt" concept, since it's
the transparent native rendering. But `Settings_Hotkeys_Conflict_ParameterHintsFormat`
("{0} shows parameter hints in Visual Studio and VS Code") also naturally wants
*подсказки* for its completely unrelated IDE-tooltip sense. Keeping one Bulgarian word
for two different UI concepts inside the same string set would violate "one word per
concept," so the AI-prompt concept was moved to the loanword **промпт** — now standard in
Bulgarian AI/LLM discussion — freeing *подсказка* for its literal "hint" sense. (Ukrainian
hit the identical collision and resolved it the same way, independently.)

## The postpositive definite article — where it collides with `{0}`

Bulgarian has no case system (the brief's usual Slavic trap doesn't apply), but it marks
definiteness with a suffix glued to the **first word** of a noun phrase — the noun itself
if bare, or a preceding adjective if there is one (**двигателят**, **новият двигател**).
A raw `{0}` can never carry that suffix, so "the {0} runtime" cannot become `{0}-ът` any
more than Romanian's `{0}-ul` could — the article would be fused onto whatever opaque
string fills the slot (a name like "Vulkan" or "CPU").

The fix, matching the pattern Romanian used for the identical `runtime` problem: put the
native noun *средата* ("the environment/runtime"), already carrying its own article, in
front of the bare, undeclined name — an ordinary Bulgarian apposition, the same
construction as *хотел Хилтън* or *реката Дунав*.

**Three keys had a genuine collision, all in the `Transcribe_Status_Runtime*` /
`Settings_Runtime_RestartNoteFormat` family** — the same three keys every other language
in this project has flagged for its own version of this problem:

| key | English | Bulgarian |
|---|---|---|
| `Transcribe_Status_RuntimeRestartRequiredFormat` | "Restart Parlotype to use the {0} runtime" | "Рестартирайте Parlotype, за да използвате **средата** {0}" |
| `Transcribe_Status_RuntimeUnavailableFormat` | "{0} runtime not available — change in Settings" | "**Средата** {0} не е налична — променете от Настройки" (English has no "the" here, but Bulgarian still wants the article: an unqualified subject like this reads as a *specific*, already-chosen runtime, not a generic one, so the definite form is grammatically required regardless of the English article) |
| `Settings_Runtime_RestartNoteFormat` | "...already running the {0} runtime. ...restart Parlotype to switch to {1}..." | "Тази сесия вече работи със **средата** {0}. ...рестартирайте Parlotype, за да преминете към {1}..." — only the first slot collided; "switch to {1}" is a bare object of "към" (to) and needs no article at all |

No other key needed this fix. Every other `{0}`-leads-the-sentence pattern in the set
(`Language_UnavailableNoteFormat`'s `{0} can't translate`, all the `Cloud_*` error
formats' `{0}: ...`) substitutes an engine or provider **name** functioning as a label —
Bulgarian proper nouns and product names don't take a definite article at all ("Whisper
не може да превежда", never "*Whisper-ът* не може да превежда"), so there was nothing to
fix there.

### Not a collision, but flagged by every other language: the three cross-language keys

`Language_Summary_Format`, `Language_ToggleSwitch_TranslateToFormat` and
`Language_Toast_TargetUnsupportedFormat` are the keys that `fr`, `uk`, `pl`, `fi` and
others all independently flagged, because a substituted language name needs a case
ending their languages can't apply to a raw string. **Bulgarian has no case system, so
none of these were forced.** "Говоря {0}" (I speak {0}) and "Преведи на {0}" (Translate
to {0}) are exactly how a language name is used bare in ordinary Bulgarian — no article,
no case, no restructuring needed. Translated them in natural English word order rather
than adopting the colon convention some Romance languages used for readability, since
Bulgarian had no grammatical reason to.

### The full/short article rule (-ът/-я) — checked throughout

Only masculine singular nouns distinguish the **full** article (`-ът`/`-ят`, subject
position) from the **short** article (`-а`/`-я`, everywhere else — object, after a
preposition). This was audited on every masculine noun the translation define-articled:

- Subject position → full form: **Диспечерът** на задачите (в "само Диспечерът на
  задачите може да го отмени"), **Пътят** е копиран, **Преводът** е на пауза, **изходът**
  вече съвпада (`Language_Toast_EngineCannotTranslateFormat`).
- Object / after a preposition → short form: Отворете **Диспечера** на задачите
  (`Settings_Startup_BlockedBody`), в **клипборда**, в **трея**, към **промпта** ви,
  на **промпта**.

`Language_Summary_YourKeyboardLanguage` ("your keyboard language", substituted as the
object `{0}` of "Говорите {0} →...") is the one place this was easy to get backwards: it
reads **"езика на клавиатурата ви"** (short form — an object of *говоря*), not
*"езикът..."* (full form, which would be wrong here since it's never the subject of its
own sentence).

## Windows/macOS feature names — verified against Bulgarian Windows, not guessed

Checked via Microsoft's own bg-bg support pages and Bulgarian tech sources rather than
translated literally:

- Task Manager → **Диспечер на задачите**; its Startup apps tab → **Приложения при
  стартиране** (confirmed via support.microsoft.com/bg-bg and Windows 11 Settings →
  Apps → Startup, which is titled the same in the Settings path).
- File Explorer → **Файлов мениджър** (Microsoft's current bg-bg support term; older
  sources also use *Файлов проводник* but the live support site uses this one).
- Run dialog → **прозорецът Изпълнение** (confirmed — Win+R opens "Изпълнение").
- Windows Voice Typing → **Гласово въвеждане** (confirmed via
  support.microsoft.com/bg-bg's own voice-typing article).
- Game Bar is kept as **Game Bar** (product name, not translated), matching how Romanian
  and other languages in this project handled it.
- Show desktop, Task View, Windows Settings, Security screen were rendered as
  straightforward, standard Bulgarian IT phrasing where I could not find a pinned
  official Bulgarian Windows string.

### Things I was unsure about

- **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X): translated as "Меню за бърз
  достъп". I found no single fixed Bulgarian Microsoft name for this menu the way there
  is for "Диспечер на задачите".
- **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P): translated as "Проектиране на
  екрана". Same issue — no confirmed official Bulgarian Windows string for the Project
  quick panel.
- **`Settings_Hotkeys_Reserved_TaskView`**, **`SecurityScreen`**: rendered as standard,
  readable Bulgarian ("Изглед на задачите", "Екран за сигурност") without a pinned
  official source; worth a native-speaker check against a Bulgarian-language Windows
  install if one is available.

## Never-translate register

Checked every string against `brief.md`'s register: Parlotype, Whisper, Parakeet /
Parakeet TDT v3, Gemma 4, sherpa-onnx, llama.cpp, llama-server, Silero VAD, Vulkan, ONNX,
OpenAI, Groq, xAI, Grok, `settings.json`, `secrets.json`, `%LOCALAPPDATA%`, GB/MB/ms,
`HKCU Run`, `Setup.exe`, `api.github.com`, `vulkan-1.dll`, `llama-server.exe`, CUDA,
mmproj, E4B/E2B, INT8, fp32, Medium/Large v1/v2/v3, and Ctrl/Alt/Shift/Win/Space/Esc — all
kept in Latin letters, never transliterated into Cyrillic and never declined. The
`{speech_lang}` / `{text_lang}` named tokens in `Settings_Prompts_Help_BuiltInBody` are
copied byte-for-byte (both verified present in the output).

One deliberate departure from a strict reading of "never translate": **"Vulkan loader"**
in `Settings_Runtime_VulkanMissing`/`VulkanUnavailableReason` translates the generic word
"loader" (→ **зареждащ модул**) while keeping "Vulkan" and the `vulkan-1.dll` filename
untouched — only the product name and the literal filename are protected, not every
English word standing next to them.

## Verification performed

- Both output files parsed as flat JSON, no wrapper, 195 + 194 = 389 keys, matched 1:1
  against their brief's key set — zero missing, zero extra, zero overlap between the two
  files (checked programmatically).
- Every `{0}`/`{1}`/`{2}` placeholder set matches its brief entry exactly, per key
  (checked programmatically).
- `{speech_lang}` and `{text_lang}` confirmed present verbatim, byte-for-byte, in
  `Settings_Prompts_Help_BuiltInBody`.
- No translated value over 25 characters is byte-identical to its English source (checked
  programmatically across all 389 entries — zero matches).
- The two keys carrying an embedded blank line in the English (`Onboarding_Tray_Body`,
  `Settings_Data_DeleteDialog_BodyFormat`) keep the same `\n\n` paragraph break in
  Bulgarian.

## Things I fought for length on

- `Onboarding_Tray_Body` and `Settings_Startup_WhatChangesBody` are the two longest
  `_Body` strings in the set; both run a little longer in Bulgarian than English because
  "Приложения при стартиране" (the confirmed official Windows term) is unavoidably
  longer than "Startup apps". Left as-is since `_Body` keys have room per the brief.
- Kept `_Title`/`_Button`/`_Tooltip` keys tight throughout — e.g. `Settings_Engine_Title`
  uses the bare **Двигател** rather than the fuller **Речеви двигател** used on the page
  heading, and `Settings_Hotkeys_Mode_PushToTalk` is the single word **Задържане** rather
  than a literal "hold to talk" phrase.

## Things I was unsure about (general)

- **`Settings_Prompts_Help_BuiltInBody`**'s embedded example — the English shows the
  *shape* of a placeholder pattern ("speech in X into X text") rather than a real
  sentence; I rendered it as „реч на X, преобразувана в текст на X“, matching the
  pattern's structure rather than producing fully idiomatic prose, since it's describing
  a template shape, not a live sentence. Worth a native read-through.
- **`Settings_Prompts_CopySuffixFormat`** ("{0} (copy)" → "{0} (копие)"): per the
  localization skill's documented exemption for `PromptSettingsViewModel`, this becomes
  user data (a saved prompt name) once written and won't re-translate on a later language
  switch — noting it here so that isn't mistaken for a live-switch bug specific to
  Bulgarian.
- Whether **среда** reads unambiguously as "runtime" everywhere it's used bare (it's also
  the ordinary Bulgarian word for "environment" in general, and separately for
  "Wednesday" — context disambiguates in every key here, but worth a native check on
  `Settings_WhisperRuntime_Title` = "Whisper среда" in isolation, outside a full sentence).
