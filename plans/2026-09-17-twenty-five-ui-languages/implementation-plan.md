---
title: Implementation plan — twenty-five UI languages
status: planned
created: 2026-09-17
---

# Implementation plan

Companion to [task.md](task.md). Facts this plan relies on are verified in
[research.md](research.md); where the two disagree, research.md wins and this file gets
corrected.

## Baseline (measured 2026-09-17)

| Fact | Value |
|------|-------|
| Keys in `Strings.resx` | 389 |
| Keys carrying `{0}`-style placeholders | 47 |
| Keys carrying a `<comment>` | 140 (neutral), 138 (`ru`) |
| Values longer than 120 chars | 36 |
| Satellite files today | `ru`, `es` — both 389 keys |
| Key **order** | Identical in all three files, and *not* alphabetical — it is authoring order |
| Hardcoded-literal baseline | `pendingFiles: {}` — every `.axaml` is already clean |

The order fact matters: the importer must emit keys in the neutral file's order, not
sorted, or every future diff is unreadable.

---

## Phase 0 — The pipeline

Two scripts in `scripts/`, matching the house style of `gen-strings.ps1` (comment-block
synopsis, `$ErrorActionPreference = 'Stop'`, `-Check` style switches).

### `export-translation-brief.ps1`

```
pwsh scripts/export-translation-brief.ps1 -Culture de -Out <path>
```

Reads `Strings.resx` and emits JSON:

```json
{
  "culture": "de",
  "sourceKeyCount": 389,
  "entries": [
    { "key": "Settings_Theme_Title", "en": "Theme" },
    { "key": "Settings_InterfaceLanguage_SystemDetailFormat",
      "en": "Follows Windows — currently {0}",
      "comment": "{0} is the language name in its own language, e.g. \"Русский\".",
      "placeholders": [0] }
  ]
}
```

- `comment` and `placeholders` omitted when absent, to keep the brief small.
- `-Missing` switch: when `Strings.<culture>.resx` already exists, emit only the keys it
  lacks. This is the mode every *future* string change uses, and the reason the script
  exists beyond this plan.
- Default output path is the session scratchpad, never the repo.

### `import-translations.ps1`

```
pwsh scripts/import-translations.ps1 -Culture de -In <path> [-Merge]
```

Reads the neutral resx plus a flat `{ "key": "translation" }` map and writes
`src/Parlotype.Desktop/Resources/Strings.de.resx`.

Rules the script enforces — it **throws rather than writing** on any of these:

1. Every neutral key is present in the input; no orphan keys.
2. Placeholder index set per key matches the neutral value exactly.
3. No value is empty or whitespace.
4. No value >25 chars is byte-identical to the English — the same rule
   `NoKeyIsAccidentallyUntranslated` enforces, caught here where the error message can
   name the key and the language in one line.

Output shape, copied exactly from the existing `Strings.ru.resx`:

- Same XML declaration, `<root>`, `resheader` block, and `xsd:schema`.
- `<data name="…" xml:space="preserve">` in **neutral-file order**.
- `<comment>`: a comment the satellite **already has is kept**; the neutral file's is used
  only where the satellite has none. See the round-trip finding below — this started as
  "always clone the neutral comment" and that was wrong.
- UTF-8 without BOM, **CRLF** line endings (the tree is CRLF under `core.autocrlf=true`),
  XML-escaped values.

`-Merge` keeps existing translations and applies only the supplied keys, for the
`-Missing` round trip.

### Proof that the pipeline is correct

```bash
pwsh scripts/export-translation-brief.ps1 -Culture ru -Out $SCRATCH/ru.brief.json
# extract the value map from the existing resx, feed it back
pwsh scripts/import-translations.ps1 -Culture ru -In $SCRATCH/ru.json
git diff --stat src/Parlotype.Desktop/Resources/Strings.ru.resx
```

### What the round-trip actually caught — *(run 2026-09-17)*

The proof earned its place immediately. Three findings, in increasing order of how badly
they would have gone unnoticed:

