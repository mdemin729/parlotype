---
title: Polish (pl) translation glossary
status: reference
created: 2026-09-17
---

# Polish glossary

Companion to `pl.json` (389/389 keys). Read `brief.md` first — this file only records
the choices specific to Polish.

## Register

- **Informal but not casual.** Second-person singular verb forms without the `ty`
  pronoun unless it adds emphasis ("Wybierz…", "Twoje ustawienia…"). This matches
  current Microsoft Polish style for Windows 10/11 consumer UI, which moved away from
  the older, more distant impersonal/passive style. It also matches how Russian and
  Spanish were already handled for this app (`ты`/`tú`-equivalent directness).
- **Past-tense verbs and predicate adjectives carry grammatical gender in Polish**
  (present-tense verbs do not). Wherever a sentence needed one and the subject was a
  substituted, un-inflectable value (an engine or provider name), I either used a
  present-tense construction, or inserted an explicit classifying noun of known gender
  ("Język {0}…", "Dostawca zgodny z OpenAI…") so the predicate agrees with *that* noun
  instead of the raw value.
- Buttons and short imperatives use the plain imperative ("Zapisz", "Anuluj", "Otwórz"),
  matching Windows' own button style.
- Sentence case throughout; no exclamation marks; no marketing language — matches the
  brief.

## Quotation convention

Polish standard double quotes: `„…”` (opening low‑9, closing high‑6/9) — e.g.
`„Large v3 Turbo”`. Never `«…»` (that's the Russian file's convention, not Polish) and
never straight `"…"`.

`ADR-032`'s slogan is kept in English inside the quotes, exactly like the Russian file
does, since it is a citation of the ADR's own title rather than ordinary prose:
`(ADR-032, „Local by default. Cloud by choice.”)`.

## Non-breaking space

Used before a unit that must not wrap from its number: `Settings_SilenceTimeout_WaitFormat`
→ `"{0} ({1} s)"` (U+00A0 between the number and "s"). No other key needed one —
sizes and version numbers arrive pre-formatted as opaque data (`{1}` already contains
`"1.5 GB"`), so I don't control spacing inside them.

## Core glossary

| Term | Polish | Notes |
|---|---|---|
| dictation | dyktowanie | verb: dyktować |
| transcribe / transcription | transkrybować / transkrypcja | |
| translate / translation | tłumaczyć / tłumaczenie | never conflated with transkrypcja |
| recording (live capture) | nagrywanie | the recorded file/result, where distinct, is nagranie |
| speech engine | silnik mowy | short form silnik used alone once the page context is clear (Settings_Engine_Title) |
| model | model | unchanged, already a Polish word |
| runtime | środowisko | "Środowisko Whisper", parallel to "Model Whisper" |
| source language | język źródłowy | |
| target language | język docelowy | |
| hotkey | skrót klawiszowy | matches Windows' own term |
| push-to-talk | przytrzymanie / przytrzymaj i mów | noun form for gesture composition, verb-phrase for the mode badge |
| toggle (mode) | przełącznik | noun; genitive przełącznika used inside a sentence |
| binding | skrót (in context), przypisanie (when naming the act of binding) | |
| tray | zasobnik systemowy | matches Windows' own term |
| widget | widżet | established Polish IT loanword |
| wait time / silence timeout | czas ciszy | short nav title; body text spells out "jak długo czekać po zakończeniu mowy" |
| punctuation | interpunkcja | |
| profanity filter | filtr wulgaryzmów | |
| inject / typed into | wpisywany do / wpisywane tam, gdzie… | no invented technical term, plain "type into" phrasing |
| clipboard | schowek | |
| cloud engine / cloud provider | silnik chmurowy / dostawca chmury | |
| API key | klucz API | |
| onboarding / tour | wprowadzenie | used for both the concept and the window title |
| restart required | wymagany restart | |
| build (software) | kompilacja | "Ta kompilacja nie może się aktualizować…" |
| prompt (LLM instruction) | prompt / prompty | kept as the established Polish AI-tooling loanword rather than translating literally; plural prompty |
| backend | backend | kept — standard Polish IT usage |

## Case-inflection notes — keys where the raw `{0}` would force an oblique Polish case

Per the brief, when a substituted value (engine/runtime/model/language name) would sit
somewhere in the sentence that demands genitive, dative, locative or a
gender-agreeing predicate, I restructured the Polish so the slot lands after a colon,
at a clause boundary, or in the nominative. Flagging every one here, as requested,
since it signals a spot where the English source could eventually be reshaped:

- **Transcribe_Status_RuntimeRestartRequiredFormat** — "use the {0} runtime" would put
  {0} in the genitive after "użyć środowiska". Restructured with a colon:
  `"Uruchom ponownie Parlotype, aby użyć środowiska: {0}"`.
- **Settings_Runtime_RestartNoteFormat** — same problem for the first `{0}`
  ("running the {0} runtime" → genitive after "w środowisku"). Restructured:
  `"Ta sesja działa już w środowisku: {0}. … aby przełączyć się na {1} — …"`. The
  second `{1}` ("switch to X") lands in the accusative, which for a masculine
  inanimate technical name is identical to the nominative, so no visible change was
  needed there.
