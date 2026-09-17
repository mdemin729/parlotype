---
title: Twenty-five UI languages — match the interface set to Parakeet's speech set
status: planned
created: 2026-09-17
started:
completed:
---

# Twenty-five UI languages

## Problem

Parlotype's default engine, Parakeet TDT v3, transcribes 25 European languages and
auto-detects between them with no language UI at all ([ADR-041](../../docs/decisions/041-parakeet-v3-sherpa-onnx.md) /
[ADR-042](../../docs/decisions/042-parakeet-default-language-ux.md)). A Czech user can
dictate Czech on day one — into an interface that only speaks English, Russian or
Spanish.

The localization foundation from [ADR-064](../../docs/decisions/064-ui-localization-foundation.md)
was built for exactly this and says so: *"Adding a language is one row in
`SupportedUiLanguages.All` plus one resx file. Nothing in markup, view models or the
settings UI changes. If that turns out to be false, the abstraction leaked and that is
the bug to fix."* This plan is that claim's first real test, 22 times over.

**Scope:** add the 22 interface languages Parakeet supports that Parlotype's UI does
not — Bulgarian, Croatian, Czech, Danish, Dutch, Estonian, Finnish, French, German,
Greek, Hungarian, Italian, Latvian, Lithuanian, Maltese, Polish, Portuguese, Romanian,
Slovak, Slovenian, Swedish, Ukrainian — bringing the interface to 25 languages, English,
Russian and Spanish included.

**A note on the premise.** Matching the UI set to *Parakeet's* set is a coherent product
rule and the one this plan follows, but it is a choice, not a derivation: Whisper covers
~99 languages, so a Japanese or Mandarin Whisper user still gets an English interface.
That is acceptable — Parakeet is the default and the 25 are where the users are — but the
rule should be written down rather than assumed, which is part of what the ADR is for.

## The real cost, stated up front

392 keys × 22 languages = **8,624 new translated strings**, and from then on every future
UI string costs 24 translations instead of 2. The `Stop` hook and
`LocalizationParityTests` will not let a session ship without them, which is correct and
also means the per-string cost is now permanent and non-negotiable.

So the deliverable is not 22 resx files. It is **a pipeline that makes the 23rd language
and the 393rd key cheap**, with 22 languages as its first output. If this plan ends with
22 hand-assembled resx files and no tooling, it has failed even with a green build.

## Approach

### 1. Translate through a JSON brief, never by editing resx directly

Each `Strings.<culture>.resx` is ~80 KB of XML with escaping rules, a schema header, and
`xml:space="preserve"` on every node. Asking an agent to emit that by hand 22 times is
~1.8 MB of XML generation whose failure mode is a malformed file that breaks the build in
a way the parity checks describe badly.

Instead, two new scripts bracket the translation step:

```
Strings.resx ──export-translation-brief.ps1──▶ <culture>.brief.json
                                                      │
                                          (Sonnet 5 subagent translates)
                                                      ▼
Strings.<culture>.resx ◀──import-translations.ps1── <culture>.json
```

The brief carries, per key: the key name, the English value, its `<comment>` (140 keys
have one), and the placeholder indices it must preserve. The subagent returns a flat
`{key: translation}` map and nothing else. The importer owns every byte of XML.

This is the load-bearing decision of the plan. It cuts per-language agent cost to roughly
a third, removes XML corruption as a failure mode entirely, guarantees all 22 files are
byte-shaped identically for review, and makes re-translating a changed key a one-key
brief instead of a full pass.

### 2. Delegate one language per Sonnet 5 subagent, in waves

Each subagent gets the brief, the shared style rules, and the do-not-translate register;
it returns the JSON map plus a short per-language glossary of the ~25 core product terms
it committed to. Waves of 4–5 group related languages (`cs`/`sk`, `lt`/`lv`, `hr`/`sl`)
so a wave's terminology decisions are made against each other.

The subagent never touches the repo's resx, the registry, or the build.

### 3. Registry row and resx land in the same commit, always

