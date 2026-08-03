---
title: Knowledge Base
type: index
status: active
last_updated: 2026-08-02
summary: Semantic memory — stable facts learned across sessions that are not derivable from code
---

# Knowledge Base

This directory stores **stable facts** learned across sessions — things that are not obvious from reading the code alone.

## How to Add Knowledge
1. Create a new `.md` file in this directory with YAML frontmatter
2. Include: `type: knowledge`, `tags`, `created`, `summary`
3. Add an entry to this index

## Entries

| File | Tags | Summary |
|------|------|---------|
| [[benchmark-warmup]] | benchmark, warm-up, cold-start | Every benchmark run performs one throwaway warm-up transcription; reported as `warmupTimeMs` |
<!-- Format: | [[filename]] | one-line summary | date | -->

| Fact | Summary | Learned |
|------|---------|---------|
| [[whisper-net-quirks]] | Whisper.net 1.9.0 NuGet `CudaHelper` differs from upstream master; `WhisperLogLevel` enum is inverted vs native ggml; `WhisperFactory` has no finalizer, so an undisposed factory leaks the whole model; native libs ship as plain `<None>` content with hard-coded TargetPaths, so `publish -r <rid>` cannot filter them and a `net10.0` TFM fires the Windows *and* macOS blocks | 2026-07-31 |
| [[agent-skills]] | Claude/Copilot skills require `.claude/skills/<name>/SKILL.md`; description-triggered discovery does not reliably fire at session boundaries, so per-session protocols belong in CLAUDE.md | 2026-04-30 |
| [[avalonia-devtools]] | Classic `Avalonia.Diagnostics` retired in 12; replacement is `AvaloniaUI.DiagnosticsSupport` (in-app) + `AvaloniaUI.DeveloperTools` (`avdt` global tool); free Essentials tier needs portal signup. Build telemetry: `AvaloniaStatsTask` POSTs hashed build metadata to `av-build-tel-api-v1.avaloniaui.net`; Community tier cannot opt out; no runtime telemetry. Set `AVALONIA_TELEMETRY_OPTOUT=1` when upgrading to paid tier. | 2026-04-30 |
| [[asyncrelaycommand-flicker]] | CommunityToolkit.Mvvm `AsyncRelayCommand` disables all buttons sharing the command while executing; use sync `RelayCommand` + fire-and-forget for shared commands in `ItemsControl` | 2026-04-30 |
| [[benchmark-pipeline-recommendations]] | Optimal STT settings from 234+ config sweep: Medium/en/beam=1/temp=0.0/no-VAD for accuracy; BaseEn for speed. language="en" gives 2× speedup; higher beam sizes never help | 2026-05-01 |
| [[vad-silence-threshold-constraint]] | Sub-500ms silence thresholds cause 77%+ WER. WaitTimeOption minimum is now Medium (500ms). Instant/VeryShort/Short removed. PipelineSimulator added for benchmark testing. | 2026-05-02 |
| [[sharphook-suppress-event]] | SharpHook `SuppressEvent` only works with `SimpleGlobalHook`; `TaskPoolGlobalHook` and `EventLoopGlobalHook` silently ignore it | 2026-05-02 |
| [[whisper-translation-models]] | Whisper translation to English only works with multilingual models (Medium+); English-only (*En) models don't support it; Base/Small produce mixed results | 2026-05-03 |
| [[vulkan-runtime-probing]] | Vulkan API version packing (variant/major/minor/patch bits), `VkPhysicalDeviceProperties` 824-byte struct with stable 276-byte head, and `RuntimeOptions.LoadedLibrary` semantics in Whisper.net | 2026-05-06 |
| [[gemma4-cuda-blackwell]] | Gemma 4 E2B bfloat16/float16 hallucinates on CUDA with Blackwell GPUs (RTX 5070 Ti, compute 12.0); CPU works; bitsandbytes 4-bit crashes on audio encoder `torch.finfo()` | 2026-05-08 |
| [[llamacpp-gemma4-integration]] | llama-server `/props` endpoint for server identification; Gemma 4 E4B GGUF filenames (case-sensitive `bf16` not `f16`); Vulkan audio performance on RTX 5070 Ti | 2026-05-09 |
| [[llama-cpp-release-assets]] | llama.cpp release asset naming (`llama-b{N}-bin-{platform}-{backend}-{arch}.{ext}`), CUDA `cudart-*.zip` companion pairing rules, no "latest" alias, GitHub 60 req/h unauthenticated limit, ETag weak-validator round-trip rule | 2026-05-17 |
| [[llama-server-hf-download]] | llama-server downloads HF models itself via `-hf <repo>[:quant]` (defaults Q4_K_M, auto-mmproj, `HF_TOKEN` for gated, `~/.cache/huggingface/hub`); Parlotype uses its own C# downloader instead for progress UX (ADR-029) | 2026-05-19 |
| [[whisper-cuda-runtime-packaging]] | **Historical (ADR-049).** `Whisper.net.Runtime.Cuda` added only `ggml-cuda-whisper.dll` (~150 MB) and bundled no cudart/cublas, so the Full build still needed the user’s CUDA toolkit — the evidence behind dropping it. Sizes: Lite ~720 MB / Full ~870 MB (2026-05); single artifact 731 MB after removal | 2026-07-31 |
| [[brand-positioning]] | Parlotype is positioned as **Local by default. Cloud by choice.** — single app, local-default, opt-in cloud (BYOK); tagline preserved; replaces "privacy-first" wording (ADR-032) | 2026-05-25 |
| [[win32-keyboard-layout]] | Windows keyboard layouts are per-thread — query the foreground window's thread, then drill to its focused input thread via `GetGUIThreadInfo.hwndFocus` (multi-thread apps like Win11 Notepad); HKL low word = LANGID; transient LANGIDs (0x2000 range) throw `CultureNotFoundException` | 2026-06-18 |
| [[avalonia-popup-patterns]] | Headless `CaptureRenderedFrame` excludes the popup layer (screenshot popover content directly); setting `DataContext` on an element rebases its other bindings (wrap in a Panel); light dismiss consumes the anchor click so button-toggled popups work without extra state | 2026-06-11 |
| [[whisper-ui-thread-loading]] | `WhisperFactory.FromPath` + processor `Build()` are synchronous/CPU-bound despite the async method; wrap in `Task.Run` or they freeze the UI thread and any `DispatcherTimer` loading animation (ADR-038) | 2026-06-27 |
| [[avalonia12-frameless-window]] | Avalonia 12: `SystemDecorations` obsolete → `WindowDecorations` enum; frameless rounded corners need transparent window + root Border; `BeginMoveDrag` blocks until drop on Windows (persist position after it returns); headless `Screens.All` is actually populated (one real virtual screen), verified empirically | 2026-07-06 |
| [[sherpa-onnx-quirks]] | org.k2fsa.sherpa.onnx NuGet is CPU-only (GPU needs source build); config objects use public fields; NeMo transducer results carry no confidence/language; `AcceptWaveform` auto-resamples with stderr log; load/decode are synchronous — wrap in `Task.Run`; ships a 391 MB `onnxruntime_providers_cuda.dll` that is never loaded (Provider = "cpu") — ~54% of the published output | 2026-07-31 |
| [[avalonia-composite-control-patterns]] | Fluent `SplitButton`'s one-frame/many-parts trick (corner-radius/border-thickness filter converters); making an inner `TextBox` chrome-less via `Border#PART_BorderElement` overrides + outer `:focus-within`; reusable `PasswordBoxReveal/HideButtonData` glyphs and `TextControlButton*` brushes | 2026-07-10 |
| [[wasapi-capture-buffer-sizing]] | NAudio `BytesRecorded` is bytes of the *native* format — sizing float buffers from it over-allocates ~4× into the LOH (~1.5 MB/s while recording); callbacks are sequential so pooled buffers are safe; `DiscardOnBufferOverflow` silently drops audio behind slow consumers | 2026-07-13 |
| [[windows-clipboard-exclusion-formats]] | `ExcludeClipboardContentFromMonitorProcessing` / `CanIncludeInClipboardHistory`=0 / `CanUploadToCloudClipboard`=0 must be set in the same OpenClipboard session as the content; `EmptyClipboard` clears them; only verifiable manually (Win+V) | 2026-07-13 |
| [[huggingface-lfs-digests]] | HF tree API `lfs.oid` is the authoritative SHA-256 for LFS files; non-LFS blobs expose only a git SHA-1 — hash them directly; pin the revision the downloader actually uses | 2026-07-13 |
| [[naudio-resampler-read-cost]] | NAudio's WDL resampler chain allocates per `Read` **in proportion to the requested count** (2.7 MB/call at 38,400 vs 0.19 MB at 3,200, same output) — never pass `BytesRecorded` or a pooled array's `.Length` as the read count; request ~2× the expected resampled output | 2026-07-14 |
| [[whisper-net-linux-native-crash]] | Whisper.net's native ggml library hard-crashes the .NET test host on Linux (`GGML_ASSERT`) regardless of CPU/CUDA/Vulkan selection — root cause unresolved; `benchmark.yml` pinned to `windows-latest` as a workaround | 2026-07-18 |
| [[sharphook-modifier-sides]] | Left/right modifiers are distinct `KeyCode`s *and* distinct `EventMask` bits; the unqualified `EventMask.Ctrl`/`Alt`/… are composites of both sides, so use `&` not `HasFlag`. Enables Hold Right Ctrl and the AltGr filter. Simulated events are visible to the hook, so it can be driven from an automated harness | 2026-07-30 |
| [[onnxruntime-gpu-providers-dead-weight]] | SileroVad 1.3.0 pulls Microsoft.ML.OnnxRuntime.Gpu, whose 391 MB `onnxruntime_providers_cuda.dll` was 54% of the published output and can never load (CPU providers pinned; ORT version mismatch with sherpa-onnx’s own onnxruntime.dll). Filtered out in `Directory.Build.targets` — 731 MB → 338 MB | 2026-07-31 |
| [[avalonia-axaml-text-gotchas]] | A `Text=`/`Run Text=` value starting with `{` is parsed as a markup extension — escape literal braces with `{}`; adjacent `<Run>`s on separate source lines get an implicit space, so punctuation-leading Runs render detached | 2026-08-01 |
| [[nuget-assettype-native-metadata]] | `ResolvedFileToPublish` items carry `%(AssetType) == 'native'` when resolved from a package's `runtimes/<rid>/native/` folder; Parlotype's own managed PDBs/DLLs have no `AssetType` at all — lets a `Directory.Build.targets` filter distinguish third-party native assets without a filename list | 2026-08-01 |

