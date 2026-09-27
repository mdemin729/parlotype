---
status: accepted
date: 2026-09-18
---

# 069. The Interface Ships in 25 Languages, Through a Translation Pipeline

## Context

[ADR-064](064-ui-localization-foundation.md) built the localization foundation and shipped
three languages — English, Russian, Spanish — with a promise attached:

> Adding a language is one row in `SupportedUiLanguages.All` plus one resx file. Nothing in
> markup, view models or the settings UI changes. If that turns out to be false, the
> abstraction leaked and that is the bug to fix.

Meanwhile the default speech engine, Parakeet TDT v3, transcribes 25 European languages and
auto-detects between them with no language UI at all ([ADR-041](041-parakeet-v3-sherpa-onnx.md) /
[ADR-042](042-parakeet-default-language-ux.md)). A Czech user could dictate Czech on day
one — into an interface that spoke English at them.

Closing that gap means 22 more languages. At 389 keys each that is 8,558 strings, and from
then on **every new UI string costs 24 translations instead of 2**, permanently, with the
`Stop` hook and `LocalizationParityTests` refusing to let a session ship without them.

## Decision

### The interface language set tracks Parakeet's speech set

Stated as a rule rather than left implicit, because it is a choice and not a derivation:
Whisper covers ~99 languages, so a Japanese or Mandarin Whisper user still gets an English
interface. That is accepted — Parakeet is the default and the 25 are where the users are —
but the set has a reason, and a future request for a 26th language should be answered
against that reason rather than ad hoc.

### Nobody hand-edits a satellite resx

At two languages, editing every file by hand was a chore. At twenty-four it is not
something a person or an agent does: each file is ~80 KB of XML with escaping rules and
`xml:space="preserve"` on every node, and an agent asked to emit that fails in ways the
parity checks describe badly.

Two scripts bracket the translation instead:

```
Strings.resx ──export-translation-brief.ps1──▶ <culture>.brief.json
                                                      │
                                              (translator works)
                                                      ▼
Strings.<culture>.resx ◀──import-translations.ps1── <culture>.json
```

The brief carries, per key, the English value, the developer's `<comment>` and the
placeholder indices. The translator returns a flat `{"key": "translation"}` map and nothing
else. **The importer owns every byte of XML** and refuses to write on a missing or orphan
key, a placeholder mismatch, a named-token mismatch, an empty value, or a value over 25
characters left verbatim in English.

`-Missing` plus `-Merge` make a one-string change one small brief per language rather than a
full pass. That is the mode that matters from here on: the initial 22 languages are a
one-off, the 391st key is forever.

### One translator per language, and the brief is sliced, never the work

Each language gets a single translator that writes `glossary-<culture>.md` **first** —
fixing the term for every core concept, the formality register, the quotation convention —
and then follows it across all 389 strings. Splitting the work between translators splits
the terminology along the same line.

A translator's *reply* has an output-token ceiling, and 389 values in one response can
exceed it: Finnish did, and died without writing a byte. So `export-translation-brief.ps1`
takes `-Part N -Of M`, the importer accepts several files and merges them, and a key
appearing in two of them is **refused** — that means the slices overlapped and one
translation would be discarded in silence.

### Translations ship as first-class, with no "machine translated" badge

A disclaimer invites users to distrust copy they cannot compare against anything.
Native-speaker review is tracked per language as follow-up work, not as a gate on shipping.
**Maltese is the first candidate**: the project can verify it least — no second Maltese
pass exists and nobody on the team reads it — and its own translator returned fourteen
flagged uncertainties, including a hazard we would never have caught (`dettatura`,
dictation, against `dittatura`, dictatorship, one vowel apart).

### Russian stays the single canary locale

`LocalizationTests` asserts that Core-originated copy renders as Cyrillic under `ru`, which
catches an enum member added without its resx key — an omission no parity check can see,
because it is missing from *every* language at once and the languages therefore still
agree. Generalizing that assertion 24 ways buys nothing.

## Findings

These cost real time to establish and are the reason this ADR is long.

### Twelve languages say three English strings are wrong

`Language_Summary_Format` ("You speak {0} → Parlotype types {1}."),
`Language_ToggleSwitch_TranslateToFormat` and `Language_Toast_TargetUnsupportedFormat` force
an uninflectable `{0}` into an oblique case. Twelve languages — `fr` `uk` `pl` `fi` `hu`
`sk` `cs` `el` `lt` `hr` `sl` `lv`, four families, none seeing another's output —
independently restructured them, almost all by moving the slot behind a colon.

Two observations turn a tally into a controlled comparison:

- **Bulgarian did not need the fix.** It is Slavic, like `pl` `cs` `sk` `sl` `hr` `uk` `ru`,
  every one of which did — and it is the one that lost its case system. Romanian reported
  the same from the Romance side, explicitly distinguishing "not grammatically forced" from
  "I preferred it anyway". The variable is case marking, not family and not translator
  temperament.