1. **The key count is 389, not 392.** A `grep -c` disagreed with every parser. Corrected
   throughout the plan; research.md §5 carries the note.

2. **XML load normalizes CRLF to LF, so the first output was mixed-ending.** An XML parser
   rewrites every line break to `\n` on load per spec, and `NewLineHandling.None` then
   wrote that out verbatim against a CRLF tree. Fixed with `NewLineHandling.Replace` plus
   `NewLineChars = "\r\n"`, which also restores the two values that contain a real newline
   (`Onboarding_Tray_Body`, `Settings_Data_DeleteDialog_BodyFormat`). Git's `autocrlf`
   hides this in `git diff`, so it would have shipped silently.

3. **Cloning the neutral `<comment>` destroyed real information.** This is the one that
   mattered. `Strings.ru.resx` carries comments the neutral file cannot hold — notes about
   *Russian*: "...so Russian uses the genitive here", and a `WaitFormat` note that
   deliberately drops the neutral's reference to the `"s"` unit because Russian prints
   `с`. The first importer overwrote all five with the generic English note. A satellite's
   comment is guidance for whoever translates *that language* next, so the rule is now:
   keep the satellite's, fall back to the neutral's. New languages, having none, inherit
   the full neutral set as intended.

After the fix the diff across both files is exactly **two added comments each** — keys
added to the neutral file after `ru`/`es` were translated, which arrived with comments the
satellites never got. That is the deliberate normalization this step was meant to surface,
and it lands with Phase 0.

Commit: `build(l10n): translation brief export/import pipeline`.

---

## Phase 1 — Guardrails that survive 25 languages

### 1.1 `deliberatelyIdentical` becomes per-language

`LocalizationParityTests.NoKeyIsAccidentallyUntranslated` (line ~105) currently holds one
global `string[]`, empty today. Across 22 more languages some strings *will* legitimately
match English — brand fragments, and technical borrowings in the smaller languages. A
global list would then excuse the key in **every** language, including ones that simply
forgot it.

Change it to `Dictionary<string, string[]>` keyed by culture name, with a comment stating
that an entry is a claim about one language only and needs a stated reason.

### 1.2 A fact that the registry is well-formed

New test in `LocalizationParityTests`, looping `SupportedUiLanguages.All`:

- `CultureInfo.GetCultureInfo(name)` succeeds and returns a **neutral** culture.
- The endonym is non-empty, and distinct across the whole list — a copy-paste slip that
  labels Slovak "Slovenščina" is otherwise invisible until a user reports it. Slovak and
  Slovenian are the pair most likely to suffer it.
- `MatchSystemCulture` round-trips each culture back to itself.

**Do not assert the endonym equals `CultureInfo.NativeName`.** Research §1 measured this:
.NET returns `français`, `čeština`, `polski` lowercase, because those languages lowercase
their own name in running prose. The repo already committed to the opposite convention for
a *list label* — it ships `Español`, not `español`, which is the same choice. So 16 of the
22 endonyms differ from `NativeName` by their first letter, on purpose, and a test that
pins them to `NativeName` would force the wrong copy into the picker.

### 1.3 Advisory expansion report

Add `-Report` to `check-localization.ps1`: for each culture, print the keys whose
translation exceeds ~1.6× the English character count, worst first. **Advisory only** — it
must not fail the build, because some languages are legitimately longer and a hard
threshold would be gamed by shortening good translations. It exists to aim the Phase 9
screenshot pass at the strings most likely to clip.

Leave the culture-scraping regex alone — `[a-z]{2}(?:-[A-Za-z]+)?` already matches every
two-letter code in the set. Confirm against research.md §4.

Commit: `test(l10n): guardrails for a 25-language registry`.

---

## Phase 2 — Interface-language picker at 26 rows

