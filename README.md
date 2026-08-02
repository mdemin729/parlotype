# Parlotype

**Speak freely. Type privately.**

Parlotype is a **local-by-default** voice-to-text desktop application: on-device speech recognition is the default, and your voice never leaves your machine in local mode. Three local engines ship today — **[NVIDIA Parakeet TDT v3](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3)** via [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) (the default: CPU-only and fastest), **[Whisper](https://github.com/openai/whisper)** (widest language coverage, GPU-accelerated), and **[Gemma 4](https://deepmind.google/models/gemma/gemma-4/)** (Google's multimodal model, run via a local [llama.cpp](https://github.com/ggml-org/llama.cpp) sidecar). Two **opt-in** cloud engines ship alongside them for users whose hardware can't deliver the latency they need — bring-your-own-key, never selected automatically. See [Provider Modes](#provider-modes).

## Tech Stack

- **.NET 10** — Runtime
- **Avalonia UI 12** — Cross-platform desktop UI (tray-based)
- **sherpa-onnx** (`org.k2fsa.sherpa.onnx`) — Parakeet TDT v3 speech recognition, in-process on CPU
- **Whisper.net** — On-device speech recognition (OpenAI Whisper), Vulkan / CPU
- **llama.cpp (`llama-server`)** — Gemma 4 speech recognition sidecar
- **Silero VAD** — Voice activity detection
- **NAudio** — Windows audio capture (WASAPI)
- **CommunityToolkit.Mvvm** — MVVM framework
- **SharpHook** — Global hotkeys (hold / double-tap / chord gestures)
- **ZLogger** — Structured logging
- **DPAPI** (`System.Security.Cryptography.ProtectedData`) — encrypted storage for cloud API keys
- **BenchmarkDotNet** — Micro-benchmarks (allocations / hot-path latency)

## Platform Support

Parlotype currently runs on **Windows** only. macOS and Linux support are planned for the future. Two features are explicitly Windows-only today: DPAPI encryption of cloud API keys (other platforms fall back to base64 with a logged warning) and keyboard-layout source detection (other platforms fall back to auto-detect).

**GPU acceleration** applies to the **Whisper** engine and runs on **any** Vulkan-capable GPU (AMD, Intel, NVIDIA), with automatic CPU fallback if no compatible GPU is detected. CUDA was dropped in [ADR-049](docs/decisions/049-drop-whisper-cuda-runtime.md) — NVIDIA cards are still accelerated, via Vulkan. The active runtime can be changed under **Settings → Speech engine → Whisper runtime**. Parakeet is **CPU-only by design** (see [ADR-041](docs/decisions/041-parakeet-v3-sherpa-onnx.md)) and needs no GPU to be fast; Gemma 4 acceleration depends on the `llama-server` build you install.

## Download / Releases

Pre-built Windows binaries are published on the [Releases page](../../releases). Each
release ships one self-contained `win-x64` build — no .NET runtime install required.

**Recommended: `Parlotype-win-Setup.exe`.** It installs per-user into
`%LOCALAPPDATA%\Parlotype` with **no administrator prompt**, adds Start Menu and desktop
shortcuts, and can update itself in place ([ADR-053](docs/decisions/053-velopack-packaging-and-auto-update.md)).

`Parlotype-win-Portable.zip` is also published for anyone who wants to unzip and run
without installing. The portable build **cannot update itself** — you re-download it by
hand each time.

Being self-contained it is sizeable (~280 MB installed). Updates after the first install
are incremental, so a typical update downloads a fraction of that. Parlotype falls back to
CPU automatically if no Vulkan-capable GPU is found, and the default Parakeet engine needs
no GPU at all. Builds are currently **unsigned**, so Windows SmartScreen may warn on first
launch — choose *More info → Run anyway*.

Speech models are **not** bundled — they download on demand into
`%LOCALAPPDATA%\parlotype-data\models\` the first time you need them, and survive both
updates and uninstalls.

> **Upgrading from a version before the installer?** Older builds stored data in
> `%LOCALAPPDATA%\parlotype`, which is now where the installer itself lives. Rename that
> folder to `%LOCALAPPDATA%\parlotype-data` before installing to keep your settings and
> downloaded models; otherwise Parlotype starts fresh and re-downloads models on demand.
> See [docs/RELEASING.md](docs/RELEASING.md#migrating-from-a-pre-adr-053-install).

### Updates

Parlotype checks for new releases automatically, shortly after launch and every six hours
after that. New versions download in the background and install on next restart.

The check is an **anonymous HTTPS GET** of
`https://api.github.com/repos/mdemin729/parlotype/releases` — the public list of releases
for this repository — followed by a download of the release file itself if a newer version
exists. No account, no machine identifier, no install id, no usage data, and no custom
headers are sent. Nothing about you or your machine is transmitted.

**This is the only network request Parlotype makes while your speech engines are local.**
Turn it off at **Settings → Application → Updates**, which also shows when the feed was
last reached and offers a manual *Check now*. With the toggle off, the updater makes no
outbound requests at all.

## Speech Engines

Parlotype ships five interchangeable speech-to-text engines — three local, two cloud. Switch between them under **Settings → Speech engine → Engine**.

| Engine | Where it runs | Download | Languages | Translation |
|--------|---------------|----------|-----------|-------------|
| **Parakeet TDT v3** (default) | Local, CPU only | ~670 MB (INT8) | 25 European, always auto-detected | — |
| **Whisper** | Local, Vulkan / CPU | 75 MiB – 2.9 GiB by model | ~99, selectable | To English |
| **Gemma 4** | Local, `llama-server` sidecar | ~5.5–15 GiB by variant | Full catalog | To any language |
| **OpenAI-compatible** (cloud) | Your provider (OpenAI, Groq, …) | — | Auto-detected | — |
| **xAI Grok** (cloud) | xAI | — | Auto-detected | — |

### Provider Modes

Parlotype is **local by default. Cloud by choice.**

- **Local (default)** — Parakeet, Whisper, or Gemma 4 run entirely on your machine. No audio leaves the device. This is the only mode enabled out of the box and the only mode required to use the app.
- **Cloud (opt-in)** — When local hardware can't deliver the latency you need, enable a cloud engine under **Settings → Speech engine → Engine** and supply your own API key under **Cloud providers**. Audio is then sent over HTTPS to the provider you configure. A persistent **Cloud** badge is shown on the Transcribe widget while a cloud engine is active, and cloud engines are never auto-selected — there is no anonymous or trial path.

See [ADR-032](docs/decisions/032-online-speech-providers-positioning.md) for the positioning principles and brand commitments, and [ADR-043](docs/decisions/043-cloud-speech-providers-v1.md) for the implementation.

### Parakeet TDT v3 (default)

NVIDIA's Parakeet TDT 0.6B v3 (FastConformer encoder + Token-and-Duration Transducer decoder, CC-BY-4.0), run **in-process** through the official sherpa-onnx .NET bindings — no sidecar process, no port management.

- **CPU-only, and fast** — INT8 ONNX decoding measured at RTF ≈ 0.07–0.13 on a 16-core CPU. No GPU dependency, which makes it the best out-of-box experience on AMD/Intel machines.
- **25 European languages**, always **auto-detected** — the model has no language-forcing parameter, so Parlotype offers no source picker for it and hides the Language UI entirely while it is active.
- **Native punctuation and capitalization**; transcribe-only (no translation task).
- **Models** — `parakeet-tdt-0.6b-v3-int8` (~670 MB, the default) and a full-precision `parakeet-tdt-0.6b-v3-fp32` entry (~2.6 GB, better on accented speech at ~2× decode time and ~3× RAM). Selectable under **Settings → Speech engine → Parakeet model**; downloaded from HuggingFace (`csukuangfj/...`) with a progress dialog on first use.

See [ADR-041](docs/decisions/041-parakeet-v3-sherpa-onnx.md) and [ADR-042](docs/decisions/042-parakeet-default-language-ux.md).

### Whisper

OpenAI Whisper via [Whisper.net](https://github.com/sandrohanea/whisper.net). Widest language coverage and the only engine with GPU acceleration. Pick any GGML model from **Tiny** through **Large v3 Turbo** under **Settings → Speech engine → Whisper model**; models download on demand.

- **~99 languages** with an explicit source-language choice.
- **Translation to English** via Whisper's `translate` task. Only multilingual models can do this — the English-only models (`*.en`) and Large v3 Turbo cannot, so the toggle is disabled with an explanation and the pipeline gates the flag regardless of UI state ([ADR-033](docs/decisions/033-translation-model-capability.md)). Your preference is preserved and resumes when you pick a capable model.
- **Whisper output** settings cover automatic punctuation and profanity filtering.

### Gemma 4 (llama.cpp sidecar)

Google's multimodal **Gemma 4** model, run through a local [`llama-server`](https://github.com/ggml-org/llama.cpp) process. Parlotype manages the sidecar for you: audio is sent over a loopback HTTP connection, so nothing leaves your machine.

- **Model files** — GGUF weights plus an audio projector (`mmproj`), downloaded from HuggingFace (`ggml-org/gemma-4-E2B-it-GGUF` / `gemma-4-E4B-it-GGUF`) into `%LOCALAPPDATA%\parlotype\models\`. Sizes range from ~5.5 GiB to ~15 GiB depending on variant and quantization; the default is **E4B (Q4_K_M)** (~5.9 GiB).
- **Server build** — the `llama-server` binary is installed and managed from the Settings UI (or you can point Parlotype at your own copy).
- **Translation to any language** in a single call: because it is an LLM, transcription and translation happen together, driven by the prompt ([ADR-034](docs/decisions/034-source-target-language-selection.md), [ADR-037](docs/decisions/037-gemma4-source-target-prompts.md)).
- **Editable prompts** — **Settings → Speech engine → Prompts**. Prompt bodies substitute `{speech_lang}` (the spoken language) and `{text_lang}` (the output language — the same as the spoken language whenever translation is off); the built-in default carries purpose-specific bodies for transcription, translation, and auto-detect. A collapsible **How prompts work** panel on the page explains both placeholders, the conditions that trigger translation, and how the built-in's three bodies differ from a custom single-body prompt. The legacy `{language}` token is retired.
- Quality is best on clean English speech; the engine is marked **Experimental** in the UI.

### Cloud providers (opt-in)

Both cloud engines are bring-your-own-key and configured under **Settings → Speech engine → Cloud providers** (visible only while a cloud engine is selected).

- **OpenAI-compatible** — `POST {base URL}/audio/transcriptions`, the OpenAI transcription protocol. Defaults to `https://api.openai.com/v1` with `gpt-4o-mini-transcribe`. Pointing the base URL at Groq, a self-hosted Whisper server, or any other compatible host is the supported way to use other providers.
- **xAI Grok** — `POST {base URL}/stt`. Defaults to `https://api.x.ai/v1` with `grok-stt`.

Both always auto-detect the language (Parlotype sends no language-forcing parameter) and are transcribe-only, so the Language UI hides while they are active. Base URLs must be HTTPS unless the host is loopback, which keeps LM Studio / llama.cpp self-hosting working. Failures surface as dialogs rather than silent no-ops: a missing or rejected key offers an **Open settings** deep link; quota, rate-limit, and provider-outage errors show an informational message and recording keeps running.

## Languages & Translation

The language surface is **capability-driven** — each engine declares what it supports and the UI renders only choices that actually take effect ([ADR-036](docs/decisions/036-language-ux-rebuild.md), [ADR-042](docs/decisions/042-parakeet-default-language-ux.md)).

- **Settings → Speech engine → Language** shows one relationship row: `[source] → [target]`. The connector is the master translation toggle; each side opens a searchable floating picker with recently-used languages pinned on top.
- **Source** can be **Auto-detect**, an explicit language, or **Keyboard layout** — a sentinel that resolves at record time to the language of your current keyboard layout (Windows only; falls back to auto-detect when detection is unavailable or the language isn't supported).
- **Target** takes the shape the engine supports: a toggle for Whisper (English only), a full picker for Gemma 4, and a disabled card with a note for transcribe-only engines.
- **The whole Language page disappears** for engines with no language choice — Parakeet and both cloud engines. The Transcribe widget's quick-picker strip hides with it and the window compacts from 118 px to 88 px. Your Whisper/Gemma source and translation setup is left untouched, so it survives a round trip through another engine.
- **Switching engines never loses selections silently** — an invalidated choice falls back (unsupported source → keyboard layout, no-translation form → translation off, etc.) with a short toast explaining what changed.

## Dictation Hotkeys

Dictation has two genuinely different modes — holding a key for one sentence, and going hands-free — so Parlotype binds **both out of the box** rather than making you choose ([ADR-047](docs/decisions/047-multi-binding-dictation-hotkeys.md)):

| Gesture | Mode | Why this one |
|---------|------|--------------|
| **Hold Right Ctrl** | Push-to-talk | The same physical key on every platform, and comfortable to hold. Left and right Ctrl are distinct keys to the hook, so all your normal Ctrl shortcuts keep working. |
| **Double-tap Ctrl** | Toggle | macOS Dictation's own default on external keyboards, and it collides with nothing on Windows or Linux. |
| **Ctrl+Alt+Space** | Toggle | An explicit chord for anywhere bare-modifier detection isn't available. |

**Esc cancels.** While recording, Escape stops and **discards** the take — nothing is transcribed and nothing is typed. It passes straight through to whatever app you're in the rest of the time.

Manage all of this under **Settings → Input → Hotkeys**: add gestures from a preset menu or record a chord, remove them, and flip a chord between push-to-talk and toggle. Hold and double-tap gestures don't offer that choice — releasing a held key has to mean "stop", and a double-tap has no release to hang "stop" on.

New bindings are validated before they're accepted. Shortcuts the OS has already claimed (Win+L, Win+H, Win+Ctrl+S, …) and duplicates of your own bindings are **refused**; combinations that merely tend to collide are **accepted with a note** — `Ctrl+Shift+Space` shows parameter hints in Visual Studio and VS Code, and any `Ctrl+Alt+<letter>` can fire while typing accented characters, because AltGr *is* Ctrl+Alt on European layouts. (Parlotype ignores chord matches while right Alt is held for exactly that reason.)

Two behaviours worth knowing:

- **Right Ctrl+C still copies.** A key pressed right after a hold begins means you reached for a shortcut, not for dictation, so the gesture is abandoned — at normal typing speed no recording is created at all. Keys pressed later are ignored, since people do type while dictating.
- **Push-to-talk starts ~250 ms after the key goes down** in the default setup. Because hold and double-tap share the Ctrl key, the hold waits out the double-tap window; without it every deliberate double-tap would flash a recording into existence and immediately throw it away. A gesture on a key nothing else uses — Hold Right Alt, say — starts instantly.

**Upgrading?** If you'd picked your own hotkey, it carries over as your only binding and the new defaults are *not* added on top — no surprise global shortcuts. Anyone still on the old `Ctrl+Shift+Space` default gets the new set, since that combination fights the IDEs most of this audience lives in.

## The Transcribe Widget

The Transcribe window is a frameless always-on-top mini card (172 px wide, 88 px tall — 118 px when the language strip is shown), modelled on the Windows Voice Typing widget ([ADR-040](docs/decisions/040-frameless-compact-transcribe-window.md)).

- Drag it by the **grip strip** at the top; the rest of the surface is not draggable.
- The ✕ button *hides* the widget — recording continues, and the tray icon reopens it. **Esc** hides it too when idle, but **cancels and discards** the take while recording (see [Dictation Hotkeys](#dictation-hotkeys)).
- Its position is remembered across restarts in `window-state.json`, and self-heals to centre-screen if the saved spot is off-screen (monitor unplugged, resolution change).
- The record button carries all state: blue chrome while recording, a waveform while you speak, and a spinner while the model loads. Hover it to be reminded of your gesture — *"Hold Right Ctrl to talk · Esc to cancel"* — or hover the card for the status text.
- **Model loading** — the spinner appears only if the load outlasts ~200 ms, so warm starts don't flash. Optionally enable **Preload model on startup** under **Settings → Speech engine → Engine** to warm the model in the background at launch (off by default; takes effect on the next launch). See [ADR-038](docs/decisions/038-speech-model-prewarm-and-loading-state.md).

## Privacy & Security

Findings from the [2026-07 security audit](docs/security/2026-07-11-security-audit.md) are addressed in [ADR-046](docs/decisions/046-security-hardening-batch.md):

- **Transcripts are never logged** at any level — only lengths and counts. The rolling log file is capped at `Information`; the console keeps `Debug` for development.
- **Dictated text stays out of clipboard history** — the clipboard injection path sets the Windows exclusion flags, so dictation never reaches Win+V history or cross-device Cloud Clipboard, and the original clipboard content is restored afterwards.
- **Model downloads are integrity-checked** — SHA-256 is computed while streaming and compared before the atomic move; a mismatch throws and the destination file is never touched. Catalog entries without a digest degrade to the previous behaviour with a warning rather than blocking the download.
- **Cloud API keys live outside `settings.json`** — in `secrets.json`, encrypted with DPAPI (`CurrentUser` scope) on Windows, so settings backups, sync, or diagnostics never carry credentials. Undecryptable values are treated as absent and re-prompted. On non-Windows platforms they are base64-encoded with a logged warning (OS keychain support is a known gap).
- **Cloud base URLs must be HTTPS** unless the host is loopback, enforced at recognizer initialization.
- **`settings.json` and `secrets.json` are written atomically** (temp file + move), and the `llama-server` sidecar is spawned with an argument list rather than a quoted command line.
- **The update check is the only outbound request** in local mode, it is anonymous, and it can be switched off. See [Updates](#updates) for the exact endpoint and what is sent.

## Where Parlotype Stores Data

User data lives under `%LOCALAPPDATA%\parlotype-data\`, deliberately **outside** the
install directory. The installer owns `%LOCALAPPDATA%\Parlotype` and erases it on
uninstall, so nothing of yours is kept there — your models and settings survive updates
and uninstalls alike ([ADR-053](docs/decisions/053-velopack-packaging-and-auto-update.md)).

| Path | Contents |
|------|----------|
| `settings.json` | User-configured settings (microphone, engine, model, hotkeys, languages, theme) |
| `window-state.json` | Transient window chrome state (Transcribe widget position) |
| `secrets.json` | Cloud API keys, DPAPI-encrypted on Windows |
| `models\` | Downloaded Whisper / Parakeet / Gemma 4 model files |
| `logs\` | Rolling log files (`Information` and above; no transcript text), plus `velopack.log` for install/update events |

**Uninstalling keeps this folder by default** — it can hold several GB of models that are
expensive to re-download, and plenty of uninstalls are really reinstalls. If you'd rather
have a clean removal, turn on *Delete everything when I uninstall Parlotype* at
**Settings → Application → Data** before uninstalling; uninstall then removes the whole
folder. You can also delete it by hand at any time.

That page also shows the folder's exact location — with *Copy path* and *Open folder*
buttons — how much space your downloaded models occupy, and a *Delete downloaded models…*
button if you want the disk space back without giving up your settings and API keys.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

Vulkan is not required unless you want GPU-accelerated **Whisper** — the default Parakeet engine runs on CPU.

### Vulkan (optional, AMD / Intel / NVIDIA GPUs)

Vulkan is the only GPU runtime for Whisper, on every vendor. Most modern GPU drivers (Radeon, Intel Arc, GeForce) already ship the Vulkan loader (`vulkan-1.dll`) — no extra install is needed for end users.

If Parlotype reports that the Vulkan loader is missing, install the **Vulkan SDK** from [vulkan.lunarg.com/sdk/home](https://vulkan.lunarg.com/sdk/home) (the SDK bundles a system-wide Vulkan loader and is also useful for development).

Parlotype will automatically detect and use Vulkan when available, in priority order **Vulkan → CPU** (Auto mode). You can pin a specific runtime under **Settings → Speech engine → Whisper runtime**; changes take effect after an app restart.

## Build & Run

```powershell
dotnet build Parlotype.slnx
dotnet run --project src\Parlotype.Desktop
```

The app starts minimized to the system tray. Click the tray icon for an Open / Settings / Exit menu, or use a [dictation hotkey](#dictation-hotkeys) — hold Right Ctrl, or double-tap Ctrl — to open the Transcribe window and start recording.

### Visual inspector (Debug builds)

The desktop app supports the official Avalonia 12 Developer Tools (Essentials
edition — free under AvaloniaUI's community licence for organisations under
€1M revenue). Setup is per-developer:

1. Install the standalone tool once:

   ```powershell
   dotnet tool install --global AvaloniaUI.DeveloperTools
   ```

2. Launch it in a separate window:

   ```powershell
   avdt
   ```

3. Run the app in **Debug** configuration, give a Parlotype window focus, and press
   **F12**. The inspector will connect and show the visual tree, properties,
   layout, and styles.

First-time activation requires a free [AvaloniaUI Portal](https://avaloniaui.net/)
account. The `AvaloniaUI.DiagnosticsSupport` package is referenced with a
`Configuration == Debug` condition, so Release builds carry no extra binaries.
See [ADR 016](docs/decisions/016-avalonia12-developer-tools.md).

## Run Tests

```powershell
dotnet test
```

## Benchmark

Evaluate speech recognition quality with the built-in benchmark tool:

```powershell
# Run a benchmark (Whisper smoke test)
dotnet run --project src\Parlotype.Benchmark -- run `
  --config datasets\smoke-test-config.json `
  --datasets datasets `
  --output results

# Run the Parakeet smoke test (model auto-downloads headlessly)
dotnet run --project src\Parlotype.Benchmark -- run `
  --config datasets\parakeet-smoke-config.json `
  --datasets datasets `
  --output results

# List historical benchmark runs
dotnet run --project src\Parlotype.Benchmark -- list --output results

# Compare two runs side by side
dotnet run --project src\Parlotype.Benchmark -- compare `
  --run-a <run-id-a> --run-b <run-id-b> --output results

# Export a run as CSV, Markdown, or JSON
dotnet run --project src\Parlotype.Benchmark -- export `
  --run-id <run-id> --format markdown --output results

# Rebuild SQLite index from existing JSON result files
dotnet run --project src\Parlotype.Benchmark -- import --output results

# Run a parameter sweep across configurations
dotnet run --project src\Parlotype.Benchmark -- sweep `
  --config datasets\sweep-config.json `
  --datasets datasets `
  --output results

# Check for regressions against a baseline (for CI)
dotnet run --project src\Parlotype.Benchmark -- check `
  --baseline <run-id> --current latest `
  --output results --max-wer-delta 2.0
```

The benchmark computes **WER** (Word Error Rate), **CER** (Character Error Rate), and **RTF** (Real-Time Factor) against WAV/FLAC datasets with ground-truth transcriptions. Results are saved as JSON and auto-indexed into SQLite for historical queries. Supports tag/sample filtering (`--tags`, `--samples`), side-by-side comparison with delta metrics, and export to CSV, Markdown, or JSON.

### Micro-benchmarks

`Parlotype.Benchmark` measures *transcription quality*. Allocation and hot-path latency
questions ("how many bytes does one WAV encode allocate?") are answered by a separate
BenchmarkDotNet project ([ADR-044](docs/decisions/044-microbenchmark-project.md)). It is
not a test project, so `dotnet test` is unaffected — run it manually in Release:

```powershell
dotnet run -c Release --project src\Parlotype.MicroBenchmarks -- --filter *
```

## Project Structure

```
src/
├── Parlotype.Core/             # Domain interfaces and models (zero external deps)
├── Parlotype.Platform/         # Platform implementations (Whisper, sherpa-onnx, NAudio, SharpHook, DPAPI)
├── Parlotype.Desktop/          # Avalonia 12 desktop app (tray-based, entry point)
├── Parlotype.Desktop.Tests/    # Avalonia headless UI tests (xUnit v3)
├── Parlotype.Benchmark/        # CLI quality benchmark (WER/CER/RTF, sweep, compare, CI check)
├── Parlotype.Benchmark.Tests/  # Benchmark unit tests
├── Parlotype.MicroBenchmarks/  # BenchmarkDotNet allocation/latency suites (not a test project)
└── Parlotype.Tests/            # Core + Platform unit tests (xUnit)

datasets/                       # Benchmark configs + sample datasets with ground truth
docs/
├── decisions/                  # Architecture Decision Records
├── architecture/               # Architecture notes
├── research/                   # Engine and provider research
└── security/                   # Security audits
```

## Architecture

- **Core** defines interfaces (`ISpeechRecognizer`, `IAudioCaptureService`, `ISettingsService`, `IWindowStateService`, `ISecretStore`, etc.)
- **Platform** implements those interfaces with real dependencies
- **Desktop** wires everything via dependency injection and provides the UI
- Clean separation ensures Core has no UI or platform dependencies
- Speech recognition is pluggable: `SpeechRecognizerFactory` resolves one of five `ISpeechRecognizer` implementations (Parakeet, Whisper, Gemma 4, OpenAI-compatible, xAI Grok) from the persisted `SpeechEngine` setting. Cloud engines needed **zero** changes to the audio pipeline or the `ISpeechRecognizer` contract.
- Engines declare `LanguageCapabilities`, and both the Settings page and the Transcribe widget render from one shared `LanguageRelationshipViewModel` — a new engine gets correct language UI by declaring capabilities, not by touching views
- The recording path is three single-threaded stages joined by unbounded channels — capture callback (RMS + pooled copy) → segmenter (VAD, sole owner of the sample buffer) → transcription (no polling) — and the capture path allocates nothing in steady state via `ArrayPool<float>` ([ADR-045](docs/decisions/045-audio-pipeline-allocation-threading.md))
- Configurable Whisper parameters (language, beam size, temperature, thread count) via `WhisperOptions` for benchmarking
- Architectural decisions are recorded as ADRs in [`docs/decisions/`](docs/decisions/) — Gemma 4 spans ADRs 024–030 and 037, the language UX 034–036 and 042, and the cloud providers 032 and 043

## License

This project is licensed under the [MIT License](LICENSE).
