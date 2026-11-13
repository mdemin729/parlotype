# Gemma 4 engine review and the case for speech cleanup

_Written 2026-11-13. A read-only review of the experimental Gemma 4 speech engine: code,
documentation and the benchmark results already in the repository. The app was not run and
no new measurement was taken._

The question was whether to remove the Gemma 4 engine or to keep developing it. The argument
for keeping it was that a local LLM could post-process recognized text: apply a style, fix
terms from a dictionary, and so on.

## Verdict

- **Do not develop Gemma 4 as a speech engine.** It is less accurate and far heavier than
  Parakeet and Whisper, and its setup assumes a technical user.
- **Keep the `llama-server` subsystem.** The catalog, installer, registry and settings are
  already workload-agnostic ([ADR-027](../../decisions/027-llamaserver-namespace-rescope.md)).
  Build text post-processing on it as a step that runs after any speech engine.
- **Once post-processing covers translation, remove Gemma 4 from the engine list.** Its one
  unique capability, translation into any language, moves into post-processing and then
  works with Parakeet too.

## What is wrong with the engine today

| Finding | Evidence |
|---|---|
| Lower accuracy | LibriSpeech test-other, 50 samples: Gemma 4 13.2–14.4 % WER against Whisper 10.2–13.1 % ([comparison](../2026-05-23-librispeech-cuda-comparison/comparison.md)). [ADR-025](../../decisions/025-gemma4-llamacpp-desktop.md) quotes about 41 % WER on noisy AMI audio against 16 % for Whisper large-v3. |
| Heavy | 5.5–15 GiB download against 670 MB for Parakeet. In practice it needs a GPU (`-ngl 99`); CPU-only speed was never measured. |
| Languages unmeasured | The README promises the full language catalog; every Gemma benchmark config is English. |
| Three extra settings pages | Selecting the engine adds Gemma 4 model (five variants), Prompts, and llama.cpp server (a list of GitHub builds by backend). Nothing is installed automatically. |
| Silent failure | With no server or model installed, the hotkey does nothing: the recognizer's exception lands in the generic `catch` in `TranscribeViewModel.StartRecordingCoreAsync` and the status returns to Ready with no dialog. Cloud engines have a dedicated dialog for the same situation. |
| Stale copy | The engine card says "English only", "Vulkan", "~10 GB will be downloaded on first use". The engine translates into any language, runs any backend, the default model is 5.9 GiB and nothing downloads on first use. |
| Possible text loss | The model card caps a clip at 30 s. `SpeechEngineLimits` hands Gemma 4 up to 60 s as an "unmeasured" engine. Not verified. |
| Unstable output | Verbatim transcription is sampled at `temperature = 1.0`, `top_k = 64`, with no token limit and thinking not disabled. The E2B Q8_0 variant crashed `llama-server` with HTTP 500 ([benchmark caveats](../../knowledge/benchmark-memory-and-engine-caveats.md)). |

## Recommendation

1. Do not fix the engine's defects; they leave with it. The "(Experimental)" label is enough
   until then.
2. Add post-processing as a separate step after recognition, behind a Core contract, so it
   works with every engine. A `/v1/chat/completions` client covers a managed `llama-server`,
   a user's own LM Studio or Ollama, and a bring-your-own-key cloud endpoint.
3. Make it opt-in and never let it block dictation: on an error or a timeout the raw text
   is pasted (OpenWhispr does the same). Handy keeps post-processing on its own hotkey so
   that plain dictation stays instant.
4. Choose the model by measurement. Candidates:
   - **S1-mini** by Superwhisper: a 0.6B fine-tune of Qwen3-0.6B built only for cleaning ASR
     output, 462 MiB at Q4_K_M, runs on a laptop CPU, 94.8 % token accuracy on its own
     held-out set. English only. Apache 2.0 plus a clause that the name is kept. It is
     steered by a fixed control line, not by a free prompt.
   - **Gemma 4 E2B / E4B** as a text model: multilingual, which matters for an app that
     ships in 25 languages, but several GiB and slow without a GPU.
   - General models of 8B and up are what Handy recommends for free-form prompts; it warns
     that models under 3B often ignore instructions.

The main risks are latency on machines with no GPU (not measured) and a small general model
changing the meaning or answering a dictated question instead of cleaning it. A model
trained for the single task avoids the second.

## Most requested uses of a local model in a dictation app

Ranked by how often competing products lead with them:

1. **Speech cleanup**: filler words, self-corrections ("Friday, no wait, Thursday"),
   punctuation and capitalization, numbers, dates, e-mail addresses.
2. **Dictionary of terms and names.** Deterministic replacements first: they need no model
   and add no latency. The same list can later be given to a model for fuzzy matches.
3. **Styles**: e-mail, messenger, list, note; later chosen by the focused application.
4. **Translation into any language**, with any speech engine.
5. **Spoken formatting commands**: "new line", "as a list", "in quotes".
6. **A developer mode**: dictating prompts for AI assistants while keeping identifiers and
   file names intact.
7. Later: **editing selected text by voice** ("shorten this", "translate this").

## Sources

- [Handy: Post-Processing](https://handy.computer/docs/post-processing)
- [superwhisper/s1-mini-GGUF model card](https://huggingface.co/superwhisper/s1-mini-GGUF)
- [S1-mini guide, Better Stack](https://betterstack.com/community/guides/ai/s1-mini/)
- [OpenWhispr: cleanup](https://docs.openwhispr.com/help/dictation/cleanup)
- [Superwhisper vs Wispr Flow](https://superwhisper.com/vs/wispr-flow) and
  [Wispr Flow vs Superwhisper](https://wisprflow.ai/post/wispr-flow-vs-superwhisper): vendor
  pages, read with that in mind
- [Best open-source dictation apps for Mac, AssemblyAI](https://www.assemblyai.com/blog/best-open-source-dictation-apps-mac)