`InterfaceLanguageSettingsView.axaml` renders `LanguageOptions` through an `ItemsControl`
inside a `StackPanel`. The settings content area already has a `ScrollViewer`
(`SettingsWindow.axaml:179`), so 26 rows scroll rather than clip — the page is not broken
today, it is just long.

Changes, smallest that does the job:

1. Keep "System default" pinned first, visually separated from the list by a thin
   separator. It is a mode, not a language, and at 26 rows it stops reading as one.
2. Sort the 25 language rows. **Sort by endonym under the invariant culture**, not by
   English name — the user is scanning for their own language's spelling. Greek and
   Cyrillic endonyms will sort after the Latin ones; that is correct and predictable.
   Do **not** sort by the current UI culture, or the list reorders itself as you switch
   languages.
3. Swap `ItemsControl` for `ItemsRepeater` **only if** a rendering measurement shows 26
   templated buttons costing anything. At this size it almost certainly does not; measure
   before adding virtualization.
4. No search box. 26 rows sorted by endonym is scannable, and a search box needs a
   localized placeholder that a user stranded in the wrong language cannot read.

Ordering lives in the view model, not the registry — `SupportedUiLanguages.All` stays in
declaration order so the parity tooling and the ADR narrative read naturally.

Verify by rendering the page with a stub registry of 25 rows before any translation
exists, so this phase is genuinely independent of the waves.

Commit: `feat(settings): interface-language picker handles 25 languages`.

---

## Phase 3 — The shared brief

Create `plans/2026-09-17-twenty-five-ui-languages/brief.md`, the document every
translation subagent is handed alongside the JSON. It contains:

**Do-not-translate register** — lifted from the `localization` skill and made concrete:
`Parlotype`, `Whisper`, `Parakeet TDT v3`, `Gemma 4`, `Vulkan`, `sherpa-onnx`,
`llama.cpp`, `OpenAI`, `Groq`, `xAI`, `Grok`, `settings.json`, `%LOCALAPPDATA%`, model
identifiers (`gpt-4o-mini-transcribe`, `Large v3 Turbo`), and the keyboard key names
`Ctrl` `Alt` `Shift` `Win` `Space` `Esc` — key names stay, the prose around them does not.

**Core glossary (English, ~25 terms)** — one authoritative English definition each, so a
translator picks one word per concept and keeps it: dictation, transcribe, recording,
speech engine, model, source language, target language, translate, hotkey, push-to-talk,
toggle, tray, widget, waveform, wait time, punctuation, profanity filter, injection,
clipboard, cloud provider, API key, download, restart required, onboarding, runtime.

**Style rules** — terse, sentence case, no exclamation marks, locale typography
(`«…»` for `ru`/`bg`/`uk`, `„…"` for `de`/`cs`/`sk`/`hr`/`sl`/`hu`/`lt`/`lv`/`et`,
`«…»` for `fr` with its non-breaking spaces, `"…"` for `nl`/`da`/`sv`/`fi`/`pl`/`ro`/`el`/`mt`
— each subagent confirms its own and records it in its glossary), and the ADR-064 rule
that a placeholder must never be inflected: if a substituted noun would need a
grammatical case the language marks, **say so in the report** rather than guessing. That
is a signal the English needs rephrasing, which is a change to the neutral file and
therefore a decision for the maintainer, not the subagent.

Commit: `docs(l10n): shared translation brief and core glossary`.

---

## Phases 4–8 — The translation waves

| Wave | Languages | Rationale for the grouping |
|------|-----------|----------------------------|
| A | `de` `fr` `it` `pl` `uk` | Largest user base, highest-confidence translation, and `uk` can lean on the existing `ru` terminology sheet for parallel structure while deliberately avoiding russianisms |
| B | `pt` `nl` `sv` `da` `fi` | `sv`/`da` share Nordic conventions; `fi` is the outlier and the longest expander |
| C | `cs` `sk` `ro` `el` `hu` | `cs`/`sk` are close kin and must agree; `el` is the only new script |
| D | `bg` `hr` `sl` `lt` `lv` | `hr`/`sl` and `lt`/`lv` are the two pairs that most need to be decided together |
| E | `et` `mt` | The two smallest, where machine translation is weakest — last, so every convention is already settled |