`SupportedUiLanguages.All` is what `check-localization.ps1` scrapes to decide which files
must exist. A row without its resx is a red build by design. So there is no "register all
25 first" step — each wave adds its own rows.

### 4. Keep Russian as the canary locale

`LocalizationTests` asserts that Core-originated copy (reserved shortcuts, hotkey conflict
reasons) renders as Cyrillic under `ru`. That catches an enum member added without a resx
key — an omission no parity check can see, because it is missing from *every* language at
once and so the languages still agree. Generalizing that assertion 22 ways buys nothing;
one canary is enough and Russian already is it. This stays as-is, deliberately.

## Workplan

Phases are sequential; the five translation waves are interchangeable in order. Detail in
[implementation-plan.md](implementation-plan.md); verified facts about culture codes,
endonyms, packaging and expansion in [research.md](research.md).

- [ ] **Phase 0 — Pipeline.** `export-translation-brief.ps1` + `import-translations.ps1`.
      Proven by round-tripping the existing `ru` and `es` files: export, re-import,
      `git diff` must be empty.
- [ ] **Phase 1 — Guardrails for 25.** Per-language `deliberatelyIdentical`; a fact that
      every registry culture is a real .NET neutral culture with a distinct endonym; an
      advisory expansion report in `check-localization.ps1`.
- [ ] **Phase 2 — Picker UX.** The interface-language page goes from 4 rows to 26.
- [ ] **Phase 3 — Shared brief.** Do-not-translate register + the 25-term core glossary
      the per-language glossaries are written against.
- [ ] **Phase 4 — Wave A:** `de` `fr` `it` `pl` `uk`
- [ ] **Phase 5 — Wave B:** `pt` `nl` `sv` `da` `fi`
- [ ] **Phase 6 — Wave C:** `cs` `sk` `ro` `el` `hu`
- [ ] **Phase 7 — Wave D:** `bg` `hr` `sl` `lt` `lv`
- [ ] **Phase 8 — Wave E:** `et` `mt`
- [ ] **Phase 9 — Verification.** Expansion report → screenshot audit in the worst-
      expanding locales; manual run of the real app in 3 spot-checked languages;
      publish-size delta measured.
- [ ] **Phase 10 — Documentation.** ADR-069; `localization` skill updated to make the
      brief/import scripts the standard path; memory vault; CHANGELOG.

## Open decisions

These are recorded rather than blocking — each has a recommendation the plan proceeds on
unless overridden.

1. **Portuguese variant.** Recommend neutral `pt`. `pt-BR` is the larger market but
   Parakeet's list is European and .NET's satellite fallback serves a `pt-BR` user from a
   `pt` satellite. Splitting later is additive. *(Confirm against research.md §2.)*
2. **Translation provenance.** Recommend shipping all 22 as first-class, with no
   "machine translated" badge in the UI — a disclaimer invites users to distrust copy
   they cannot compare against anything. Native-speaker review is tracked per language
   as follow-up issues, not as a gate on shipping.
3. **Wave granularity.** Recommend one PR per wave (5 PRs). One PR for all 22 is
   ~1.8 MB of diff nobody can review; one per language is 22 PRs of ceremony.
4. **Maltese.** The smallest language in the set and the one where LLM translation
   quality is weakest and hardest to verify. Recommend shipping it — an imperfect
   Maltese interface beats an English one for a Maltese speaker — but flagging it in
   the ADR as the first candidate for human review.

## Definition of Done

Beyond the repo-wide bar in `CLAUDE.md`:

- `pwsh scripts/check-localization.ps1` passes with 24 satellite files.
- `pwsh scripts/gen-strings.ps1` produces no diff — the accessor is culture-agnostic and
  must not change at all. If it does, something is wrong.
- `dotnet build Parlotype.slnx` clean, `dotnet test` green.
- The app launches and switches live into at least three of the new languages, one of
  them Greek (the only new script).
- Published size delta measured and recorded in the ADR.
