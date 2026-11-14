---
status: accepted
date: 2026-11-14
amends: [073]
---

# 075. Speech cleanup follows the dictation language

## Context

Speech cleanup ([ADR-073](073-speech-cleanup-s1-mini.md)) runs an English-only model. ADR-073
left it to the user to switch cleanup off before dictating in another language, and backed
that with one check on the text: skip it when more than 3% of its letters are non-ASCII.

That check ignores everything the app already knows. The pipeline read only the switch: not
the engine, not the source language, not whether the text was being translated. The results:

- Whisper set to Russian still loaded the cleanup model on every recording, to then skip
  every utterance.
- An Italian, Spanish, Dutch or Danish sentence often carries no accented letter at all. It
  passed the check and came back damaged. ADR-073 names this gap.
- A short English sentence around a name like "Müller" failed the check and lost its cleanup.
- When cleanup was skipped the user was not told. The switch said "on" and the text arrived
  raw; the reason was a debug line in the log.

## Decision

**The output language of a dictation decides whether cleanup runs. Where the settings give
it, they decide; where only the text can tell, the text is examined more carefully; and the
user is shown which of the two happened.**

### One rule for the output language

`CleanupLanguagePolicy.Resolve` (Core) answers "what language will the pasted text be in?"
with `CleanupLanguage`: English, another language (with its code), or unknown. It mirrors
what each engine does with the settings, not what the settings say:

| Engine | Output language |
|---|---|
| Parakeet, OpenAI-compatible, xAI Grok | Unknown. They always auto-detect and ignore the stored source and target, which survive an engine switch on purpose ([ADR-042](042-parakeet-default-language-ux.md)). |
| Whisper, an English-only model | English, whatever is stored. |
| Whisper, translating | English when translation is on, the target is English and the model can translate; the same condition that sets the translate flag. |
| Whisper, otherwise | The source language; unknown for auto-detect. The keyboard sentinel resolves to the current layout. |
| Gemma 4 | The target when the translation prompt is used, otherwise the source. |

`WhisperModelInfo.IsEnglishOnly` is new: `SupportsTranslation` is false both for the
English-only models and for Large v3 Turbo, and only the first group always writes English.

The audio pipeline and the desktop UI call the same function, so the UI cannot show
"paused" while the pipeline cleans, or the reverse.

### What the pipeline does with it

- **Another language:** cleanup is paused for the session. The model is not loaded and not
  asked. The stored switch is untouched: the user's choice stays on and takes effect again
  when the language allows it.
- **English:** the text goes to the model and is not second-guessed. The setting is trusted
  over a letter count.
- **Unknown:** decided per utterance. If the recognizer reports a language
  (`TranscriptionResult.DetectedLanguage`, which Whisper fills and nothing read),
  `CleanupLanguagePolicy.Refine` takes it. Otherwise the text is examined.

### Examining the text

`SpeechCleanupGate.IsLikelyEnglish` keeps the script test and adds function words
(`EnglishTextEvidence`): it counts the words that speak for English and those that speak for
another Latin-script language, across the languages Parakeet recognizes plus Norwegian,
Turkish and Indonesian. Text is rejected when the foreign words weigh at least as much as
the English ones. With no evidence either way it passes.

That last rule is deliberate. Parakeet is the default engine and reports no language, so an
English speaker's short or telegraphic utterance ("meeting moved to Friday at three") has to
keep its cleanup. Requiring positive evidence of English would have taken it away. The
foreign list is lopsided for the same reason: a word that is also ordinary English ("met",
"pour", "care"), a common name ("Sam", "Ali") or something said when dictating about
software ("int", "nav") is left out even where it is frequent in its own language, and
tokens written in capitals ("AI", "EU", "OS") are ignored.

The non-ASCII limit is now 10%, but above 3% the words have to vouch for the text: some
English evidence and no foreign evidence.

This is a heuristic, not language identification. A language-identification library was
not added: the lists are a few hundred words, need no dependency and no model file, and the
failure they leave (below) is bounded.

### The outcome travels with the result