### Per-wave procedure

1. `pwsh scripts/export-translation-brief.ps1 -Culture <c> -Out $SCRATCH/<c>.brief.json`
   for each language in the wave.
2. Spawn one **Sonnet 5** subagent per language, in parallel, with the prompt template
   below.
3. For each returned file: `pwsh scripts/import-translations.ps1 -Culture <c> -In …`.
   The importer rejects placeholder drift, blank values and untranslated strings before
   anything reaches the repo.
4. Add the wave's rows to `SupportedUiLanguages.All`, using the endonyms from
   research.md §1.
5. `pwsh scripts/check-localization.ps1` → `pwsh scripts/gen-strings.ps1` (expect **no**
   diff) → `dotnet build Parlotype.slnx` → `dotnet test src/Parlotype.Desktop.Tests`.
6. Spot-read ~15 strings per language by eye, weighted toward the 47 placeholder keys and
   the 36 long values — those are where a plausible-looking translation goes wrong.
7. Commit and open one PR per wave.

### Subagent prompt template

> You are translating the Parlotype desktop app's interface into **\<language>**
> (`<culture>`).
>
> Read `<path>/<culture>.brief.json` — 389 entries, each with `key`, `en`, an optional
> `comment` written for you by the developer, and an optional `placeholders` list. Read
> `plans/2026-09-17-twenty-five-ui-languages/brief.md` for the do-not-translate register,
> the core glossary and the style rules. Read
> `.claude/skills/localization/SKILL.md` sections "Composite formats", "Keyboard key
> names" and "Translation quality bar".
>
> Write two files:
>
> 1. `<scratch>/<culture>.json` — a flat JSON object, `{"key": "translation"}`, with all
    389 keys and nothing else. No wrapper, no comments, no extra keys.
> 2. `plans/2026-09-17-twenty-five-ui-languages/glossary-<culture>.md` — the core glossary
>    terms with the word you chose for each, your quotation-mark convention, and any term
>    you were unsure about.
>
> Hard rules:
> - Preserve every `{0}`/`{1}` exactly. Never add or drop one, never reorder the *set*
>   (you may move them within the sentence).
> - Do not translate anything in the do-not-translate register.
> - One word per concept, for the whole file. Check your glossary before inventing a
>   synonym.
> - Register: terse, sentence case, no exclamation marks. Match the English's brevity —
>   this copy sits in a small window.
> - Never leave English in place as a placeholder. If you genuinely cannot translate a
>   string, translate it as best you can **and list the key in your final report**.
>
> Report back: the count of keys written, the quotation convention you used, any key whose
> English wording forces a grammatical case your language marks and which therefore ought
> to be rephrased in English, and anything you were unsure of.
>
> Do not edit any file in `src/`. Do not run the build.

Keep each wave's subagents to one language each — a subagent handed two languages blends
their terminology.

---

## Phase 9 — Verification

1. `pwsh scripts/check-localization.ps1 -Report` → take the worst-expanding locales.
   Research §5 nominates **German** primary and **Finnish** secondary (compounding and
   agglutination hit short labels hardest), plus **Greek** as a glyph-width check
   distinct from the Cyrillic width `ru` already exercises. Treat that as where to look
   first, not as a measurement — it is general localization guidance, and the report is
   the actual evidence once translations exist. Note the report will *understate* risk on
   the 36 long values and overstate it nowhere: expansion is worst on short labels
   ("Theme", "Startup"), not on the 403-char onboarding body.
2. Render the settings screenshot tests under those cultures and **read the images**.
   Do not render all 19 pages × 25 languages; that is ~475 screenshots and no one will
   look at them. Aim at the pages carrying the report's worst offenders.
3. Fix clipping by making controls content-sized, never by shortening a translation —
   ADR-064's rule, and the reason the expansion report is advisory.