- **Language_ToggleSwitch_TranslateToFormat** ("Translate to {0}") — the English
  comment already flags this for Russian once language names are localized; the same
  risk exists for Polish (`"Tłumacz na {0}"` needs {0} in the accusative, which for
  most Polish language-name forms happens to equal the base form, but this is fragile
  once arbitrary values arrive). Kept as `"Tłumacz na {0}"` for now, matching the
  existing Russian treatment, but this is worth re-examining alongside Russian in the
  language-name-localization phase.
- **Language_Summary_Format** ("You speak {0} → Parlotype types {1}.") — a literal
  "Mówisz {0}" is not idiomatic Polish (Polish says "mówisz *po polsku*", not "mówisz
  polski" — a direct object doesn't work here). Restructured around a colon:
  `"Mówisz: {0} → Parlotype wpisuje: {1}"`.
- **Language_Toast_SourceUnsupportedFormat** ("{0} isn't a source in {1}.") — "{1}"
  is an engine name and "in {1}" would need the locative case ("w Whisperze",
  "w Parakeecie" — awkward and inconsistent for "Gemma 4"). Restructured with an
  explicit classifying noun and a colon: `"Język {0} nie jest obsługiwany jako
  źródłowy w silniku: {1}."` — the added noun "Język" also fixes a second problem:
  the predicate adjective ("obsługiwany") needs a known grammatical gender to agree
  with, and the raw `{0}` value's gender is unknowable.
- **Language_Toast_TargetUnsupportedFormat** ("{0} isn't available in {1}.
  Translation set to {2}.") — same "{1}" locative problem as above, same fix:
  `"Język {0} nie jest dostępny w silniku: {1}. Ustawiono tłumaczenie na: {2}."`
  ({2} was already colon-guarded per the brief's own comment.)
- **Language_Toast_TargetResetFormat** ("not supported by {0}") — "przez {0}" uses
  the accusative, which is harmless for the current closed set of engine names
  (masculine inanimate, accusative = nominative) but would break for a
  feminine/neuter name. Restructured defensively anyway:
  `"Poprzedni język docelowy zresetowano — nieobsługiwany w silniku: {0}."`

Keys I did **not** flag despite holding a name in a placeholder: anywhere the design
already quotes the value (`„{0}”` — quoting neutralizes case in Polish, same as
Russian's `«{0}»`), anywhere the value leads the sentence as a bare nominative subject
of a present-tense verb (no gender agreement in Polish present tense), and the
hold/double-tap key-name formats, where the case problem is solved once per language
inside the dedicated `Settings_Hotkeys_Modifier_LeftFormat` / `RightFormat` keys
(`"lewego {0}"` / `"prawego {0}"`, genitive adjective + undeclined key name — the same
pattern the Russian file uses with `левого`/`правого`).

## Windows-terminology matches

Used the actual Polish Windows 10/11 UI wording rather than literal translations for:

- Task Manager → **Menedżer zadań**; its "Startup apps" tab → **Aplikacje przy
  uruchamianiu**; "Enable" → **Włącz**.
- Task View → **Widok zadań**; Quick Link menu (Win+X) → **Menu Szybki dostęp**;
  Windows Voice Typing → **Wpisywanie głosowe**.
- The Run dialog is rendered as `Otwórz okno „Uruchom”`, quoting the dialog's own
  Polish title the way Windows itself presents it.

## Things I was unsure about

1. **`Settings_Category_Input`** ("Input") — translated as **Wejście**. The category
   groups the Hotkeys page; "Wejście" is generic enough to match the English's own
   vagueness, but I'm not fully confident it reads as clearly in Polish as "Input"
   does in English. Worth a second look once the category's exact contents are final.
2. **`Settings_Prompts_Title` / prompt terminology** — settled on the loanword
   **prompt/prompty** rather than a native coinage (e.g. "polecenie", "instrukcja")
   because that's what Polish AI-tooling users already call this concept, but it's a
   judgment call and a native alternative would also be defensible.
3. **Gender of "build"** — treated `kompilacja` (feminine) as the working noun for
   "build" throughout the llama.cpp section, which drove badge genders like
   `Zainstalowana` (vs. the masculine `Zainstalowany` used for a model). If the actual
   UI string the badge attaches to is something else (e.g. "wersja", also feminine —
   consistent — or "plik", masculine — would break agreement), that badge gender
   should be re-checked against the real visual context.
4. **`Language_ToggleSwitch_TranslateToFormat`** — see the case-inflection notes above;
   flagged but not fully future-proofed against arbitrary localized language names.
5. **Masculine default for gender-neutral direct address** — past-tense verbs referring
   to the user ("used it last" → `używałeś`) default to the masculine form, per common
   Polish software convention absent any gender signal from the user. This is a
   widely-used convention, not a settled rule, and some Polish software instead avoids
   the past tense entirely to sidestep it.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Domyślny (dosłowna transkrypcja)"** — verbatim/word-for-word rendered as *dosłowna (literal)*, paired with the already-established *transkrypcja* for transcription; no new terms introduced.
