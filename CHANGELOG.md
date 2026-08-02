# Changelog

Notable changes to Parlotype, written for the people who use it. Engineering
rationale lives in [docs/decisions/](docs/decisions/).

This file is the source of truth for release notes: the release workflow copies
the section matching the tag into the GitHub Release body, so nothing ships
without an entry here ([ADR-054](docs/decisions/054-curated-release-notes.md)).

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and Parlotype follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.0] — 2026-08-02

### Highlights

- **Parlotype installs and updates itself.** A real installer replaces the
  hand-unzipped folder, and Parlotype now checks for new versions in the
  background and updates in place — no more downloading a fresh zip every time.
- **The download is 82 MB instead of 253–385 MB.** One build, one file. The
  confusing "full" and "lite" choice is gone, and after the first install
  updates arrive as patches of a few hundred kilobytes.
- **Bring your own cloud engine, if you want one.** Two optional cloud
  transcription services (any OpenAI-compatible host, including Groq, and xAI
  Grok) sit alongside the on-device engines. They are off by default, need your
  own API key, and show a persistent **Cloud** badge whenever one is active.

### ⚠️ Action required if you used an earlier version

Parlotype's data folder moved from `%LOCALAPPDATA%\parlotype` to
`%LOCALAPPDATA%\parlotype-data`, because the installer now owns the old
location and wipes it on uninstall. **There is no automatic migration.** With
Parlotype closed, and *before* running the installer:

```
move "%LOCALAPPDATA%\parlotype" "%LOCALAPPDATA%\parlotype-data"
```

