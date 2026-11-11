# Local LLM Runtimes for Parlotype: Gemma 3n / Gemma 4 ASR + Post-Processing on Windows

**Bottom line: For a Windows-only C#/.NET voice-to-text app that wants ONE local runtime to handle both Gemma's audio ASR and downstream text post-processing, the clear top recommendation is `llama-server` from llama.cpp (CUDA or Vulkan build), with **Lemonade** as the runner-up — particularly compelling on AMD Ryzen AI / Radeon hardware and as a path to fall back to Whisper for ASR while still using Gemma for post-processing.**

## TL;DR

- **Gemma audio is real but young.** Gemma 3n (June 2025) introduced a USM-based audio encoder; **Gemma 4** (released by Google DeepMind on **April 2, 2026**) carries a redesigned 50%-smaller conformer audio encoder on the **E2B** and **E4B** edge variants. Audio is supported on E2B/E4B only; the 26B-A4B and 31B variants are vision+text only. The model card limits clips to **30 seconds, mono, 16 kHz**, and Gemma 4 costs **25 tokens per second of audio** (vs 6.25 for Gemma 3n).
- **Only two runtimes today actually expose Gemma audio over a programmable local API on Windows: llama.cpp's `llama-server` (input_audio support landed in April 2026 after PR #21421 added the conformer encoder and a follow-up patch closed the HTTP routing gap raised in issue #21868) and vLLM (which is Linux/WSL-only on Windows in practice). Ollama's `gemma4` library entry shows only "Text, Image input" tags as of May 2026, audio is silently ignored on `/api/chat`, and a GGML-assert crash bug (issue #15333) is open. LM Studio's Gemma 3n / Gemma 4 model pages explicitly note GGUFs are currently text-only or text+image; audio is not exposed in the LM Studio runner. SGLang has a tracked bug (#8361) where Gemma 3n audio fails on its OpenAI-compatible endpoint.**
- **Practically, your single-runtime ambition is best served by `llama-server` exposing OpenAI-compatible `/v1/chat/completions` with `input_audio` blocks for ASR and the same endpoint for post-processing — but you should keep an architectural escape hatch: WER for Gemma 4 E4B on noisy speech is materially worse than dedicated Whisper (Open ASR Leaderboard runs show E4B at ~4.17% WER on LibriSpeech-clean, but ~41% WER on AMI noisy meeting audio; E2B catastrophically misbehaves at ~202% WER on AMI). For privacy-sensitive transcription of arbitrary user audio, plan to ship Whisper.cpp as the ASR engine and use Gemma only for post-processing — Lemonade is unique in that it bundles both behind one OpenAI-compatible localhost endpoint.**

## Key Findings

### 1. Gemma model naming as of May 2026

- **Gemma 3n** (announced at Google I/O May 2025, full release June 26, 2025) — first Gemma family with a USM-based audio encoder; E2B and E4B "effective parameter" variants only; 32K context. Audio limited to 30 seconds, 6.25 tokens/second.
- **Gemma 4** (released April 2, 2026 by Google DeepMind under Apache 2.0) — four sizes: **E2B**, **E4B**, **26B-A4B** (Mixture-of-Experts with 4B active), and **31B dense**. **Audio is featured natively only on E2B and E4B.** Smaller, redesigned conformer audio encoder (~50% smaller than Gemma 3n's, 40 ms frame duration). 128K context on E2B/E4B; 256K on the larger models. 25 tokens/second of audio. Standard system/user/assistant chat roles plus an explicit `<|think|>` thinking-mode token.
- For Parlotype, the relevant SKUs are **Gemma 4 E2B** (~5 GB at Q4) and **Gemma 4 E4B** (~9.6 GB at Q4) — both fit comfortably on a 16 GB consumer laptop and run with CUDA, Vulkan or CPU.

### 2. Audio capability matrix across the 14 candidates

| Runtime | Windows | Gemma audio? | OpenAI-compatible API | Native API | CLI for ASR | Hardware (Win) | Verdict for Parlotype |
|---|---|---|---|---|---|---|---|
| **llama.cpp (`llama-server`)** | Yes (prebuilt CUDA / Vulkan / ROCm Win x64 zips on every release) | **Yes** (`input_audio` block in `/v1/chat/completions` after April 2026 fix; mmproj must be loaded) | Yes (`/v1/chat/completions`, `/v1/completions`, `/v1/embeddings`) | Yes (`/completion`, `/props`, `/health`) | Yes (`llama-mtmd-cli --audio file.wav`) | CUDA, Vulkan, HIP/ROCm | **Top choice** |
| **Lemonade (AMD)** | Yes (signed installer) | Indirect (LLM via llama.cpp/ROCm; ASR via bundled Whisper.cpp/FastFlowLM at `/api/v1/audio/transcriptions`) | Yes (port **13305** as of v10.x; older builds used 8000) | Anthropic + Ollama-compatible APIs simultaneously | Yes (`lemonade run Whisper-Large-v3-Turbo`) | ROCm, Vulkan, CPU; **XDNA 2 NPU** | **Runner-up; only choice for one-runtime Whisper+Gemma** |
| **Ollama** | Yes | **No** for Gemma (the gemma4 library page shows "Text, Image input" only; audio bug #15333 open; `audios` field is silently ignored) | Yes via `/v1/chat/completions` shim | Yes (`/api/chat`, `/api/generate`) | No native ASR endpoint | CUDA, ROCm, Vulkan | Not viable for Gemma audio today |
| **LM Studio** | Yes | **No** — model card says "GGUFs are currently text-only" for Gemma 3n; Gemma 4 LM Studio cards list only text+image | Yes (`/v1/chat/completions` on port **1234**) | Yes (`/api/v0/*` and v1 native REST) | `lms` CLI loads/unloads models, no audio transcription endpoint | CUDA, Vulkan, ROCm (via runtime selector) | Not viable for Gemma audio today |
| **Jan** | Yes | No (Cortex backend is llama.cpp; audio not exposed in UI/API as of v0.6.x) | Yes (`localhost:1337`) | Limited | Limited | CUDA, CPU | Not viable |
| **vLLM** | Effectively no (Linux + WSL only on Windows) | **Yes** — `gemma3n_mm.py` and Gemma 4 recipe both expose `audio_url` content blocks; implements `SupportsTranscription` | Yes (`/v1/chat/completions`, `/v1/audio/transcriptions`, `/v1/audio/translations`) | OpenAI-compatible only | Limited | CUDA, ROCm | Excluded for native Windows |
| **SGLang** | Linux-first (Windows via WSL) | **Partial / broken** — issue #8361 reports audio_url failing for Gemma 3n on the OpenAI-compatible endpoint | Yes | Native gRPC + REST | No | CUDA, ROCm | Excluded |
| **Docker Model Runner** | Yes (Docker Desktop ≥4.41) | **No** Gemma 4 audio path documented; `ai/gemma4` Docker model treats audio as model-card metadata only; Docker's blog explicitly lists Gemma 3 / Moondream2 / SmolVLM as the multimodal examples | Yes (`http://localhost:12434/engines/v1`) | None | None | CUDA, Metal | Not viable for Gemma audio |
| **MLX LM** | **macOS-only** | Yes on Mac | n/a | n/a | n/a | n/a | **Excluded — not Windows** |
| **Unsloth Studio** | Yes (web UI on `:8888`) | Audio works in fine-tuning notebooks; Studio inference exposes its own UI/API but is fine-tuning-first, not an OpenAI-compatible production server in the llama.cpp/Ollama sense | Custom API endpoint | Custom | No | CUDA primarily | Possible but not optimal as a server |
| **Draw Things** | macOS/iOS only | n/a — image generation app (Stable Diffusion / Flux) | n/a | n/a | n/a | n/a | **Excluded — image gen only & not Windows** |
| **DiffusionBee** | macOS only | n/a — image generation app | n/a | n/a | n/a | n/a | **Excluded — image gen only & not Windows** |
| **JoyFusion** | macOS-focused | n/a — image generation app | n/a | n/a | n/a | n/a | **Excluded — image gen only** |
| **Pi (earendil-works/pi-mono)** | Cross-platform CLI but **not a local inference server** — it's a "bring-your-own-key" coding-agent toolkit that wraps OpenAI/Anthropic/Gemini/Llama through `pi-ai`. It does not host a model. | n/a — calls remote or local providers | n/a | n/a | n/a | n/a | **Excluded — wrong category, this is an agent harness not a runtime** |

### 3. The state of Gemma audio in `llama.cpp` (the linchpin)

The audio path in llama.cpp landed in three discrete steps:

1. **PR #21421 — "mtmd: add Gemma 4 audio conformer encoder support"** (merged April 2026). Added the actual audio encoder graph to the multimodal (`mtmd`) library. Tested on E2B and E4B with CPU, Vulkan, and CUDA backends, including the canonical "Mr. Quilter is the apostle of the middle classes…" LibriSpeech sample. Skips Whisper-style normalization for Gemma's mel output.
2. **Issue #21868 — `server: add input_audio content type routing for Gemma 4 audio inference`** (filed April 13, 2026). Demonstrated that immediately after #21421 merged, `llama-server` still returned HTTP 500 (`"audio input is not supported - hint: if this is unexpected, you may need to provide the mmproj"`) on `/v1/chat/completions` even with the mmproj loaded — because `server.cpp` did not dispatch `input_audio` blocks to `libmtmd`. The CLI `llama-mtmd-cli` worked; the HTTP server did not.
3. **Server-side fix merged in April 2026.** Independent third-party verification (amu_lab, April 12, 2026) confirms that `llama-server` now natively accepts Gemma-4 audio input on the OpenAI-compatible `/v1/chat/completions` endpoint using the `input_audio` content block with base64-encoded WAV/MP3, exactly mirroring OpenAI's `gpt-4o-audio-preview` schema.

This is the single most important fact for Parlotype: **as of May 2026, `llama-server` is the only Windows-friendly OSS runtime that exposes Gemma 4 audio through a stable HTTP API.** Both `llama-mtmd-cli` (subprocess) and `llama-server` (HTTP) work; the HTTP route is appropriate for a long-running C#/.NET app.

### 4. The OpenAI ASR API standard and how Gemma fits

OpenAI defines two primary surfaces for audio:

**A. `POST /v1/audio/transcriptions` and `/v1/audio/translations`** — the Whisper-style endpoints. The request is `multipart/form-data` with these fields (per OpenAI's reference):

- `file` — the audio file (WAV, MP3, FLAC, M4A, OGG, WEBM, MPEG, MPGA — all formats ffmpeg understands)
- `model` — a string ID
- `response_format` — `json` (default), `text`, `srt`, `verbose_json`, `vtt`
- `language`, `prompt`, `temperature`, `stream` (SSE)

**B. `POST /v1/chat/completions` with an `input_audio` content part** — the multimodal-LLM-style schema introduced for `gpt-4o-audio-preview`. The body is JSON; audio is sent inline as base64:

```json
{
  "model": "gemma-4-e4b",
  "messages": [{
    "role": "user",
    "content": [
      { "type": "text",
        "text": "Transcribe the following speech segment in English. Only output the transcription, with no newlines." },
      { "type": "input_audio",
        "input_audio": { "data": "<base64 WAV>", "format": "wav" } }
    ]
  }]
}
```

**Important interaction with Gemma:** Gemma's audio path is implemented as a *prompt-conditioned LLM*, not a CTC decoder. Quality is therefore prompt-sensitive: Google's own Gemma 4 audio docs prescribe the exact prompt template — "Transcribe the following speech segment in {LANGUAGE} into {LANGUAGE} text. Only output the transcription, with no newlines. When transcribing numbers, write the digits…" Send anything looser and you get free-form interpretation rather than verbatim text.

**Which API fits which runtime?**

- **llama-server**: implements **both** content-block style on `/v1/chat/completions` (production path for Parlotype) **but does not yet implement `/v1/audio/transcriptions`** — feature requested in issue #15291 and #21852, status: open as of May 2026.
- **vLLM**: implements both, including `/v1/audio/transcriptions` for any model that registers as `SupportsTranscription` (Gemma3n_mm.py does).
- **Lemonade**: implements **both** at `/api/v1/audio/transcriptions` (whisper.cpp backend) and `/api/v1/chat/completions` for Gemma — but the transcription endpoint is whisper-only; Gemma audio would need to go through chat completions.
- **LocalAI** (out of your candidate list, but worth flagging): is the only runtime that explicitly supports routing `/v1/audio/transcriptions` to a llama.cpp-backed multimodal-audio GGUF model (`backend: llama-cpp` + audio mmproj), and accepts every ffmpeg-supported format. If you eventually want a single endpoint that *looks like Whisper but talks to Gemma*, LocalAI is the cleanest option, but it adds an extra dependency.

### 5. ASR quality reality check

You should make this decision with eyes open. Two independent benchmarks of Gemma's ASR vs Whisper:

- **Open ASR Leaderboard runs (April 2026, James Ding via vLLM bf16 on RTX 6000 Pro Blackwell)**: Gemma 4 **E4B 4.17 % WER on LibriSpeech-clean** — actually beating Whisper base.en (4.25 %). But on the AMI noisy-meeting set, **E4B = 41 % WER**, and **E2B = 202 % WER** (the model wrote roughly twice as many words as the reference — pure hallucination). Whisper-large-v3 sits around 16 % on AMI by comparison.
- **Independent practitioner benchmarks (Ajjay K, Medium 2025)**: Whisper-large WER ~4.4 vs Gemma 3n-8B WER ~13.0 on a representative test set.

**Implication for Parlotype**: Gemma audio is "decent on clean speech, unreliable on hard inputs." A privacy-first voice-to-text app aimed at real users — whose audio will include accents, background noise, fast speech, and microphones of varying quality — should **not** silently bind ASR to Gemma alone. The pragmatic architecture is: keep your existing Whisper.net pipeline as the default ASR, and use the same local runtime (Gemma) for post-processing. If a runtime can also do Gemma-audio ASR as an opt-in mode, expose it as a model choice.

### 6. Hardware acceleration on Windows

| | CUDA (NVIDIA) | Vulkan (cross-vendor) | ROCm/HIP (AMD) | NPU (XDNA 2) | Notes |
|---|---|---|---|---|---|
| **llama.cpp** | Mature; CUDA 12 & CUDA 13 prebuilt zips per release (`llama-bXXXX-bin-win-cuda-13.1-x64`) | Mature; native Win zips | **Yes on Windows** — official prebuilt at `repo.radeon.com/rocm/llama.cpp/windows/...`; AMD also ships build for gfx110X/gfx115X/gfx120X. Note: `HSA_OVERRIDE_GFX_VERSION` *not* supported on Windows. | No | Best-in-class Windows binary story |
| **LM Studio** | Yes | Yes | Yes (selectable runtime) | No | Hides build complexity behind a runtime selector |
| **Ollama** | Yes | Yes | Yes | No | |
| **Lemonade** | No (CUDA not the focus) | Yes (default for many LLMs) | **Yes** — preview ROCm builds for Radeon RX 7000/8000 series; RDNA-class GPUs | **Yes** — only mainstream OSS runtime that schedules prefill to AMD Ryzen AI XDNA 2 NPU and decode to iGPU/dGPU | Genuinely differentiated on AMD AI PCs |
| **vLLM** | Yes | No | Yes (Docker `vllm/vllm-openai-rocm`) | No | Server/data-center optimized; Windows native unsupported |
| **Docker Model Runner** | Yes | Limited | Limited | No | |

For a Windows app aiming at the broadest installed base, **llama.cpp with Vulkan** is the safest universal default; CUDA users get faster prefill; AMD Ryzen AI / Radeon users will see a meaningful uplift through Lemonade.

### 7. CLI and audio format compatibility

`llama-mtmd-cli` is the canonical CLI path:

```
llama-mtmd-cli -m gemma-4-E2B-it-Q6_K.gguf \
  --mmproj mmproj-BF16.gguf \
  --audio sample.wav \
  -p "Transcribe this audio exactly." \
  --temp 1.0 --top-k 64 --top-p 0.95 \
  -ngl 99 --jinja
```

The `--audio` flag accepts WAV, MP3, FLAC (via miniaudio's linear resampler — input at non-16kHz is silently downsampled but quality drops; ship 16kHz mono PCM-16 from your VAD pipeline whenever possible). The man page says `--audio FILE … can be repeated if you have multiple files`.

For Parlotype, **HTTP from C# is preferable to shelling out to the CLI** — issue #21868's working note says CLI-per-request cold-loads the model in ~44 s on a Pi 5 (still ~3-5 s on a desktop with NVMe + 4090, but unacceptable for interactive UX). Keep one `llama-server` process running.

### 8. Concrete C# integration pattern

```csharp
// Program.cs — Parlotype shelling Gemma 4 audio through llama-server
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class GemmaLocalClient : IDisposable
{
    private readonly HttpClient _http;

    public GemmaLocalClient(string baseUrl = "http://127.0.0.1:8080")
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMinutes(2)
        };
    }

    /// <summary>
    /// Transcribe a WAV (16 kHz mono PCM16, ≤ 30 s) using Gemma 4 E4B
    /// via llama-server's OpenAI-compatible /v1/chat/completions input_audio block.
    /// </summary>
    public async Task<string> TranscribeAsync(
        string wavPath, string language = "English", CancellationToken ct = default)
    {
        var b64 = Convert.ToBase64String(await File.ReadAllBytesAsync(wavPath, ct));

        var body = new
        {
            model = "gemma-4-e4b",
            stream = false,
            temperature = 1.0,
            top_p = 0.95,
            top_k = 64,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text",
                              text = $"Transcribe the following speech segment in {language} into {language} text. " +
                                     "Only output the transcription, with no newlines. " +
                                     "When transcribing numbers, write the digits." },
                        new { type = "input_audio",
                              input_audio = new { data = b64, format = "wav" } }
                    }
                }
            }
        };

        using var resp = await _http.PostAsJsonAsync("/v1/chat/completions", body, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    /// <summary>Post-process previously transcribed text on the same loaded Gemma model.</summary>
    public async Task<string> PostProcessAsync(
        string transcript, string instruction, CancellationToken ct = default)
    {
        var body = new
        {
            model = "gemma-4-e4b",
            stream = false,
            messages = new[]
            {
                new { role = "system", content = instruction },
                new { role = "user",   content = transcript }
            }
        };
        using var resp = await _http.PostAsJsonAsync("/v1/chat/completions", body, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.GetProperty("choices")[0]
                              .GetProperty("message")
                              .GetProperty("content").GetString() ?? string.Empty;
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var r = await _http.GetAsync("/health", ct);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public void Dispose() => _http.Dispose();
}
```

The equivalent curl smoke test:

```bash
curl -X POST http://127.0.0.1:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"gemma-4-e4b","messages":[{"role":"user","content":[
       {"type":"text","text":"Transcribe verbatim."},
       {"type":"input_audio","input_audio":{"data":"<base64>","format":"wav"}}]}]}'
```

To launch llama-server with Gemma 4 audio enabled on Windows (using prebuilt binary `llama-bXXXX-bin-win-cuda-13.1-x64`):

```powershell
.\llama-server.exe -hf ggml-org/gemma-4-E4B-it-GGUF:Q4_K_M `
                   --mmproj <path-to>\mmproj-gemma-4-e4b-it-f16.gguf `
                   -c 24676 -ngl 99 --flash-attn on --jinja `
                   --host 127.0.0.1 --port 8080
```

The `--mmproj` flag is **required** for audio (and vision); `-hf` will pull the GGUF quant if not cached. If you forget it, the server returns the explicit error `"audio input is not supported - hint: if this is unexpected, you may need to provide the mmproj"`.

### 9. Process lifecycle for Parlotype

Three viable models, in increasing ambition:

1. **"Bring-your-own-runtime"** — Parlotype assumes the user has llama-server running on `127.0.0.1:8080`. Health-check on startup, surface a helpful first-run dialog with one-click instructions if not. Lowest engineering cost; offloads model management to the user.
2. **"Bundle and supervise"** — ship `llama-server.exe` and `mmproj`/`gguf` files inside Parlotype's installer; spawn the process from C# with `Process.Start`, monitor its `/health` endpoint, restart on crash, kill on app exit (use a Win32 Job Object or `Process.OnExit`). This is what most successful local-LLM-bundled apps do.
3. **"Embed via library"** — link directly against `llama.cpp` (e.g., via the LLamaSharp C# bindings or P/Invoke). More control, no separate process, but you re-implement the multimodal/HTTP plumbing yourself and lose the ability to swap runtimes per user choice.

Recommended for Parlotype: **option 2**. You said end-users should choose the model, so you need the runtime online and serving anyway. Bundle a known-good llama-server build (CUDA 13 + Vulkan dual-build) and let advanced users point at their own LM Studio/Ollama/Lemonade if they prefer.

### 10. Comparative ranking for Parlotype's specific use case

1. **llama.cpp / `llama-server`** — only OSS runtime with stable Gemma 4 audio over OpenAI-compatible HTTP on Windows; CUDA + Vulkan + ROCm prebuilt Win zips on every release; same loaded model handles ASR and post-processing without reload (saves your VRAM). The single Gemma E4B mmproj load (~9.6 GB for Q4) replaces both a Whisper model and a separate text LLM. **Pick this.**
2. **Lemonade (AMD)** — uniquely strong on Ryzen AI / Radeon hardware, and is the only candidate that can run **Whisper for ASR + Gemma for post-processing behind a single OpenAI-compatible localhost endpoint** (`/api/v1/audio/transcriptions` → whisper.cpp; `/api/v1/chat/completions` → Gemma). If you decide ASR quality matters more than Gemma single-model elegance, Lemonade is the best one-runtime route. Also has a streaming WebSocket realtime ASR endpoint with built-in VAD as of v9.4.1, which fits Parlotype's voice pipeline very naturally.
3. **Ollama** — by far the smoothest installer/UX on Windows and the API your users will recognize; **but Gemma audio is not yet plumbed through** (`audios` field silently ignored, GGML assertion crashes on Gemma 4 E4B audio per issue #15333). Watch this — it will likely catch up fast since Ollama tracks llama.cpp. Until then, fine for Gemma text post-processing only.
4. **vLLM** — best Gemma audio implementation overall (includes both `/v1/audio/transcriptions` and `audio_url` chat content), but **not a Windows native runtime**. WSL2 works but adds a dependency you don't want for a desktop app.
5. **LM Studio** — excellent UX but its Gemma 3n / Gemma 4 model cards explicitly say GGUFs are text-only or text+image, and audio is not surfaced through the API. Good for Gemma text-mode only.
6. **Jan, Docker Model Runner, Unsloth Studio** — none expose Gemma audio over a programmatic local API in a production-ready way as of May 2026.
7. **SGLang** — Linux-first, audio path partially broken on Gemma 3n.
8. **Excluded**: Draw Things, DiffusionBee, JoyFusion (image generation tools, no LLM audio), MLX LM (macOS-only), Pi (an agent harness, not a local runtime).

## Details

### A. Why Gemma 4 audio's prompt-LLM design matters for your pipeline

Gemma's audio encoder generates ~6 tokens per second at the encoder side and ~25 tokens of model context per second of audio (Gemma 4); the audio is then *integrated as input tokens to the language model*, which decodes a transcription as it would any other generation. This has three engineering consequences:

1. **The transcription quality depends on your prompt.** Use Google's prescribed template verbatim. Drift, and you'll get summaries instead of transcripts.
2. **Gemma will happily *interpret* audio.** That's the feature, not a bug — it can simultaneously transcribe and translate ("first output the transcription in English, then one newline, then output 'Korean: ' then the translation in Korean", per Google's Gemma 4 audio docs). For Parlotype, this means you can collapse "transcribe → translate" into one model call.
3. **30-second clip limit and 16 kHz mono.** This is a hard architectural constraint. Your existing VAD pipeline almost certainly already produces sub-30s segments, but the C# preprocessing must downmix-to-mono and resample-to-16kHz before base64 encoding (use NAudio or your existing audio chain — `WaveFormatConversionStream` works).

### B. The hidden gotcha: `/v1/audio/transcriptions` vs `/v1/chat/completions`

The user's note rightly flags this: **OpenAI's transcriptions endpoint was designed around Whisper's encoder-decoder architecture** (audio in → text out, no chat). Gemma is a prompt-LLM. Most runtimes therefore expose Gemma audio through chat completions with `input_audio` blocks, **not** through `/v1/audio/transcriptions`. The exceptions are vLLM and LocalAI, both of which adapt Gemma to fit Whisper's transcription endpoint shape under the hood (vLLM's `SupportsTranscription` interface; LocalAI's `backend: llama-cpp` configuration).

Implication for Parlotype: **plan for two API shapes**, not one. Your local-runtime abstraction layer should support:
- `POST /v1/audio/transcriptions` (multipart, Whisper-style) — for Whisper.cpp, faster-whisper, OpenAI-cloud, and as a fallback
- `POST /v1/chat/completions` with `input_audio` content blocks (JSON, base64) — for Gemma via llama-server, vLLM, etc.

This gives you the flexibility your "user picks the model" requirement implies, and lets you swap runtimes without rewriting transport code.

### C. Streaming

`llama-server` supports SSE streaming on `/v1/chat/completions` (`stream: true`). For ASR-with-input_audio, the stream emits `chat.completion.chunk` deltas as Gemma decodes the transcription. This is appropriate for live UX where the user wants to see words appearing. For batch transcription (90% of Parlotype's likely use case), keep `stream: false` for simpler error handling.

LM Studio and Ollama also support OpenAI-style SSE streaming on their `/v1/chat/completions` shims.

### D. Memory implications of single-model dual-purpose

Gemma 4 E4B at Q4_K_M is ~9.6 GB on disk and roughly the same in VRAM at runtime, plus mmproj (~150-300 MB depending on quant) and KV cache (~1-2 GB at 32k context). Versus loading Whisper-large-v3 (3.1 GB) **plus** a separate Gemma 4 E4B for post-processing (9.6 GB) = ~13 GB. So the single-model strategy saves roughly 3 GB of VRAM, which on an 8 GB consumer GPU is the difference between fitting and not fitting. This is the quantitative case for "one runtime that does both."

### E. Process lifecycle and crash handling specifics

`llama-server` exposes a public `/health` endpoint that returns:
- `200 {"status": "ok"}` when ready,
- `503` while the model is still loading,
- `500` on internal errors.

Poll on app startup and after any 5xx response. Use Windows Job Objects (via `Microsoft.Windows.Sdk.Win32.JobObjects` or P/Invoke `CreateJobObject` + `AssignProcessToJobObject` + `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`) to guarantee the child llama-server dies if Parlotype crashes. Don't rely on `Process.Kill()` alone — it's racy on Windows and can leave the GGUF mapped in memory until reboot.

For graceful shutdown, llama-server responds to SIGINT cleanly; on Windows this means `GenerateConsoleCtrlEvent` for an attached console process, or simply terminating via the Job Object on app exit. Cold-start time for Gemma 4 E4B Q4 from NVMe + RTX 4070 is ~3-5 s; from spinning rust with Vulkan it can be 30+ s — pre-launch on app start, don't do it lazily on first transcription.

### F. Audio format support across the relevant runtimes

| Format | llama.cpp (mtmd) | Lemonade Whisper | vLLM Gemma | Ollama (when fixed) |
|---|---|---|---|---|
| WAV (16-bit PCM, 16 kHz mono) | ✅ ideal | ✅ ideal | ✅ ideal | ✅ |
| MP3 | ✅ (miniaudio) | ✅ (ffmpeg) | ✅ | ⚠️ unverified |
| FLAC | ✅ (miniaudio) | ✅ (ffmpeg) | ✅ | ⚠️ unverified |
| OGG | ⚠️ via miniaudio | ✅ (ffmpeg) | ✅ | ⚠️ |
| M4A / AAC | ❌ — strip to WAV first | ✅ (ffmpeg) | ✅ | ❌ |

Recommendation: **always preprocess to 16 kHz mono PCM-16 WAV in C# before sending.** This avoids server-side quality variance (the linear resampler in mtmd is "lossy by design" per the PR description) and keeps your compatibility matrix simple. Use NAudio or pin a small ffmpeg binary if you need M4A in the input pipeline.

## Recommendations

### Stage 1 — Ship now (this quarter)

1. **Adopt `llama-server` from llama.cpp** as Parlotype's local-runtime baseline. Bundle the official Windows CUDA-13 build (~80 MB zipped) and the Vulkan build as a fallback. Use the Vulkan build by default if no CUDA driver is detected.
2. **Bundle Gemma 4 E4B Q4_K_M** GGUF + matching mmproj (`mmproj-gemma-4-e4b-it-f16.gguf`) — total ~10 GB. Provide a "lite" install option that downloads on first use rather than bundling.
3. **Wrap llama-server lifecycle in C# behind an abstraction** (`IAsrBackend` + `ITextLlmBackend`) that accepts both `/v1/audio/transcriptions` (multipart) and `/v1/chat/completions` (JSON with input_audio) shapes. This is the single most important architectural decision: it future-proofs you against runtime churn and gives you the model-switching UX you want.
4. **Keep your existing Whisper.net pipeline as the default ASR.** Make Gemma audio an opt-in ("Experimental: transcribe with Gemma") in settings. Reason: Gemma 4 E4B's WER on noisy audio (41% on AMI) is too high to be the default for a privacy-first transcription app; it shines on clean read speech but fails on hard inputs.

### Stage 2 — When numbers move

- **Re-evaluate Gemma-as-default ASR if** Gemma 4 E4B's AMI WER drops below 20% in a future patch, or if Google ships Gemma 4.5 / Gemma 5 with a stronger audio encoder.
- **Add Lemonade as a second backend** when your AMD Ryzen AI / Radeon user share crosses ~5% — Lemonade is the only path to NPU-accelerated ASR on those machines, and the bundled Whisper.cpp + Gemma combination behind one localhost is exactly your "one runtime, two roles" requirement.
- **Add Ollama as a backend** as soon as their Gemma audio path stabilizes (track issue #15333 and the gemma4 model tags page — when "Audio input" appears alongside "Text, Image input", they're ready). Many Parlotype users will already have Ollama running for other apps, and respecting their existing install is good UX.

### Stage 3 — If demand justifies it

- **Embed via LLamaSharp / P-Invoke to libllama** instead of running a separate `llama-server` process. Skips one network hop, reduces memory by removing the HTTP server, and lets you stream tokens directly to the UI. Cost: significantly more engineering and you forfeit the per-user runtime-choice flexibility.
- **Wire up a streaming, push-to-talk, low-latency path** using llama-server's SSE on `/v1/chat/completions` (or Lemonade's WebSocket realtime endpoint) for the "live caption" UX. This is where the integrated Gemma audio+post-processing single-call really shines: you can transcribe and translate (or transcribe and grammar-fix) in a single round trip, on a single loaded model.

### Benchmarks that should change your decision

- **Gemma 4 E4B AMI/CHiME WER below 20%** → make Gemma the default ASR for Parlotype.
- **Ollama merges Gemma audio support to stable** → make Ollama the default bundled runtime (better Windows UX than llama-server).
- **OpenAI standardizes a non-Whisper-shaped multimodal ASR endpoint, or major runtimes converge on `/v1/audio/transcriptions` for Gemma** → simplify your dual-API abstraction layer down to one shape.
- **AMD Ryzen AI install base in Parlotype telemetry > 5%** → ship Lemonade as a recommended runtime.

## Caveats

- **The llama-server `input_audio` HTTP routing fix is recent (April 2026).** It works as documented in third-party reports and per the merge of #21421 plus the follow-up patch closing #21868, but you should pin a specific llama.cpp build (current as of this report: b9000+ series) and run a smoke-test suite against it in CI. The audio pathway is materially newer than llama.cpp's vision pathway and may have edge-case bugs that haven't surfaced yet.
- **Gemma 4 was released April 2, 2026, just over a month before this report.** Some runtime support is described in vendor blogs (Google, AMD, NVIDIA) using forward-looking phrasing — "support is coming," "the next Ryzen AI SW update will integrate," "support is planned." I have flagged these where I noticed them; treat any "support" claim that points at a future driver/SW release as a roadmap item, not a shippable dependency.
- **The `/v1/audio/transcriptions` endpoint is NOT yet implemented in llama-server** as of May 2026 (issues #15291 and #21852 are open and have not been merged). If you want a Whisper-shaped endpoint pointing at a Gemma model, you must use vLLM or LocalAI — not llama-server. This is the principal engineering reason your runtime-abstraction layer must accept *both* API shapes.
- **Gemma 4 E2B is too unreliable for production ASR.** The Open ASR Leaderboard run shows it hallucinating 200% WER on noisy meeting audio. Restrict any Gemma ASR mode to E4B at minimum, and gate it behind a clean-speech detector or a per-clip confidence check.
- **Audio clips are hard-capped at 30 seconds.** Your existing VAD pipeline must respect this. For longer recordings, segment client-side and stitch transcripts.
- **Gemma's audio encoder is monolingual-friendly but multilingual-fragile.** The "script collapse in multilingual ASR" benchmark (arxiv 2604.08786) found that Gemma 4 E2B catastrophically substitutes Latin script for some languages (Urdu collapses to 6.5% SFR under generic prompts) unless you use script-aware prompting. If Parlotype targets non-English users at scale, prompt engineering matters more than for Whisper.
- **vLLM and SGLang are excluded from the Windows recommendation** primarily for platform reasons, not capability. If a future Parlotype version targets Linux or WSL, vLLM jumps to the top of the list — its Gemma audio implementation is the most complete in OSS.
- **The "Pi" entry in the candidate list** (earendil-works/pi-mono) is an AI agent CLI / harness, not a local inference runtime. It calls out to OpenAI/Anthropic/Gemini/local-Llama backends rather than hosting a model itself, so it's structurally not a candidate for what Parlotype needs. I excluded it on category grounds.
- **Source quality flag**: A few of the 2026-dated sources cited above (codersera, dev.to, mayhemcode, pinggy) are AI-blog-style summaries that occasionally over-paraphrase primary docs. Where the same fact also appears in Google's official Gemma 4 model card, the AMD Day-Zero technical article, the vLLM recipe repo, the llama.cpp PR/issue tracker, or the Hugging Face Gemma 4 blog, treat the primary source as authoritative; the secondary sources are useful for confirming the consensus reading but should not be the sole basis for an architectural decision.