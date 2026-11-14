# S1-mini through llama-server: measurements

_Taken 2026-11-13 for [ADR-073](../../decisions/073-speech-cleanup-s1-mini.md)._

**Model:** `superwhisper/s1-mini-GGUF`, `s1-mini-q4_k_m.gguf` (484,219,808 bytes).
**Server:** llama.cpp `b10330-win-cuda-13.3-x64`, started with the flags the app uses:
`-c 4096 -np 1 --jinja --chat-template-kwargs {"enable_thinking":false} --temp 0 --no-webui`.
**Machine:** the development desktop, a 16-core CPU with an NVIDIA GPU, Windows 11.
One run per case, model already loaded; times are wall-clock for the HTTP request.
A slow or battery-powered laptop CPU was not measured.

## Startup

| | Time |
|---|---:|
| Download from HuggingFace with SHA-256 verification, on this connection | 13.2 s |
| Server start and model load, GPU offload (through `LlamaServerSpeechCleanupService`) | 0.9 s |
| Server start and model load, `-ngl 0` | 0.8 s |

## Cleanup latency

| Case | Input chars | Tokens in / out | GPU | CPU only (`-ngl 0`) |
|---|---:|---:|---:|---:|
| Model card example (one sentence, self-correction) | 80 | 97 / 11 | 81 ms | 196 ms |
| List of four items | 40 | 87 / 17 | n/a | 227 ms |
| Three action items | 245 | 128 / 50 | n/a | 615 ms |
| A realistic long dictation, six sentences | 861 | 252 / 199 | n/a | 2395 ms |
| Filler only ("um uh hmm"), returns empty | 9 | n/a | 19 ms | n/a |

On the CPU that is about 80 generated tokens per second. Output length drives the time; the
fixed system prompt and control line cost about 85 input tokens on every request.

## Output

Run through the real service with the default options unless noted. The results match the
model card.

| Options | Input | Output |
|---|---|---|
| default | `so um i need to like send the the report by uh friday no wait make that thursday` | `So I need to send the report by Thursday.` |
| casual | `hmm im gonna be late theres a cute dog outside i cant just walk past him` | `hmm im gonna be late. theres a cute dog outside. i cant just walk past him` |
| semi-casual | same | `hmm, I'm gonna be late. there's a cute dog outside. I can't just walk past him` |
| formal | same | `Hmm, I am going to be late. There is a cute dog outside. I cannot just walk past him.` |
| default | `the meeting is on march third at two thirty pm and it costs twenty five dollars email me at john dot smith at example dot com` | `The meeting is on March 3 at 2:30pm, and it costs $25. Email me at john.smith@example.com.` |
| default | `um what is the capital of france` | `What is the capital of France?` |
| default | `So, um, I think we should, uh, we should probably move the release to, to next Tuesday. No, wait, Wednesday.` | `So I think we should probably move the release to next Wednesday.` |
| lists | `i need to buy milk eggs bread and coffee` | `I need to buy:` followed by four bullets |
| e-mail | `hey sarah just wanted to follow up on the proposal can you send the numbers by end of week thanks john` | greeting line, body and sign-off block separated by blank lines |

The question case matters: a general chat model tends to answer a dictated question. S1-mini
punctuates it and stops.

## Where it goes wrong

- **Lists are conservative.** With `[Structure: lists]`, "we need to pack sunscreen and then
  also a first aid kit and chargers for everything" stayed prose, although the model card
  shows it as a list. Explicit enumerations ("first… second… third…", "milk eggs bread and
  coffee") became lists.
- **Repetition makes it loop.** One sentence repeated six times (875 characters) came back
  as the cleaned sentence repeated until `max_tokens` cut it off at 2312 characters.
  `SpeechCleanupGate.IsPlausibleResult` rejects this, so the raw text is pasted. A realistic
  861-character dictation without repetition cleaned correctly.
- **Other Latin-script languages pass the script test and come back damaged.** German
  without umlauts, `ich habe heute keine zeit koennen wir morgen sprechen`, returned
  `Ich habe heute keine Zeit koennen. Wir morgen sprechen.`, with the sentence break in the
  wrong place.

## Server observations

- `/props` reported `n_ctx 4096` and one slot, as intended by `-c 4096 -np 1`.
- At default verbosity the server log did not contain any request text.
- b10330 prints `Setting 'enable_thinking' via --chat-template-kwargs is deprecated. Use
  --reasoning on / --reasoning off instead.` The flag still works. Whether `--reasoning off`
  gives the same output was not tested.