Skipping this is safe — Parlotype starts with default settings and re-downloads
models on demand. You would just lose your settings, saved API keys and the
models you had already downloaded. See
[the migration notes](https://github.com/mdemin729/parlotype/blob/master/docs/RELEASING.md#migrating-from-a-pre-adr-053-install)
if you install first and need to recover afterwards.

Parlotype is not code-signed yet, so Windows SmartScreen warns on first run.

### Added

- **Cloud engines (opt-in).** OpenAI-compatible (configurable base URL, so
  OpenAI, Groq or a self-hosted server all work) and xAI Grok. Keys are stored
  encrypted with Windows DPAPI, never in `settings.json`. Cloud engines always
  auto-detect the language, so the language controls hide when one is selected.
- **Hotkeys you can actually choose.** Dictation now takes a *list* of gestures
  instead of one chord, including hold-a-modifier and double-tap-a-modifier.
  New defaults: **hold Right Ctrl** to talk, **double-tap Ctrl** to toggle, and
  **Ctrl+Alt+Space**. The old `Ctrl+Shift+Space` default is retired — it was
  Parameter Info in Visual Studio and signature help in VS Code. If you had
  picked your own hotkey, it is kept as your only binding.
- **Press Escape to throw a dictation away** while it is running, instead of
  waiting for it to transcribe into whatever window you were in.
- **Settings → Updates.** Automatic update checks (on by default, one anonymous
  request to the public GitHub release feed — you can turn it off), when it last
  checked, a manual **Check now**, and **Restart to update** once one is staged.
- **Settings → Application → Data.** See where your data lives, copy the path or
  open the folder, see how much disk your downloaded models use, delete them,
  and opt in to having everything removed if you ever uninstall (off by default).
- **"How prompts work"** panel on the Gemma 4 prompt settings, explaining the
  `{speech_lang}` and `{text_lang}` placeholders and when translation kicks in.

### Changed

- **GPU acceleration for Whisper is Vulkan-only.** The CUDA option is gone. It
  never shipped the NVIDIA libraries it needed, so it only worked if you had
  separately installed the ~3 GB CUDA toolkit — which is why half the runtime
  settings page was toolkit instructions. Measured on LibriSpeech test-other,
  Vulkan is within 8–26% of CUDA on speed with identical accuracy on Small and
  Medium, and *better* accuracy on Large v3 Turbo. NVIDIA cards are still
  accelerated, through Vulkan. If you had selected CUDA, you are moved to
  **Auto**, and NVIDIA users on the larger models get roughly 800 MB of RAM
  back. Gemma 4 is unaffected and can still use CUDA builds of `llama-server`.
- **Recording allocates ~10× less memory** — about 3 MB/s instead of ~30 MB/s
  during dictation — so long sessions put much less pressure on the garbage
  collector. Voice detection also moved off the microphone callback thread,
  where a slow moment could silently drop audio.
- **Dictation never reaches your clipboard history.** Injected text is now
  marked to stay out of Win+V history and Cloud Clipboard sync.
- **Transcripts are never written to logs** — only their length. Log files are
  capped at Information level so debug detail cannot persist.
- **Every model download is checked against a SHA-256 digest** and fails without
  touching your model folder if it does not match.
- **Cloud provider URLs must be HTTPS** unless they point at your own machine.
- **Settings and saved keys are written atomically**, so a crash mid-save can no
  longer corrupt them into a silent reset.
- Parlotype ships with its own waveform icon instead of a placeholder, and the
  executable is now `Parlotype.exe`.

### Fixed

- **Switching the Whisper runtime mid-session no longer breaks recording.**
  Whisper picks its runtime once per process, so a change only takes effect
  after a restart — but nothing said so, and every record press afterwards
  failed with an error implying broken drivers while leaking a full model's
  worth of RAM per attempt (up to ~3 GB, ~18 GB with large-v3). Parlotype now
  tells you a restart is required, on the settings page and before anything
  loads.
- **Push-to-talk no longer misses your key release** while a model is still
  loading, and cancelling during a cold load takes effect immediately instead of
  seconds later.
- **Removing every hotkey binding sticks.** Clearing the list used to silently
  hand the defaults back on the next launch.
- **Escape only cancels when pressed on its own.** It used to fire with any
  modifiers held, so a `Ctrl+Escape` binding discarded your dictation, and
  `Ctrl+Esc`/`Alt+Esc` were swallowed from the rest of Windows while recording.
- **Gemma 4 prompts substitute `{text_lang}` correctly** in custom prompts,
  including while translating, where it previously leaked through as raw text.
  All three built-in prompt bodies now ask for punctuation explicitly.

<details>
<summary>Under the hood</summary>

Full rationale for each item is in the linked decision record.

- Packaging moved to Velopack 1.2.0 with delta updates
  ([ADR-053](https://github.com/mdemin729/parlotype/blob/master/docs/decisions/053-velopack-packaging-and-auto-update.md)).
- Published output shrank 731 MB → 180 MB by dropping the CUDA runtime
  ([ADR-049](https://github.com/mdemin729/parlotype/blob/master/docs/decisions/049-drop-whisper-cuda-runtime.md)),
  the never-loaded ONNX Runtime GPU execution providers
  ([ADR-050](https://github.com/mdemin729/parlotype/blob/master/docs/decisions/050-drop-onnx-runtime-gpu-providers.md)),
  Whisper natives built for other platforms
  ([ADR-051](https://github.com/mdemin729/parlotype/blob/master/docs/decisions/051-publish-only-target-rid-runtimes.md))
  and native Skia/HarfBuzz PDBs
  ([ADR-052](https://github.com/mdemin729/parlotype/blob/master/docs/decisions/052-drop-native-pdbs-from-publish.md)).
- Audio pipeline reworked into channel-joined stages with pooled buffers
  ([ADR-045](https://github.com/mdemin729/parlotype/blob/master/docs/decisions/045-audio-pipeline-allocation-threading.md)),
  measured by a new micro-benchmark project
  ([ADR-044](https://github.com/mdemin729/parlotype/blob/master/docs/decisions/044-microbenchmark-project.md)),
  plus a full security audit and its remediations
  ([ADR-046](https://github.com/mdemin729/parlotype/blob/master/docs/decisions/046-security-hardening-batch.md)).
- Hotkey recognition moved into Core as pure timestamp-driven state machines
  ([ADR-047](https://github.com/mdemin729/parlotype/blob/master/docs/decisions/047-multi-binding-dictation-hotkeys.md));
  the Whisper runtime latch became a Core contract
  ([ADR-048](https://github.com/mdemin729/parlotype/blob/master/docs/decisions/048-whisper-runtime-latch-and-factory-lifetime.md)).
- Migrated off obsolete Avalonia AXAML APIs; the benchmark workflow is pinned to
  Windows runners because Whisper.net's native library crashes the .NET test
  host on Linux.

</details>

## [0.3.0] — 2026-07-08

### Highlights

- **A new default engine: NVIDIA Parakeet TDT 0.6B v3.** It runs on the CPU, is
  substantially faster than Whisper, and detects all 25 supported European
  languages automatically — so there is nothing to configure. Whisper and
  Gemma 4 are still there if you need ~99 languages or translation.

### Added

- Parakeet is downloaded automatically the first time you use it (~670 MB, via a
  dialog you can cancel).
- A full-precision Parakeet variant (~2.6 GB) for noticeably better accuracy at
  roughly twice the decode time and three times the RAM. Selectable in settings.

### Changed

- **The dictation window is a compact, frameless widget** (172×112). Drag it by
  the strip along the top, and ✕ or Escape hides it to the tray. Its position is
  remembered between runs, with a fallback if the screen it was on is gone.
- Language controls are hidden for Parakeet, which has no language choice to
  make. Your Whisper and Gemma 4 language preferences survive switching engines.

### Fixed

- Releasing a push-to-talk key while the model was still loading no longer
  leaves the recording stuck.

## [0.2.0] — 2026-06-27

### Highlights

- **Rebuilt language settings.** One page showing `[Source] → [Target]`, where
  the arrow itself is the translate toggle, with searchable pickers and a
  recently-used list for each role.

### Added

- Pick the spoken language and, for Gemma 4, an arbitrary target language to
  translate into.
- Parlotype can take the spoken language from your current Windows keyboard
  layout, so it follows you as you switch layouts.
- A quick language picker on the dictation widget itself, so you do not have to
  open settings to change it.
- A loading spinner on the dictation widget, and an optional setting to load the
  model at startup so the first recording does not wait for it.

### Changed

- Gemma 4 prompts use `{speech_lang}` and `{text_lang}` instead of the old
  single `{language}` placeholder, and the built-in prompt gained dedicated
  bodies for translation and for auto-detected input.

### Fixed

- The translate-to-English toggle is disabled for Whisper models that cannot
  translate (the `.en` models and Large v3 Turbo), with an explanation in the
  model list, instead of silently doing nothing. Your preference is preserved
  for models that can.
- Keyboard-layout detection now reports the layout of the window you are typing
  into, not Parlotype's own, and no longer floods the log.

## [0.1.0] — 2026-05-24

First public release.

- Local-by-default voice-to-text for Windows: audio never leaves your machine.
- Two on-device engines — Whisper (~99 languages, optional translate-to-English)
  and Gemma 4 via llama.cpp, with configurable transcription prompts.
- Global hotkeys with push-to-talk and toggle modes, and a warning when you pick
  a shortcut Windows has already claimed.
- Tray-based dictation widget with a live waveform, microphone selection, theme
  choice, and speech settings (wait time, punctuation, profanity filter).
- Whisper models are downloaded on demand from within the app.
- Voice activity detection (Silero) so only speech is sent to the recognizer.
- Optional GPU acceleration via Vulkan, or CUDA in the `-full` download.

[Unreleased]: https://github.com/mdemin729/parlotype/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/mdemin729/parlotype/releases/tag/v0.4.0
[0.3.0]: https://github.com/mdemin729/parlotype/releases/tag/v0.3.0
[0.2.0]: https://github.com/mdemin729/parlotype/releases/tag/v0.2.0
[0.1.0]: https://github.com/mdemin729/parlotype/releases/tag/v0.1.0
