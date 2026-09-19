---
title: Maltese (mt) translation glossary
status: reference
created: 2026-09-18
---

# Maltese (mt) glossary

Companion to the 389-key Maltese translation (delivered as two slices, `mt.part1.json`
and `mt.part2.json`, merged by the importer). Read `brief.md` first — this file only
records the choices specific to Maltese.

## Sources consulted

Beyond `brief.md` and `glossary-hu.md` (read as instructed, for the article technique),
two external references shaped word choice and grammar decisions, since Maltese has no
second translator on this project to check against:

- Microsoft's own **Maltese Localization Style Guide** (`mlt-mlt-styleguide.pdf`, 72
  pages) — used for: the sun/moon letter list, the quotation-mark convention, the rule
  that personal pronouns are dropped when addressing the user, real attested UI verbs
  (Ikkanċella, Agħlaq, Issejvja, Niżżel, Ħassar, Oħroġ, Agħżel, Ikklikkja, Ittajpja,
  Irrestartja, Aqleb), and the real key-name table (confirms Esc/Ctrl/Alt/Enter etc. stay
  as printed).
- A ChatsControl summary of the same guide, for the "warm, conversational, informal"
  register framing and confirmation that **"Settings" is conventionally left
  unlocalized** in Maltese Microsoft software.

## Register

**Second person singular, with the pronoun dropped**, matching the Microsoft Maltese
style guide's explicit instruction: *"Personal pronouns are to be avoided when
addressing the user."* Verbs carry the person on their own conjugation
(`"Titkellem"` = "You speak", not `"Inti titkellem"`). Buttons and short commands use the
**bare imperative** (`Ikkanċella`, `Agħlaq`, `Niżżel`, `Ħassar`, `Iftaħ`), matching the
attested Microsoft Maltese pattern for the same actions. Body copy uses second-person
indicative with the pronoun dropped (`"Agħfas waħda mill-hotkeys tiegħek..."`). No
exclamation marks, no marketing language, sentence case throughout. Negative imperatives
drop the classical `la` prefix in the same informal register the style guide recommends
(`"Tibdiex Parlotype Awtomatikament"`, not `"La tibdiex..."`).

## Quotation convention

**Curly double quotes `" "`**, not single quotes — the Microsoft Maltese style guide
gives this explicitly and explains why: a large number of Maltese words themselves end
in an apostrophe (`erġa'`, `ta'`, `dan'`), so a single-quote pair reads ambiguously
against the language's own punctuation. Used for quoted UI values (model names, search
terms) and the ADR-032 slogan, which — matching every other language file — stays in
English inside the quotes as a citation: `(ADR-032, "Local by default. Cloud by
choice.")`.

## Core glossary