| [[velopack-pack-folder-is-destructive]] | Velopack owns `%LOCALAPPDATA%\{packId}` and deletes it **entirely** on uninstall *and* on a Setup.exe re-run; Windows case-insensitivity means a data folder differing only in case is the same directory. Store nothing user-owned under the pack folder | 2026-08-01 |
| [[vpk-pack-version-must-exceed-feed]] | `vpk pack` hard-fails when `--packVersion` is ≤ any release already in `--outputDir` — and `vpk download` puts the live feed there for delta generation, so the two steps are coupled. The CI dry-run default had to become `9999.0.0-dryrun`; it only worked before because `vpk download` had failed on every run until v0.4.0 created the first feed | 2026-08-02 |
| [[github-release-empty-body-fallback]] | A GitHub Release with an empty body renders the **tagged commit's message** instead of nothing — so a release can look like it has (verbose, commit-dump) notes while `gh release view --json body` returns `""`. `vpk upload github` sets no body. Judge release notes by the API, never by the page | 2026-08-02 |
| [[named-sync-primitives]] | Named `Mutex` ownership is **per-thread** (a same-thread re-acquire succeeds recursively; `ReleaseMutex` off-thread throws `ApplicationException`), `AbandonedMutexException` still hands you the lock, named `EventWaitHandle`s throw on Unix while named mutexes work there, and an auto-reset event queues one `Set()` so a signal that beats the listener is not lost. Decides how a single-instance guard is written *and* how it can be tested | 2026-08-02 |
| [[avalonia-click-event-vs-command]] | `button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))` bypasses `OnClick`, so a bound `Command` never executes — fine for `Click`-handler dialogs, silent no-op for command-bound buttons; headless tests must simulate a real `MouseDown`/`MouseUp` at the button's point instead. `AdornerLayer.SetAdorner` *does* work under Avalonia.Headless (FluentTheme window template supplies the layer) | 2026-08-02 |
| [[avalonia-window-activation-focus]] | `Window.Activate()` focuses **no control** inside the window (use `IsDefault` + `Focus(NavigationMethod.Tab)`, the latter for a visible ring), and a window shown from a queued `Dispatcher.Post` activates *after* a caller that found it synchronously — yield at `DispatcherPriority.Background` first. Order-dependent, so the first visit looks fine and later ones don't. Verify cross-window focus with `GetGUIThreadInfo().hwndFocus` + `PostMessage`, never `SendKeys`/`SetForegroundWindow` | 2026-08-02 |

## Distillation Rules
- Only store facts that are **not derivable** from reading current code or git history
- Include the "why" — reasoning, context, constraints
- Update or remove entries when they become stale
- Prefer specific, actionable facts over vague observations