4. `dotnet run --project src/Parlotype.Desktop` and switch live into German, Greek and
   one Cyrillic language (Ukrainian or Bulgarian): check the settings nav pane rebuilds,
   the tray menu follows, the Transcribe widget's hotkey tooltip is translated, and Greek
   renders without tofu. No embedded font ships — the app uses the system UI font, which
   on Windows covers Greek and Cyrillic — so this is a confirmation, not an expected
   failure.
5. Measure publish size before and after; record the delta in the ADR. Research §3
   predicts **≈1.26 MB, ≈0.45 % of the 278 MB app** — extrapolated from Debug satellites
   at 53–65 KB each, since no current Release output carries any. Confirm against a real
   publish; a wildly different number means something is packing more than expected.

---

## Phase 10 — Documentation

1. **ADR-069** (`docs/decisions/069-twenty-five-ui-languages.md`, next free number —
   068 is the highest today). No ADR *trigger* strictly fires: no new Core type, no DI
   registration, no new dependency. It is warranted anyway, because the thing worth
   recording is the **rule** — the interface language set tracks Parakeet's speech set —
   together with the translation-provenance policy, the brief/import pipeline, the
   decision to keep Russian as the single canary locale, and the standing cost that every
   future UI string now needs 24 translations.
2. **`localization` skill** — this is the part most likely to be skipped and most likely
   to be missed later. "Adding a string" must stop telling an agent to hand-edit two resx
   files and start telling it to run `export-translation-brief.ps1 -Missing` and
   `import-translations.ps1`. "Adding a new language" shrinks to: one registry row, one
   brief, one subagent, one import.
3. **Memory vault** — `memory/services/parlotype-desktop.md` gains the two scripts;
   `memory/decisions/_index.md` gains ADR-069; `memory/architecture/subsystems.md`'s
   localization section gains the pipeline. Anything learned about .NET satellite
   fallback or a specific language's behaviour goes to `memory/knowledge/` with an index
   row.
4. **CHANGELOG.md** — one user-facing line naming the count, not the 22 codes.
5. Close the plan per [WORKFLOW.md](../WORKFLOW.md): `status: completed`, remove the row
   from `INDEX.md`.

---

## Risks

| Risk | Mitigation |
|------|-----------|
| **Plausible-but-wrong translations.** An LLM translation reads fluently while using the wrong term for "engine" or "injection". Nothing in the build can see this. | The per-language glossary makes the choice explicit and reviewable in the PR. Native-speaker review tracked as follow-ups. Named honestly in the ADR rather than implied away. |
| **Placeholder damage** → `FormatException` in production, in a language nobody on the team reads. | Caught three times: importer, `check-localization.ps1`, `LocalizationParityTests`. This risk is genuinely closed. |
| **Text expansion clipping** — German ~35 % longer, Finnish and Hungarian comparable. | Advisory expansion report aims a targeted screenshot pass; fix by content-sizing controls. |
| **Maintenance drag** — the next UI string costs 24 translations. | The `-Missing` brief mode is exactly this workflow; Phase 10 puts it in the skill so it is the default path, not a thing to remember. |
| **Scope creep into speech-language names.** ~99 speech languages × 25 locales is not a translation task this project owns. | Explicitly out of scope; they come from ICU via `CultureInfo`, per the skill. |
| **Wave PRs going stale against each other.** Each wave touches `SupportedUiLanguages.All`. | Rows are appended; conflicts are one-line and trivial. Land waves in order rather than in parallel. |
| **A future RID could silently eat a locale.** `RemoveForeignRuntimeAssetsFromPublish` (ADR-051) filters publish files by *immediate parent directory name* against a hardcoded RID list. Satellite assemblies live in `<culture>/`. No collision exists today — research §3 checked all 22 against the list — but the two namespaces are one unlucky RID apart. | Not a blocker, and not worth pre-emptive code. Record it in ADR-069 as a known adjacency so whoever next edits that RID list sees why it matters. |
