---
title: Portuguese (pt) translation glossary
status: reference
created: 2026-09-17
---

# Portuguese (pt) glossary

Parlotype ships **one neutral `pt`** — served to `pt-PT` and `pt-BR` alike via .NET
satellite fallback. There is no `pt-BR` split (yet), so every choice below was made to
minimize friction for both audiences, defaulting to European Portuguese where a genuine
choice had to be made, per the brief.

## Register

- **Verb form:** infinitive/imperative "você-conjugation" without ever writing the
  pronoun — e.g. "Escolha um modelo", "Prima Esc", "Mantenha uma tecla premida". This is
  the register Windows and macOS use in PT-PT (their imperative UI copy uses the same
  verb endings as the formal/`você` conjugation, just without the pronoun), and it reads
  as neutral, unmarked instruction copy to Brazilian readers too — Brazilian software
  almost always addresses the user the same way. It avoids `tu` entirely, so there's no
  risk of the informal `tu` conjugation (`-as`, `-es`) clashing with `você`'s (`-a`, `-e`)
  if a future edit slips.
- Wherever English used a second-person pronoun that isn't load-bearing ("you can
  remove it yourself", "the app you were using"), I rewrote around it with a passive or
  impersonal construction ("essa entrada pode ser vista e removida...", "a aplicação que
  estava a utilizar") rather than reach for `você`/`o utilizador`. Keeps the tone
  consistent with the rest of the sentence-case, unmarked copy.
- Sentence case throughout, no exclamation marks, matching the English source.

## Quotation convention

**Guillemets, `«…»`**, no spaces inside — the standard Portuguese quotation mark (both
variants recognize and read them natively; Brazilian software typesets them less often
but every Brazilian reader parses them as quotes without friction). Used for: quoted UI
strings (`«Deteção automática»`), badge/status names referenced in running prose, and the
quoted ADR-032 line. Left plain ASCII single quotes on `'{0}'` in
`Cloud_BaseUrl_UnsupportedSchemeFormat`, since that quotes a raw URL scheme token, not
prose.

## Core glossary

| Term | Portuguese | Notes |
|---|---|---|
| dictation | **ditado** / ditar | The core verb throughout. |
| transcribe | **transcrever** | Kept distinct from traduzir everywhere. |
| translate | **traduzir** | Never used for transcribe. |
| recording | **gravação** | |
| speech engine | **motor de voz** | "Motor" alone used only where the surrounding heading already says "voz"/context is unambiguous (`Settings_Engine_Title` = "Motor"). |
| model | **modelo** | |
| runtime | **runtime** (kept, masc.) | Loanword — matches how Whisper.net's runtime concept is discussed in PT tech writing generally; "tempo de execução" would be a stilted calque here. `Vulkan`/`CPU` runtime names stay untranslated per the register. |
| source language | **idioma de origem** | |
| target language | **idioma de destino** | |
| hotkey | **atalho** (de ditado) | |
| push-to-talk | **premir para falar** | Badge/verb phrase, used identically everywhere (mode badge, conflict message, hint). |
| toggle (mode) | **alternância** (noun) / alternar (verb) | |
| tray | **área de notificação** | Official Windows PT-PT term. |
| widget | **widget** | Loanword, standard in PT UI writing. |
| wait time / silence timeout | **tempo limite de silêncio** | |
| punctuation | **pontuação** | |
| profanity filter | **filtro de linguagem imprópria** | "Linguagem imprópria" avoids the more casual/regional "asneiras" (PT-PT) or "palavrões" (BR-leaning), staying neutral and formal. |
| inject / typed into | **é escrito em** (natural phrasing) | No invented technical term, per the brief. |
| clipboard | **área de transferência** | Same in both variants — safe, no split needed. |
| cloud engine/provider | **motor / fornecedor na nuvem** | |
| API key | **chave de API** | |
| onboarding | **visita guiada** | Matches the "tour" framing of the onboarding window title. |
| restart required | **É necessário reiniciar** | |
| Settings (the app section) | **Definições** | See variant note below — this is the single biggest EU/BR fork in the whole file. |
| prompt(s) | **prompt(s)** (kept, loanword) | Matches the shipped Spanish file's approach — "prompt" is the term AI-literate PT users of both variants already use; "comando" or "instrução" would be actively confusing next to `Settings_Prompts_Editor_*` copy that also uses "instrução" for a different concept (the appended translate-instruction). |
| build (llama-server) | **compilação** | Formal MS/dev term for a software build, used consistently for llama-server and Parlotype "development build" alike. |
| backend | **backend** (kept, loanword) | Matches precedent in the shipped Spanish resx. |
| download (verb/noun) | **transferir / transferência** | See variant note below. |
| Cloud badge | **Nuvem** | Translated (not left as "Cloud") — "nuvem" is short, matches the glossary noun used everywhere else, and the comment only asked to keep it *short*, not untranslated. |

## European-vs-Brazilian choices

These are the specific forks the brief asked me to flag. All were decided **toward
European Portuguese**, as instructed, with the Brazilian alternative noted for a future
`pt-BR` split:

| Concept | Chosen (EU) | Brazilian alternative |
|---|---|---|
| screen | **ecrã** | tela |
| file | **ficheiro** | arquivo |
| user | **utilizador** | usuário |
| application/app | **aplicação** | aplicativo |
| Settings (as UI section name) | **Definições** | Configurações |
| download | **transferir/transferência** | baixar/download |
| control (noun, "your dictation control") | **controlo** | controle |
| delete | **eliminar** | excluir |
| desktop (Windows feature) | **ambiente de trabalho** | área de trabalho |
| driver (GPU) | **controlador** | driver (also common informally in EU, but "controlador" is the formal MS term I used consistently) |

Mouse/rato never came up — no key mentions a mouse. I did not introduce it speculatively.

Everything else (ditado, gravação, transcrever, traduzir, idioma, motor, modelo, chave,
nuvem, atalho, pasta, caminho, etc.) reads identically and naturally in both variants, so
no fork was needed there.

## Windows/macOS feature names (Settings_Hotkeys_Reserved_*)

Per the brief's instruction to "use the wording the user's own operating system uses,"
these use the actual PT-PT Windows 11 UI strings where I could recall them precisely
(Explorador de Ficheiros, Vista de Tarefas, Barra de Jogo, Reconhecimento de Voz do
Windows, Ditado por Voz). A couple (Projetar, Menu de acesso rápido, Mudar o idioma de
entrada) are my best-effort rendering of the corresponding Windows PT-PT feature and
should be checked against a live PT-PT Windows install before shipping, since I
translated from description rather than a screenshot.

## Placeholders forcing a gendered/case-agreement decision

- `Settings_CloudProviders_KeySavedBadge` ("✓ Saved" → "✓ Guardada") — this is a static
  string, not a placeholder substitution, but it silently assumes the implicit subject is
  feminine ("a chave" → guardada). No {0} involved, so no real risk, just noting the
  agreement choice for consistency if this badge is ever reused for a masculine noun.
- `Settings_Hotkeys_Gesture_HoldFormat` ("Manter {0} premido") and
  `Settings_Hotkeys_Gesture_DoubleTapFormat` — {0} is always a key/chord name (e.g. "Right
  Ctrl", "Space"), never inflected as a Portuguese noun, so the trailing "premido" stays
  fixed masculine singular regardless of what fills {0}. This works because key names are
  treated as opaque technical labels, not Portuguese nouns — flagging only because it's
  exactly the shape of problem the brief warns about, even though it isn't actually
  broken here.
- No other key forces a Portuguese gendered agreement onto a substituted `{0}`/`{1}`
  value; all are either engine/provider/language names placed after a colon or dash
  (matching the placeholder-position guidance) or numeric/technical values.

## Unsure / flag for review

- **`Settings_Hotkeys_Reserved_ProjectDisplay`, `_QuickLinkMenu`,
  `_SwitchInputSource`** — translated from the English description of the Windows
  feature ("Project", the Win+X menu, and language-bar switching) rather than from a
  live PT-PT Windows string I could verify character-for-character. Worth a native PT-PT
  Windows check before ship.
- **`Settings_Prompts_Help_PlaceholdersHeading` / `_Editor_PlaceholdersLabel`** —
  "Marcadores de posição" is the correct formal Microsoft term for "placeholders" but is
  noticeably longer than the English. Given the tight-space warning in the brief, a
  shorter alternative might be wanted if this heading turns out to clip in the UI; I did
  not shorten it myself since I couldn't verify layout.
- **`Settings_Hotkeys_Reserved_LockWorkstation`** ("Bloquear o computador") — Windows
  PT-PT actually labels the Ctrl+Alt+Del option "Bloquear", with "computador" implicit
  from context. I added "o computador" for clarity as a standalone string (this one
  appears without surrounding UI chrome, unlike on the actual lock screen), which is a
  judgment call rather than a verbatim OS string.
- I did not find any key where the English forces a Portuguese gendered agreement onto a
  substituted `{0}` that I could not route around with a colon/dash placement — see
  the section above.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Predefinição (transcrição literal)"** — verbatim/word-for-word rendered as *literal*, paired with the already-established *transcrição* for transcription; no new terms introduced.