- **Polish knowingly shipped it fragile.** `Tłumacz na {0}` wants an accusative the
  substituted value will never carry; its translator reported this as an accepted risk
  rather than solving it. That defect is live in the tree and **cannot be fixed in Polish** —
  only in the English.

The fix is to rephrase the neutral file. That is a maintainer decision and is **not done
here**; this ADR records the evidence so it is not rediscovered a thirteenth time.

**Three techniques are in use**, worth knowing before rewording so the new English does not
defeat them:

| technique | solves | languages |
|---|---|---|
| Colon / label — `Käännä: {0}` | case | universal fallback |
| Carrier noun — `{0} vykdymo aplinka` | case, when a native noun can take the ending | `fi` `hu` `lt` `lv` `et` `bg` `ro` |
| Classifying noun — `Kalba {0}` | unknown **gender**, which neither of the others touches | `pl` `lt` `lv` `cs` |

Hungarian also contributed `a(z)` — its own written convention for an article before an
unpredictable word — and Maltese faced the same problem with **ten** outcomes (`id-`, `is-`,
`it-`, `ix-`, `iż-`, `iċ-`, `in-`, `ir-`, `il-`, elided `l-`) and no such convention.

### Getting a registry row right

- **Neutral cultures only** (`pt`, never `pt-PT`). .NET's satellite fallback serves every
  region from a neutral file and never the reverse, so a regional code strands everyone
  else on English.
- **`CultureInfo.GetCultureInfo` is not a validity check.** Given any well-formed code it
  *manufactures* a culture rather than throwing: `"zz"` succeeds, and so does `"ls"` typed
  for `"sl"`. `EveryRegisteredLanguage_IsAWellFormedNeutralCulture` asserts membership in
  `CultureInfo.GetCultures(CultureTypes.NeutralCultures)` instead — verified by watching it
  pass on `new("zz", "Nonsense")` before it was written correctly.
- **The endonym is not `CultureInfo.NativeName`.** .NET returns `français`, `čeština`,
  `polski` lowercase, because those languages lowercase their own name in prose. As a list
  label this app capitalizes — it shipped `Español`, not `español` — so 16 of 22 endonyms
  differ from `NativeName` by their first letter, deliberately.

### Six settings pages did not follow a live language switch

A pre-existing ADR-064 defect that 22 languages made visible, and **two of the six were
reported by a user, not caught by us**: Help, Updates, Cloud providers, Data, llama.cpp and
Startup all composed copy in C# and never refreshed it, so each kept the language the app
started in for the life of the process. Updates was worst — it only rewrites its text on
`StatusChanged`, which on a development build never fires at all.

Three of them needed more than a hook: they stored the finished string and no longer knew
what produced it. They now retain the **input** — a status label, a measured byte count, the
effective launch state — and derive the text from it, so re-rendering has no side effects. A
language switch must not probe a server or walk the models directory.

Guarded structurally, because discipline was demonstrably not enough:
`EverySettingsSection_ThatComposesCopyInCSharp_RefreshesItOnCultureChange` fails the build
for a settings view model that touches `Strings.` beyond its own `Title` without overriding
the hook.

### An exemption silences the class, not the string it was written for

`PromptSettingsViewModel` was exempt from that test for a good reason: its only `Strings`
use named a *copied* prompt, which becomes the user's data and must never be re-translated.
Phase 9 found a second use the exemption had been covering — the built-in prompt's display
name, hardcoded English in `JsonPromptTemplateRegistry` and reaching the screen untranslated
in all 24 languages. Nothing could see it: not in AXAML, so the literal scan missed it; not
in resx, so parity missed it. It was found by rendering the Prompts page under Hungarian and
reading it.

Fixed the ADR-064 way — Platform supplies the identity (`IsBuiltIn`), Desktop chooses the
words — and the exemption list is now empty. **Prefer overriding the hook and narrowing
inside it over exempting a class.**

### A placeholder check must ask `string.Format`, not count braces

Review found the validator's own blind spot, and it was the worst defect in the change.
Comparing `\{(\d+)` index sets between the English and a translation waves through **three**
distinct failures, all of which produce the same index set as a correct original:

| translation | brace regex sees | what actually happens |
|---|---|---|
| `Schritt {0x} von {1}` | `0,1` | `FormatException` at runtime |
| `Schritt {0 von {1}` | `0,1` | `FormatException` at runtime |
| `Schritt {{0}} von {1}` | `0,1` | `{{0}}` is a **literal** `{0}`; argument 0 is silently dropped |