| Term | Maltese | Notes |
|---|---|---|
| dictation (noun) | **dettatura** | Distinct from **dittatura** ("dictatorship") by one vowel — a real near-homophone risk, flagged below. |
| dictation / to dictate (verb) | **iddetta** (jiddetta / tiddetta) | Attested Maltese verb from Italian *dettare* (confirmed via Wiktionary). Used for "Start dictating" → `Ibda Tiddetta`, "{0} to dictate" → `{0} biex Tiddetta`. |
| transcribe / transcription | **traskrizzjoni** (noun only) | No clean native verb exists for "transcribe" that I could confirm, so every occurrence was rephrased around the noun (`tagħmel it-traskrizzjoni` — "does the transcription") rather than coining an unattested verb. See "Unsure" below. |
| translate / translation | **traduċi** / **traduzzjoni** | Long-established, unambiguous native/Romance-loan pair, no overlap risk with traskrizzjoni. |
| recording (live capture) | **reġistrazzjoni** | Verb **irreġistra** (attested, used for AV recording generally). Status line "Recording..." → `Qed jirreġistra...`, matching the "Qed + verb" progressive construction that is how Maltese forms an ongoing-action status line. |
| speech engine | **magna tad-diskors** (full), **magna** (short) | "magna" alone is the established Maltese word for "engine" in the computing sense (`magna tat-tfittxija` = search engine is the standard term), so it carries the concept on its own once "tad-diskors" has been established once. `Settings_Engine_Title` (tight nav leaf) is just **Magna**. |
| model | **mudell** | Fully native-feeling loanword, no alternative needed. |
| runtime | **runtime** (kept) | No established Maltese IT term found; kept as an English technical loanword throughout, always with a native article/preposition attached to the word "runtime" itself (see the article section below), never bare. |
| source language | **lingwa tas-sors** | "sors" = source, itself an established loanword. |
| target language | **lingwa tal-mira** / **mira** (short) | "mira" = target/aim, a native word — deliberately not another Romance loan, so "sors" (loan) and "mira" (native) sit side by side, both fully naturalized in Maltese. |
| hotkey | **hotkey** (kept) | Kept as an English technical loanword throughout, singular and plural (`il-hotkey`, `il-hotkeys`) — Maltese computing discussion routinely uses this term as-is, and no short native compound was available that would not run long in the tight `Settings_Hotkeys_Title` nav slot. |
| push-to-talk | **żomm biex titkellem** (full), **Żomm** (badge) | "Żomm" = Hold, imperative, matching the same imperative-badge pattern as "Aqleb" (Toggle) below — this mirrors how Windows' own Maltese localization already uses bare imperatives as UI labels (Agħżel, Ikklikkja). |
| toggle | **aqleb** / **Toggle** | The verb **aqleb** ("switch") is the attested Microsoft Maltese rendering of "switch to" instructions, and is used for the *action* of toggling (`Aqleb bejn...`). The badge/mode *name* itself (`Settings_Hotkeys_Mode_Toggle`) is left as the loanword **Toggle**, since Maltese has no single native noun for the mode-name-as-such distinct from the verb, and mixing "Aqleb" as both verb and mode-name badge risked reading as two different UI actions. |
| binding | **assenjazzjoni** | Not surfaced as standalone UI copy in this key set; recorded for glossary completeness (one configured hotkey entry). |
| tray | **tray** (kept) | Kept as a loanword — Windows itself does not have a settled short native Maltese term for "system tray" that I could confirm, and it appears only inside longer descriptive sentences here, never as a standalone label. Flagged as uncertain below. |
| widget | **widget** (kept) | Universal computing loanword, consistent with how nearly every other language in this project keeps it. |
| waveform | **forma tal-mewġ** | Not surfaced as standalone copy in this key set; recorded for completeness. "mewġa" (wave) is native Semitic vocabulary. |
| wait time / silence timeout | **ħin ta' skiet** | "skiet" = silence, native. Title `Settings_SilenceTimeout_Title` → `Ħin ta' Skiet`. |
| punctuation | **punteġġjatura** | The standard, school-taught Maltese word — no alternative considered. |
| profanity filter | **filtru tal-kliem oxxen** | "kliem oxxen" = obscene/vulgar language ("oxxen" from Italian *osceno*, fully naturalized); "filtru" is an everyday Maltese loanword (`filtru tal-ilma` = water filter). |
| inject / typed into | **jiġi ttajpjat (fl-app)** | Passive construction built on the attested verb **ittajpja** (to type — confirmed directly from the Microsoft style guide's own example, `Ittajpja "erġa' aqbeż"`). No separate technical term invented for "inject". |
| clipboard | **clipboard** (kept) | No established native Maltese term found; kept as a loanword, consistent with keeping other core OS-level nouns (tray, widget, runtime) in English where Maltese computing usage does the same. |
| cloud engine / cloud provider | **magna bbażata fuq is-sħaba** / **fornitur tas-sħaba**; badge **Sħaba** | "sħaba" is the plain native Maltese word for "cloud" (the weather phenomenon) — this follows the brief's own precedent list (Chmura/Хмара/Nuvem/etc.) of languages that had a natural short native word for the Cloud badge and used it, rather than the languages that kept "Cloud" as a loanword. |
| API key | **ċavetta tal-API** | "ċavetta" = key, native Semitic word (irregular plural **ċwievet**). |
| onboarding | **gwida** | "The first-run tour" is rendered throughout as "the guide" rather than borrowing "tour" — "Gwida ta' Parlotype" for the window title, "Aqbeż il-Gwida" for Skip tour. |
| restart required | **jinħtieġ restart** (heading), verb **irrestartja** | "irrestartja" (to restart) is directly attested in the Microsoft style guide's own example sentence (`Jekk tirristartja issa...`). The noun "restart" is kept as a loanword when used as a bare noun (`ir-restart`), paralleling "runtime". |
| build (software) | **build** (kept) | No native or established loan noun found distinct from "verżjoni" (version, used separately for release version numbers) — kept as an English technical loanword for the llama.cpp build-number concept specifically, to avoid collapsing two distinct English source concepts (version vs. build) into one Maltese word. |
| prompt (LLM instruction) | **prompt** (kept, plural **prompts**) | Universal AI-tooling loanword, no attested Maltese alternative; English plural kept as-is, matching how other recent tech loanwords are pluralized in casual Maltese tech usage. |
| Settings (the app section) | **Settings** (kept) | A dedicated Microsoft Maltese localization reference states plainly that "Settings" is conventionally left unlocalized in Maltese software. Kept as a single consistent term everywhere it names the app's own Settings window/section (`Iftaħ is-Settings`, `Settings_WindowTitle`, tray menu item), never mixed with a coined alternative. |

## The Maltese-specific problem: the definite article's nine forms

Maltese `il-` is not a single fixed problem the way Hungarian's `a`/`az` binary choice
is — it assimilates to **nine different sun-letter consonants** (`ċ d n r s t x ż z` →
`iċ- id- in- ir- is- it- ix- iż- iz-`) and elides before vowels (`l-`), while staying
`il-`/`l-` before the eight moon-letter consonants. A `{0}` substituted at runtime with a
name I never see makes choosing the right one impossible — and unlike Hungarian's
`a(z)`, Maltese has **no written convention for "the article, form unknown"**: there is
no single fallback spelling a reader would recognize as deliberate rather than a typo.

So for Maltese the rule really is absolute: **never let a bare article, or a preposition
that contracts with the article (`ta'`, `fi`, `ma'`, `għal`), land directly against
`{0}`.** Two techniques were used, matched to what the sentence needed:

### 1. Hang the article on a fixed native/loan noun instead, and let `{0}` follow it bare

Where the placeholder is an adjective-like modifier of a noun I already control (a
runtime name, an engine name), Maltese word order lets the modifier follow the noun, so
the article lands on the **noun**, whose first letter I know, never on `{0}`:

- `Transcribe_Status_RuntimeRestartRequiredFormat` → `"Irrestartja lil Parlotype biex
  tuża r-runtime {0}"` — the sun-letter article assimilates onto **runtime** itself
  (`r-runtime`, since "runtime" starts with the sun letter r), regardless of what `{0}`
  turns out to be.
