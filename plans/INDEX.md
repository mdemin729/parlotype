# Plans

## Completed

| Plan | Completed | Description |
|------|-----------|-------------|
| [2026-07-09-cloud-speech-providers](2026-07-09-cloud-speech-providers/) | 2026-07-10 | First two opt-in cloud engines (ADR-043): `SpeechEngine.OpenAiCompatible` (OpenAI/Groq/any compatible host via base-URL setting) + `SpeechEngine.XaiGrok`; batch multipart WAV upload, BYOK keys in `ISecretStore`/`DpapiSecretStore` (`secrets.json`, DPAPI on Windows), Cloud providers settings section, persistent Cloud badge on TranscribeWindow (ADR-032 commitment #3). Amendment: not-configured record-start shows a `ConfirmationDialog` (typed `CloudProviderNotConfiguredException`) with "Open settings" deep link to `SettingsSection.CloudProviders` |
| [2026-07-08-parakeet-fp32-variant](2026-07-08-parakeet-fp32-variant/) | 2026-07-08 | Second Parakeet catalog entry `parakeet-tdt-0.6b-v3-fp32` (~2.6 GB, encoder graph + ONNX external-data weights via optional `EncoderWeightsFileName`); selectable in the existing Parakeet model settings section; smoke WER 1.9 % vs INT8's 5.6 % at ~2× decode time / ~3× RAM (ADR-041 amendment) |
| [2026-07-07-parakeet-default-language-ux](2026-07-07-parakeet-default-language-ux/) | 2026-07-07 | Parakeet becomes the default engine (ADR-042): unset-setting fallbacks, Parakeet-first cards, silent model auto-download on first use. Language UI hides for choice-less engines via `LanguageCapabilities.HasLanguageChoices` + `IsVisibleFor` — Language page + Transcribe strip hidden for Parakeet, widget compacts 118→88 px, language prefs survive engine round trips |
| [2026-07-06-parakeet-v3-engine](2026-07-06-parakeet-v3-engine/) | 2026-07-07 | Third speech engine: NVIDIA Parakeet TDT 0.6B v3 via sherpa-onnx (ADR-041) — in-process, CPU-only INT8, ~670 MB 4-file HF download, 25 European languages auto-detected, transcribe-only (`TranslationForm.None`); engine card + Parakeet-restricted model section; Benchmark `parakeet` config (smoke WER 5.6 %, RTF 0.072) |
| [2026-07-05-transcribe-window-compact-redesign](2026-07-05-transcribe-window-compact-redesign/) | 2026-07-05 | Frameless compact TranscribeWindow (design C2, ADR-040): 172×112, drag via top grip strip (Windows Voice Typing style), ✕/Esc hide to tray, position persistence (`TranscribeWindowPosX/Y`) with off-screen fallback, status text in tooltip only; HTML prototypes in `prototypes/` |
| [2026-06-27-gemma4-source-target-prompts](2026-06-27-gemma4-source-target-prompts/) | 2026-06-27 | Gemma 4 prompts use `{speech_lang}`/`{text_lang}` (retiring `{language}`); built-in default gains translation + auto-detect bodies (3-body `PromptTemplate`); custom prompts stay single-body with code-appended translation; recognizer source/target selection matrix; keeps `TranslationEnabled` toggle (ADR-037) |
| [2026-06-08-language-ux-rebuild](2026-06-08-language-ux-rebuild/) | 2026-06-11 | Language UX rebuild (ADR-036, supersedes ADR-035 UX): keyboard-layout source (`IKeyboardLayoutService` + Win32 P/Invoke), `TranslationForm` model-driven target forms (toggle/full/none), shared `LanguageRelationshipViewModel`, floating popover pickers, summary + engine-switch fallback toasts, Transcribe quick-picker strip + flyout |
| [2026-05-25-translation-model-capability](2026-05-25-translation-model-capability/) | 2026-05-25 | Gate Whisper translation by model capability (`SupportsTranslation`); disable toggle + model-list hint for `*En` and Large v3 Turbo, preserving user preference (ADR-033) |
| [2026-05-25-language-selection](2026-05-25-language-selection/) | 2026-05-25 | Source & target language selection: source picker (both engines) + Gemma 4 arbitrary-target translation; `LanguageCatalog`/`LanguageCapabilities`/`RecentLanguages` (ADR-034) |
| [2026-05-31-language-settings-ux-redesign](2026-05-31-language-settings-ux-redesign/) | 2026-05-31 | Unified Language page: `[Source] → [Target]` row with arrow as translation toggle; reusable inline `LanguagePickerView`; `TranslationEnabled` master key + per-role MRU; `LanguageSettingsMigrator` for legacy state; Whisper-output translate toggle removed (ADR-035) |

## In Progress

| Plan | Started | Description |
|------|---------|-------------|
| [2026-07-11-audio-pipeline-perf-security](2026-07-11-audio-pipeline-perf-security/) | 2026-07-13 | Audio pipeline allocation/performance improvements (capture-callback LOH churn, WavEncoder, buffer copies, threading) measured via new BenchmarkDotNet project + full security audit (transcripts-in-logs, model download integrity, HTTPS enforcement, clipboard history exclusion) |

## Planned

| Plan | Created | Description |
|------|---------|-------------|
| [2026-05-01-pipeline-settings-alignment](2026-05-01-pipeline-settings-alignment/) | 2026-05-01 | Align pipeline defaults with ADR-011 benchmark recommendations (Medium model, language=en, beam=1) |
