---
title: "S1-mini quirks (thinking flag, greedy decoding, slots, looping)"
area: "speech-engines"
summary: "S1-mini needs its exact system prompt and control line, thinking off through the template, temperature 0 on every request and -np 1; it loops on repeated input and is English-only."
verified: 2026-11-13
---

# S1-mini quirks

Facts about running `superwhisper/s1-mini-GGUF` through `llama-server` for speech cleanup
([ADR-073](../decisions/073-speech-cleanup-s1-mini.md)). Observed on llama.cpp b10330 with
`s1-mini-q4_k_m.gguf`; the numbers behind them are in the
[measurements note](../research/2026-11-13-gemma4-engine-review/s1-mini-measurements.md).

## The input format is part of the model

The model card states that a reworded system prompt, a missing control line, or a control
value outside the trained sets makes the model hallucinate or return garbled text. The
strings in `S1MiniPrompt` are copied verbatim and a test pins the system prompt. Trained
values: styling `casual | semi-casual | semi-formal | formal`, structure `prose | lists`,
context `general | email`. It is not a chat model and follows no other instruction.

## Thinking must be off, and only one way of turning it off works

The embedded chat template is Qwen3's, which thinks by default. S1-mini was trained with an
empty think block and returns nothing usable otherwise.

- Use `--jinja --chat-template-kwargs {"enable_thinking":false}` (the app also sends
  `chat_template_kwargs` in each request).
- Do **not** use `--reasoning-budget 0`. The model card reports that it silences the think
  block differently and degrades the output: fillers survive.
- b10330 logs `Setting 'enable_thinking' via --chat-template-kwargs is deprecated. Use
  --reasoning on / --reasoning off instead.` It still works. `--reasoning off` was **not**
  tested against the published examples; if a later build drops the old flag, compare the
  outputs before switching, and remember that builds older than the `--reasoning` flag
  exit on an unknown argument.

## Send `temperature: 0` yourself

The GGUF metadata carries `general.sampling.temp = 0.6` (inherited from Qwen3-0.6B) and
`llama-server` defaults to 0.8. The model is trained for greedy decoding. Neither default
gives that, so the app passes `--temp 0` and `temperature: 0` in the request body.

## `-np 1`, or the context is split

`llama-server` divides `-c` between its slots, and the default slot count is "auto", which
is more than one. With `-c 4096 -np 1`, `/props` reports `n_ctx 4096` and one slot. Without
`-np 1` a long dictation silently gets a fraction of the window.

## It loops on repeated input

One sentence repeated six times came back as the cleaned sentence repeated until
`max_tokens`. The request caps `max_tokens` at half the input's character count plus 64, and
`SpeechCleanupGate.IsPlausibleResult` rejects any result that outgrew its input, so the raw
text is pasted. A cleanup result longer than its input is a symptom, not a style.

## An empty result is a success

Filler-only input ("um uh hmm") correctly cleans to an empty string. Treat empty as valid
and paste nothing; do not retry or fall back to the raw text.

## Lists are deliberately conservative

`[Structure: lists]` only permits a list. The model wants a clear enumeration of at least
three items; "sunscreen and then also a first aid kit and chargers" stayed prose in our run
although the model card shows it as bullets.

## English only, and it does not refuse other languages

German typed without umlauts came back with a sentence break in the wrong place. The model
does not detect that the input is outside its training; the caller has to keep such text
away from it (`SpeechCleanupGate.IsLikelyEnglish`, a script test that Latin-script languages
with few diacritics get past).

## Licence

Apache 2.0, inherited from Qwen3-0.6B, plus one term: the model keeps its name, "S1-mini" by
"Superwhisper", with that capitalization, wherever it is used. `SpeechCleanupModelInfo.S1Mini.DisplayName`
is that name and is never translated. Parlotype downloads the weights from HuggingFace at
the user's request and does not redistribute them.

The Hub sidebar says 0.8B parameters; the model is 0.6B. Qwen3-0.6B ships a materialized
copy of the tied embedding, which is counted twice.
