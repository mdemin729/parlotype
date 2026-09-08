---
title: "Session: 2026-08-01 — Prompt settings help panel, punctuation instruction, {text_lang} leak fix"
type: session
status: complete
tags: [gemma4, prompts, settings-ui, adr-037]
created: 2026-08-01
summary: "Explained the Gemma 4 prompt model in the Prompts settings page (collapsible 'How prompts work' panel), added a 'Use punctuation.' instruction to every built-in body, and stopped {text_lang} leaking as a raw token on every BuildPromptTextAsync branch (including the translating custom-prompt path, caught in code review)."
---

# Session: 2026-08-01

## Active Focus
- `src/Parlotype.Desktop/Views/Settings/PromptSettingsView.axaml` — new collapsible
  `Expander` "How prompts work" (placeholders / translation trigger / built-in vs.
  custom bodies) + expanded two-line editor hint replacing the one-line `Tip:`.
- `src/Parlotype.Desktop/ViewModels/Settings/PromptSettingsViewModel.cs` — added
  `IsHelpExpanded` (session-only, collapsed by default, VM-side so headless tests
  can drive it).
- `src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs` — `BuildPromptTextAsync`
  now passes both arguments to `PromptTemplate.Substitute` on **every** branch:
  `targetName` on both translation paths (built-in body + custom-prompt path), `speechName`
  twice on the two non-translating paths.
- `src/Parlotype.Platform/Speech/JsonPromptTemplateRegistry.cs` — all three built-in
  bodies (and the new-prompt template in `PromptSettingsViewModel.NewPrompt`) gained
  `Use punctuation.`.
- Tests: `LlamaCppPromptBuildingTests` (+3), `JsonPromptTemplateRegistryTests` (+1),
  `PromptSettingsViewModelTests` (+1), `PromptSettingsScreenshotTests` (+1 scenario,
  stale `Tip:` description fixed).
- Docs: `README.md`, `docs/architecture/llamacpp-subsystem.md`, `docs/decisions/037-*.md`
  (amendment section), vault.

## Decisions Made
- The doubled `{speech_lang}` in the built-in default's transcription body is
  **correct, not a typo** — that body only runs when the output language equals the
  spoken one ("speech in X into X text"). The confusion it caused is a documentation
  problem, so the fix is explanatory copy, not a prompt change.
- `{text_lang}` is substituted on every path: target name while translating, speech
  language otherwise. `PromptTemplate.Substitute`'s null-argument contract ("leave the
  token alone") is unchanged — the recognizer supplies both arguments instead. Keeps
  the helper honest for any future caller that genuinely wants a partial render.
  **Caught in review:** the first pass only fixed the two non-translating branches and
  left the custom-prompt *translation* branch leaking the token — the one case where a
  prompt author is most likely to write `{text_lang}`. Lesson: `BuildPromptTextAsync`
  has four exit points, and a token-substitution change has to be checked against all
  of them, not just the one the reported symptom came from.
- Help panel state lives on the ViewModel, not in code-behind, purely so the
  screenshot/headless tests can expand it. Not persisted — a settings key for a help
  toggle would be noise.
- No new ADR: no Core type, no DI entry, no dependency, no OS divergence. Recorded as
  a dated **Amendment** section inside ADR-037 instead.
- All three bodies of the built-in default (and the pre-filled text for a new custom
  prompt) now carry `Use punctuation.` — user-observed quality win. Deliberate
  divergence from Google's prescribed template; pinned by
  `JsonPromptTemplateRegistryTests.BuiltInDefault_EveryBody_InstructsPunctuation`.
  Existing *custom* prompts are not rewritten — that is the user's text.

## Facts Learned
- Two Avalonia AXAML inline-text gotchas hit while writing the help copy — promoted to
  [[../knowledge/avalonia-axaml-text-gotchas]]: brace-prefixed attribute values need
  `{}` escaping, and adjacent `<Run>`s on separate lines get an implicit space (so a
  `Run` starting with punctuation renders detached).
- `Expander` had no prior usage anywhere in the Desktop project; the Fluent default
  renders fine inside the settings `StackPanel` with `HorizontalAlignment="Stretch"`.
- `reports/prompt-settings-scenarios.html` (written by the screenshot-test fixture on
  dispose) embeds base64 PNGs — extracting one and viewing it is a cheap way to verify
  UI copy actually renders as intended without launching the app.

## Open Blockers
- None.

## Documentation Status
- ADR: done — amendment section in `docs/decisions/037-gemma4-source-target-prompts.md`
  (no new ADR required).
- Vault (services/architecture): done — `memory/services/platform.md`,
  `memory/services/desktop.md`, `memory/decisions/_index.md`.
- Knowledge (non-derivable facts): done — `memory/knowledge/avalonia-axaml-text-gotchas.md`
  + index row.

## Next Action
Nothing pending. If the Prompts page grows further, consider surfacing the built-in
default's translation and auto-detect bodies read-only in the UI — today they are only
visible in code, and duplicating the built-in silently drops them.