- `Transcribe_Status_RuntimeUnavailableFormat` → `"Ir-runtime {0} mhux disponibbli..."`
  — same technique, subject position.
- `Settings_Runtime_RestartNoteFormat` → `"...diġà qed tuża r-runtime {0}... irrestartja
  lil Parlotype biex taqleb għar-runtime {1}..."` — used twice; `għar-runtime` is the
  standard contraction of `għal` + `ir-runtime` (`għal`+`ir-`=`għar-`), so the
  preposition-article fusion also lands on "runtime", never on `{1}`.
- `Language_Toast_SourceUnsupportedFormat` → `"...fil-magna {1}."` — `fil-` is `fi` +
  `il-`, fixed because "magna" (engine) always takes the moon-letter form regardless of
  the engine name substituted after it.
- `Language_Toast_TargetUnsupportedFormat` → same `fil-magna {1}` device for the engine
  slot.
- `Settings_Hotkeys_Modifier_LeftFormat` / `RightFormat` → `"{0} tax-Xellug"` / `"{0}
  tal-Lemin"` — here the fixed noun is the side name itself (Xellug/Lemin), not the key
  name, and `ta'` contracts onto it (`ta'`+`ix-`=`tax-`, `ta'`+`il-`=`tal-`); `{0}` (the
  key name) sits safely in front, ungoverned by any article.