So validity is now decided by calling `string.Format` — the parser that runs in production —
and parity compares a **signature**: which sentinel arguments survive substitution. That is
the only definition that treats an escaped brace correctly. `scripts/lib/CompositeFormat.ps1`
is shared by the export, import, generation and check scripts; `LocalizationParityTests`
carries the same rule in C#, since the two languages cannot share code.

One refinement came from running the new check over the real tree rather than over test
cases: it flagged `Settings_Prompts_Help_BuiltInBody` in all 25 languages at once. That key
documents the Gemma prompt syntax and contains `{speech_lang}`/`{text_lang}`; it is never
passed to `string.Format` at all. Named tokens are therefore masked before validation — with
a pattern requiring a leading letter, so `{0x}` is still caught.

### Named tokens are not placeholders

`Settings_Prompts_Help_BuiltInBody` documents `{speech_lang}` and `{text_lang}`, which the
Gemma prompt engine substitutes **by name**. Translating a word inside those braces breaks
prompt substitution silently: not a numbered placeholder, not a wholly untranslated string,
invisible to every check. The importer now rejects a named-token mismatch outright.

### Declension is not translation

Finnish and Hungarian cannot leave a name bare where grammar demands an ending, and
`Parlotypessa` or `Visual Studióban` is correct where a bare stem would be ungrammatical.
The rule is that **the stem survives and stays recognizable**. A sweep across all 24
languages found every protected identifier intact except `Visual Studio`, declined by Czech
and Hungarian — accepted deliberately.

### Measured expansion, replacing the prediction

Standard guidance nominates German and Finnish as worst. Both are mid-pack. Measured over
all 24 languages:

| | | | |
|---|---|---|---|
| da 1.08 | et 1.12 | sv 1.12 | sl 1.13 |
| cs 1.13 | ru 1.15 | hr 1.16 | sk 1.17 |
| pl 1.17 | fi 1.17 | nl 1.18 | bg 1.18 |
| uk 1.18 | ro 1.18 | mt 1.19 | lv 1.19 |
| pt 1.21 | hu 1.22 | it 1.22 | lt 1.22 |
| de 1.23 | es 1.24 | **el 1.29** | **fr 1.30** |

German's `du` register and Finnish's compounding both *cut* length. Aim a layout review with
`pwsh scripts/check-localization.ps1 -Report`, not with received wisdom. The worst *single*
strings are always short labels — a six-character button doubles while a 300-character
paragraph grows by a third.

### What no check can catch

`NoKeyIsAccidentallyUntranslated` only fires above 25 characters, because `Updates` is
correct German **and** Dutch, and `Audio`, `Model`, `OK` and `Auto` come out unchanged in
several languages. That threshold exists to stop false alarms drowning the signal, and the
cost is that **a genuinely skipped short string cannot be caught automatically**. That gap
is covered by the per-language glossaries and by human review. It is worth saying plainly
when someone reports a short string as "not translated" — twice now, the report has been
about correct copy.

## Consequences

**Every future UI string costs 24 translations.** Non-negotiable and enforced. The
`-Missing`/`-Merge` path is what makes it a small brief per language rather than a full
pass; Phase 9 exercised it end-to-end on one key.

**Adding the 26th language is one row and one pipeline run.** ADR-064's promise held in
the sense that matters: **no per-language** markup, view model or `.csproj` change was
needed for any of the 22, and `Strings.cs` did not move while they were added.

Stated precisely, because the loose version is false and a reviewer caught it: this branch
*does* change a view and a view model — the interface-language picker went from 4 rows to
26 — and `Strings.cs` *does* gain one accessor, for the key phase 9 added. Neither is
per-language work. The picker change would have been needed for the 4th language as much
as the 25th, and the new accessor is an ordinary new string.

**Published size grows 1.28 MB** — 24 satellites at ~55 KB each, produced by the SDK with
no build change. Nothing in `Directory.Build.targets` filters them, though
`RemoveForeignRuntimeAssetsFromPublish` matches on immediate parent directory name and
satellites live in `<culture>/`: no collision exists today, but the two namespaces are one
unlucky RID apart.

**Layout review is 600 images** — 24 pages (19 settings pages plus five warning states)
across 25 languages. `LocalizedLayoutReviewTests` renders them behind
`PARLOTYPE_LAYOUT_REVIEW=1`, gated because rendering many view trees leaves
them bound to the `Localizer` singleton and destabilizes the culture-bound tests. That gate
is load-bearing: Phase 9 began by writing a second, ungated copy of the harness — by someone
who had not found the first — which broke eight tests in exactly the documented way. The
harness now reads its language list from the registry rather than a hardcoded `{ en, ru, es }`.

**Translation quality is the standing risk, and it is not hedged by tooling.** Every
mechanical property is enforced — 9,336 strings at zero placeholder mismatches, zero
named-token damage, zero left in English. Whether the Maltese reads well to a Maltese
speaker is not something this project can currently answer.