`TranscriptionResult.CleanupOutcome` (`SpeechCleanupOutcome`: not run, cleaned, skipped as
not English, skipped as too long, failed) is set by the pipeline for every utterance.
`SpeechCleanupGate.Evaluate` returns the skip reason; `ShouldClean` remains as the
language-unknown shorthand.

### One state behind two surfaces

`SpeechCleanupStateViewModel` (Desktop, singleton) holds the switch, readiness, the output
language (computed from `LanguageRelationshipViewModel`) and the last outcome, and derives
one `CleanupIndicatorState`. The Speech cleanup page and the Transcribe widget both read it,
as the Language page and the widget's strip share `LanguageRelationshipViewModel`.

- **Settings page.** The line under the switch says cleanup is English-only, or, while
  paused, that it is paused and for which language, in amber.
- **Transcribe widget.** A sparkle icon between the grip and the close button is indicator
  and switch in one:

  | State | Look | Click |
  |---|---|---|
  | Cleanup never set up | not shown | |
  | Set up, switched off | dimmed | turns it on |
  | On | accent | turns it off |
  | On, paused for the language | amber | turns it off |
  | On, last dictation pasted as spoken | amber until the next recording | turns it off |
  | On, model or server missing | amber | opens the Speech cleanup page |

  It is hidden until the model and a server are installed, so the widget is unchanged for
  anyone who does not use the feature, and a click never has to start a download: turning
  cleanup on from the widget is refused unless everything is on disk. The downloads stay on
  the settings page, which asks first (ADR-073). The widget keeps both of its heights.

  A fourth chip on the language strip was rejected because Parakeet has no strip
  ([ADR-042](042-parakeet-default-language-ux.md)); a status-tooltip-only signal because
  nobody would find it and it cannot be clicked.

A click stops a recording in flight, as every other settings change does: the pipeline
snapshots its settings when a recording starts.

## Consequences

**Easier**

- Dictating in another language with Whisper or Gemma 4 no longer needs the switch touched,
  and no longer keeps half a gigabyte of model loaded for nothing.
- Translating into English, or using an English-only Whisper model, cleans text the letter
  count used to reject.
- The user can see whether the next dictation will be cleaned and why the last one was not,
  and can switch cleanup without opening Settings.
- A second cleanup model that covers more languages changes one function: what
  `CleanupLanguagePolicy` treats as supported.

**Harder / trade-offs**

- For Parakeet and the cloud engines the accent icon means "will try, if the text is
  English", not a promise. The verdict arrives after the dictation, as the amber state.
- The text test still errs towards English. A short reply in another Latin-script language
  with no diacritics and no listed word ("va bene") passes and may come back damaged.
  Sentences are what the word lists catch.
- The word lists are hand-written and were checked against test sentences, not measured on
  a corpus. A language outside them written without diacritics is not caught.
- An English sentence built mostly from words on the foreign list would lose its cleanup.
  The exclusions above are there to make that rare; it costs polish, not the dictation.
- The widget's header now holds up to four things: Cloud badge, grip, cleanup icon, close.
- The pipeline reads `SettingsKeys.SpeechEngine` a second time at session start, next to
  the utterance-ceiling read.
- Nine new interface strings in 25 languages; `Settings_SpeechCleanup_EnglishOnlyNote`
  ("turn it off when you dictate in another language") is gone, because it is no longer
  what the user should do.

## Verification

- `dotnet build Parlotype.slnx`: zero warnings. `dotnet test`: all projects pass.
- New tests: `CleanupLanguagePolicyTests` (every engine's output language, per-utterance
  refinement, the gate with a known and an unknown language, English and thirteen other
  Latin-script languages written without diacritics), `AudioPipelineSpeechCleanupTests`
  (paused sessions never load the model, translation and English-only models clean,
  Parakeet ignores left-over language settings, the outcome on every path),
  `SpeechCleanupStateViewModelTests` (each indicator state, the widget click, the page and
  the widget following each other).
- Screenshot scenarios for the widget (on, off, paused, last dictation skipped next to the
  Cloud badge) and for the paused settings page were rendered and read.
- Not exercised in a running app with a real microphone and the real model.