### 2. Where no fixed noun is available, move the slot behind a colon

Exactly the same three-plus-language convergence the brief describes for Finnish,
Polish, Ukrainian and French happened here too, on exactly the same keys:

- **`Language_ToggleSwitch_TranslateToFormat`** ("Translate to {0}") — "to X" in natural
  Maltese wants `għal` immediately followed by the language's own article
  (`għall-Ingliż`, `għat-Taljan`, `għall-Malti`), which is unknowable for a raw `{0}`.
  Restructured to **`"Traduzzjoni: {0}"`**.
- **`Language_Summary_Format`** ("You speak {0} → Parlotype types {1}.") — "speak in X"
  wants `bil-`/`bl-` fused with the language's article on `{0}`; "types Y" has the same
  problem on `{1}`. Restructured to **`"Titkellem: {0} → Parlotype tikteb: {1}."`** —
  independently landing on the identical fix Hungarian, Finnish, Polish and Ukrainian
  used for their version of this same key.
- **`Language_Toast_TargetUnsupportedFormat`** ("...Translation set to {2}.") — same
  `għal`+article problem as above on the third slot. Restructured to end with
  **`"...Lingwa tat-traduzzjoni: {2}."`**
- **`Settings_Hotkeys_Conflict_AlreadyBoundFormat`** ("{0} is already bound to {1}.") —
  "bound to X" wants `ma'` (which also fuses with the article: `ma'`+`il-`=`mal-`, etc.)
  directly before the mode name. Restructured to **`"{0} huwa diġà assenjat: {1}."`**

No other key needed either technique — every `Cloud_Error_*`/`Cloud_NotConfigured_*` key
already puts the provider-name placeholder first followed by a colon (the brief notes
this shape was deliberate in the English), and every bare language/engine name used as a
sentence subject (`{0} isn't a source in {1}`, `{0} can't translate`, `{0} is reserved:
{1}`) needed no article at all — Maltese does not require one on a proper-noun-like
subject at the head of a sentence, per the style guide's own rule that articles are
*not* used before proper names.

## Keys where the article collided with `{0}` — full list

As instructed, every key touched by this problem, regardless of which of the two
techniques resolved it:

1. `Transcribe_Status_RuntimeRestartRequiredFormat` — fixed-noun technique (runtime)
2. `Transcribe_Status_RuntimeUnavailableFormat` — fixed-noun technique (runtime)
3. `Settings_Runtime_RestartNoteFormat` — fixed-noun technique (runtime, twice)
4. `Language_Toast_SourceUnsupportedFormat` — fixed-noun technique (magna/engine)
5. `Language_Toast_TargetUnsupportedFormat` — fixed-noun technique (magna/engine) **and**
   colon restructure (translation-target slot)
6. `Settings_Hotkeys_Modifier_LeftFormat` / `Settings_Hotkeys_Modifier_RightFormat` —
   fixed-noun technique (side name)
7. `Language_ToggleSwitch_TranslateToFormat` — colon restructure
8. `Language_Summary_Format` — colon restructure
9. `Settings_Hotkeys_Conflict_AlreadyBoundFormat` — colon restructure

## Technical terms kept in English, and why

- **Settings, hotkey(s), tray, widget, clipboard, runtime, build, prompt(s), Output,
  Input, Toggle (mode name), Data, CPU** — no attested or confidently natural Maltese
  term exists for these that a Maltese-speaking Parlotype user would actually expect
  over the English word, per the brief's own allowance that technical nouns commonly
  stay in English in Maltese software. Longer surrounding sentences are still fully
  Maltese, so the 25-character identical-string check is not at risk from these (all
  are short).
- **Server, backend, filtru, driver(s)** — established Maltese IT loanwords, not flagged
  as uncertain.

## Things I was unsure about

Generous on purpose, per the brief's own instruction — this is the least-verified
language in the project.

1. **`dettatura`** (dictation) vs. **`dittatura`** (dictatorship) — a genuine one-vowel
   near-homophone risk. I am reasonably confident these are two distinct, correctly
   spelled words (parallel to English dictation/dictatorship not colliding either), but
   this is exactly the kind of short, load-bearing, repeatedly-used term a native
   speaker should double check first.
