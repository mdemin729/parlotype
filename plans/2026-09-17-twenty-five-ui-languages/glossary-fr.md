---
title: French (fr) translation glossary
status: reference
created: 2026-09-17
---

# French (fr) glossary

Companion to `brief.md`. Choices made for the 389-key French translation in
`<scratch>/fr.json`.

## Register

**Vous** (formal), never **tu**. This matches the convention used by Windows,
macOS and virtually all professional desktop software in French — "Vous pouvez...",
"Vos raccourcis...". Used consistently across onboarding, tooltips, error text and
settings descriptions. Never mixed with tu-forms.

Imperative verb forms use the **vous** conjugation throughout: "Choisissez",
"Redémarrez", "Vérifiez", "Ajoutez-en un" — matching the imperative Windows itself
uses in French menus and dialogs ("Sélectionnez", "Cliquez").

## Quotation and typography convention

- **Guillemets** `«  »` for quoted terms and literal fragments, with a
  **no-break space (U+00A0)** immediately inside each mark: `« Cloud »`,
  `« {0} »`. This is the standard French convention (as used in `Strings.es.resx`'s
  Spanish analogue, but with French's own inner spacing — Spanish does not space
  its `«»`).
- **Narrow no-break space (U+202F)** before `:` `;` `?` `!` throughout — e.g.
  `Vos raccourcis : `, `l'enregistrement ; survolez`, `Nouveau sur Parlotype ?`.
  This is modern French digital typography (the convention Word, Windows and
  Apple's French locales use), distinct from the older Imprimerie-Nationale rule
  of a full no-break space in the same position.
- **No-break space (U+00A0)** between a number and a kept unit token
  (`670 MB`, `10 GB`, `{1} s`) — French typography always ties a number to its
  unit, even though the unit itself stays in English per the never-translate
  register (see below).
- **Typographic apostrophe** `’` (U+2019) everywhere — `n’importe`, `l’enregistrement`,
  `d’attente` — never the straight `'`.
- Em dash `—` (with plain spaces, no special nbsp treatment) used to mirror the
  English source's dash-separated clauses, e.g. status strings like
  `Fournisseur cloud indisponible — réessayez sous peu`.

## Never-translate register

Followed exactly as specified in `brief.md`: `Parlotype`, `Whisper`, `Parakeet`,
`Gemma 4`, `llama.cpp`, `llama-server`, `Vulkan`, `ONNX`, `OpenAI`, `Groq`, `xAI`,
`Grok`, `Ctrl`/`Alt`/`Shift`/`Win`/`Space`/`Esc`/`Tab`/`Enter`, `settings.json`,
`Setup.exe`, `vulkan-1.dll`, `HKCU Run`, `api.github.com`, and unit tokens
`GB`/`MB`/`ms`.

One deliberate note: **`GB`/`MB` are kept as literal English abbreviations**, not
converted to the French `Go`/`Mo`, per the brief's explicit instruction and
matching the precedent already set in `Strings.es.resx` (which keeps `MB`/`GB`
untranslated too, e.g. `Settings_Engine_Parakeet_Description`). This deviates from
strict French style guides but keeps the token consistent with what a user would
see in a file manager reporting the same download in English-labelled software.

`Windows` feature names are rendered with their real French Windows UI wording,
not literal translations, per brief.md's "prefer the terms Windows/macOS use":
- Task Manager → **Gestionnaire des tâches**
- Startup apps tab → **Applications au démarrage**
- File Explorer → **Explorateur de fichiers**
- Run dialog → **Exécuter**
- Task View → **Affichage des tâches**
- Windows Settings app → **Paramètres Windows** (capitalized, to distinguish
  the OS's own settings from Parlotype's `Paramètres`)
- Game Bar → **barre de jeu**
- Windows Voice Typing → **Saisie vocale**

## Core glossary

| English term | French | Notes |
|---|---|---|
| dictation | **dictée** | App's core verb throughout; "Parlotype — Dictée" as the widget window title. |
| transcribe | **transcrire** | Kept distinct from *traduire*. |
| translate | **traduire** | Never used for *transcribe*. |
| recording | **enregistrement** | Also used for "Recording..." status. |
| speech engine | **moteur de reconnaissance** (full: "moteur de reconnaissance vocale" on the page heading, shortened to "Moteur" in the nav) | One noun phrase throughout; page heading `Settings_Engine_Heading` gets the fuller form, `Settings_Category_SpeechEngine` and `Settings_Engine_Title` the short forms since they sit in tight nav rows. |
| model | **modèle** | |
| runtime | **runtime** (kept as an anglicism) | No natural French UI term exists for the Vulkan/CPU backend choice; French technical software (including dev tooling) uses "runtime" as-is. Flagged as a judgment call. |
| source language | **langue source** | |
| target language | **langue cible** | |
| hotkey | **raccourci** (full: "raccourci clavier" on the settings page title and page-name references) | Single base noun; "clavier" appended only where it names the settings page, matching how every `→ Hotkeys` cross-reference in the brief must read identically. |
| push-to-talk | **appui maintenu** | Mode badge; lowercase "appui maintenu" mid-sentence in conflict messages, matching English's own case-shift pattern (`Settings_Hotkeys_Conflict_Mode_PushToTalk`). |
| toggle | **bascule** | Mode badge / verb "basculer" for the toggle-between-modes tooltip. |
| binding | *(not a separate surfaced term — folded into "raccourci")* | No dedicated `_Binding` key exists in this key set; the concept is always expressed via "raccourci". |
| tray | **zone de notification** | The actual Windows French name for the system tray (not "barre d'état système", which is the macOS-flavoured term). |
| widget | **widget** (kept, anglicism) | Matches the Spanish translation's own choice to keep it unchanged; it is the accepted French tech term for this kind of floating UI panel. |
| waveform | *(no key currently surfaces this string alone)* | Not directly present as standalone copy in this key set. |
| wait time | **délai d'attente** | `Settings_SilenceTimeout_Title` → "Délai d'attente"; `Settings_SilenceTimeout_Description` uses "attendre"/"attente" consistently. |
| punctuation | **ponctuation** | |
| profanity filter | **filtre de grossièretés** | "grossièretés" used consistently for the masked words themselves too. |
| inject / typed into | **saisi (dans …)** | "le texte reconnu est saisi dans l'application…" — natural French phrasing per the brief's instruction, no invented technical term. |
| clipboard | **presse-papiers** | (No key in this set surfaces the clipboard directly — noted for completeness.) |
| cloud engine / cloud provider | **moteur cloud** / **fournisseur cloud** | "cloud" kept as the noun/adjective (widely used as a loanword in French consumer and enterprise software — Microsoft, Google and Apple's own French UIs all say "le cloud"), rather than the stiffer "informatique en nuage". The `Transcribe_CloudBadge` badge is simply **"Cloud"** (capitalized, kept as English), mirroring how the Spanish translation still marks it as a distinct badge ("Nube") — French keeps the loanword directly since "Cloud" alone is already standard in French tech UI copy. |
| API key | **clé API** | |
| onboarding | **visite guidée** | "Guided tour", matching the Spanish precedent (`Onboarding_WindowTitle` → "Visite guidée de Parlotype"). |
| restart required | **redémarrage nécessaire** | |

## Things I was unsure about

- **"runtime"** — there is no idiomatic French noun for "which backend executes
  the model" that isn't itself borrowed from English. I kept "runtime"
  untranslated throughout (`Runtime Whisper`, `runtime {0}`, `runtime CUDA`).
  A native French speaker on the team should confirm this reads naturally rather
  than "moteur d'exécution" or similar.
- **`Settings_Hotkeys_Reserved_QuickLinkMenu`** ("Quick Link menu", Win+X) — there
  is no single official French name for this menu in Microsoft's own
  localization; I used "Menu de liens rapides" as a literal, readable rendering.
  Worth checking against Microsoft's current French support docs if precision
  matters here.
- **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P) — Windows' own French
  UI calls the resulting pane "Projeter" (a single verb used as the pane title);
  I expanded it slightly to "Projeter l'affichage" for a full sentence fragment
  that reads naturally as a description rather than a button label. Flagging in
  case the shorter, more literal "Projeter" is preferred for exact terminology
  parity with Windows.
- **Cloud as a kept loanword** — I chose not to translate "cloud" to "nuage"
  anywhere (badge, glossary term, or body copy), on the grounds that "cloud" is
  now the dominant term in French consumer tech UI. This is a judgment call the
  maintainer may want to revisit; "nuage"/"informatique en nuage" is the
  Office québécois de la langue française's recommended term, but it reads as
  stiffer and less recognizable to a French Windows user than "cloud" does today.

## Placeholder / gendered-agreement note

French does **not** force gendered adjective agreement on most of the substituted
values in this key set, because:
- All language names are grammatically masculine in French (*le français*,
  *l'espagnol*), so no key that substitutes a language name into a sentence with
  a following adjective needed rephrasing.
- Verbs agreeing with a 3rd-person-singular subject (`{0} ne traduit pas`,
  `{0} est réservé`) don't inflect for gender in the present tense, so engine/
  provider names substituted as sentence subjects are safe as written.

One key **does** force a gendered choice that English doesn't mark, worth
flagging per the brief's instruction:

- **`Settings_Hotkeys_Modifier_RightFormat`** — `"Right {0}"` → `"{0} droit"`.
  French `droit`/`droite` agrees with the gender of the noun it modifies. Since
  `{0}` is always a keyboard key name kept in English (`Ctrl`, `Alt`, `Shift`,
  `Win`), I resolved this by treating all such untranslated key names as
  grammatically masculine in French (as they conventionally are treated —
  "le Ctrl", "le Shift") and used the masculine form `droit` uniformly. This is
  consistent and unambiguous in practice, but it is exactly the kind of
  agreement English's phrasing doesn't need to think about. (Its sibling,
  `Settings_Hotkeys_Modifier_LeftFormat` → `"{0} gauche"`, has no such problem:
  `gauche` is invariant for gender in French.)

No other key in the 389 needed rephrasing for this reason.

## Validation performed

Every key was checked programmatically against `fr.brief.json`: all 389 keys
present with no extras, every `{0}`/`{1}`/`{2}` placeholder set matches the
English source exactly, the literal template tokens `{speech_lang}` and
`{text_lang}` used inside prompt-help copy are preserved verbatim, and no value
over 25 characters matches the English source byte-for-byte.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Par défaut (transcription mot à mot)"** — verbatim/word-for-word rendered as *mot à mot (word-for-word)*, paired with the already-established *transcription* for transcription; no new terms introduced.