2. **`traskrizzjoni`** for transcription, and the decision to avoid coining a verb
   ("traskrivi"/"ittraskrivja") entirely — I could not confirm an attested Maltese verb
   for "to transcribe" and judged periphrasis (`tagħmel it-traskrizzjoni`, "does the
   transcription") safer than inventing one. A native reviewer may know an established
   verb I don't.
3. **`tray`** kept as a bare English loanword for "system tray" — I could not confirm
   whether Maltese Windows users have a settled native or loan term for this concept
   distinct from just saying "tray".
4. **`tast`** for a single keyboard "key" (used once, in `Settings_SilenceTimeout_HoldNote`:
   "iż-żamma tat-tast") — plausible from Italian *tasto* (parallel to `tastiera` =
   keyboard, which is definitely attested), but I could not independently confirm this
   specific singular noun is in real use rather than my own back-formation.
5. **`assenjazzjoni`** for "binding" (hotkey concept) — a reasonable loanword coinage
   from "assenja" (to assign), not independently confirmed; it does not appear as
   standalone UI text in this key set so the risk is contained.
6. **`Runtime ta' Whisper`** for "Whisper runtime" and **`Output ta' Whisper`** for
   "Whisper output" — grammatically safe (Whisper is a proper name, takes no article)
   but the resulting nav labels are noticeably longer than the tight English originals;
   worth a screenshot check for clipping.
7. **`Il-mod taż-żamma`** ("the hold-based mode", used in
   `Settings_SilenceTimeout_HoldNote` to refer to push-to-talk without repeating the
   full "żomm biex titkellem" phrase) — a shorthand I introduced for readability inside
   one sentence; confirm it reads unambiguously as "push-to-talk" and not as some other
   "holding" concept.
8. **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P, "Project display") — rendered
   as `"Projezzjoni tal-Iskrin"` (screen projection); I have no confirmation this matches
   what Maltese-language Windows (if it exists for this dialog) actually calls this
   specific action.
9. **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — kept as `"Menu Quick Link"`,
   English-labelled with a Maltese article/noun wrapper, since I found no evidence this
   menu has a settled Maltese name.
10. **Windows UI strings quoted verbatim in `Settings_Startup_BlockedBody`** ("Task
    Manager", "Startup apps", "Enable") — left in English throughout because I have no
    confirmation Windows actually ships a Maltese localization of these specific dialog
    names; if it does, these three should be swapped for the real strings.
11. **`Mudell li Jittraduċi`** / **`Language_ChooseTranslatingModel`** rendering ("Choose
    a model that translates" → "Agħżel Mudell li Jittraduċi") — grammatically fine but
    worth checking it doesn't read as clipped inside the button given Maltese's
    generally longer word forms.
12. **Gender agreement on "window" as feminine** (`din il-window`, `l-window
    attiva`) — inferred from the Microsoft style guide's own example
    (`"Agħlaq il-window tal-applikazzjoni attiva"`, where *attiva* is the feminine
    adjective form), and applied consistently throughout, but not independently
    cross-checked beyond that one example.
13. **`fowlder`** (folder) — the spelling is taken directly from a real Microsoft
    Maltese style-guide example sentence, but I only saw it in that one place, not
    confirmed as the universally preferred spelling over `direttorju` in every context.
14. Overall length: several longer body paragraphs (onboarding copy, settings
    descriptions) run noticeably longer than the English, consistent with normal
    Romance/Semitic-mix expansion, but I had no comparable-language measurement to check
    this against for Maltese specifically — worth a `check-localization.ps1 -Report`
    pass and a screenshot check once this lands.

## Length

No individual short label (`_Title`, `_Button`, `_Tooltip`) was left unnaturally long as
far as I could judge, but Maltese noun phrases with the article attached
(`Il-Hotkeys tad-Dettatura Attwali`, `Runtime ta' Whisper`) run longer than the terse
English originals more often than in Romance languages proper. Flagging for a screenshot
pass per item 6 above rather than shortening blind, per the brief's instruction to fix
clipping by resizing controls, never by cutting the translation.
