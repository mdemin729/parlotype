# AGENTS.md

Instructions for coding agents in this repository. Every statement here must stay true.

## Project

Parlotype is a **local-by-default** voice-to-text tray app (.NET 10, Avalonia 12): a global
hotkey starts the microphone and the recognized text is pasted into the focused window. In
local mode audio never leaves the machine.

- Local engines: **Parakeet TDT v3 via sherpa-onnx (default**, CPU-only, 25 European
  languages, always auto-detected, [ADR-041](docs/decisions/041-parakeet-v3-sherpa-onnx.md)/[042](docs/decisions/042-parakeet-default-language-ux.md)),
  Whisper.net (~99 languages, translate-to-English) and Gemma 4 via a managed `llama-server`.
- Opt-in cloud engines: OpenAI-compatible and xAI Grok, bring-your-own-key, never the
  default, with a persistent Cloud badge while active ([ADR-032](docs/decisions/032-online-speech-providers-positioning.md), [ADR-043](docs/decisions/043-cloud-speech-providers-v1.md)).
  API keys go through `ISecretStore` (DPAPI), never into `settings.json`.

## Commands

```bash
dotnet build Parlotype.slnx                 # must be clean: warnings are errors
dotnet test                                 # every test project
dotnet test src/Parlotype.Tests             # Core + Platform (xUnit v2)
dotnet test src/Parlotype.Desktop.Tests     # Avalonia headless UI (xUnit v3)
dotnet test src/Parlotype.Benchmark.Tests   # benchmark metrics and storage (xUnit v2)
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"
dotnet run --project src/Parlotype.Desktop  # launch the app
dotnet run -c Release --project src/Parlotype.MicroBenchmarks -- --filter *   # not part of dotnet test
```

The transcription-quality CLI (`run`, `sweep`, `list`, `compare`, `export`, `check`,
`import`) is documented in [`src/Parlotype.Benchmark/README.md`](src/Parlotype.Benchmark/README.md).

On Windows a build often fails with a file lock held by a `.NET Host` or `testhost` process
from an earlier run. Kill that process by PID and rebuild before suspecting your change.

## GPU runtime

- Whisper's only GPU runtime is **Vulkan** (`Whisper.net.Runtime.Vulkan`, AMD/Intel/NVIDIA).
  CUDA was removed ([ADR-049](docs/decisions/049-drop-whisper-cuda-runtime.md)): there is no
  `EnableCuda` flag and no Full/Lite build split. Gemma 4's `llama-server` can still be a CUDA
  build, downloaded at runtime.
- `RuntimePreference` is `Auto` (Vulkan, then CPU; default) / `Vulkan` (strict: throws
  `RuntimeUnavailableException` instead of falling back) / `Cpu`. Runtime selection is
  process-global and one-shot ([ADR-048](docs/decisions/048-whisper-runtime-latch-and-factory-lifetime.md)), so a change needs an app restart.
- `Directory.Build.targets` strips unused ONNX Runtime GPU providers, other platforms'
  Whisper natives and native PDBs from the output (ADR-050/051/052). Keep those filters.

## Architecture

Eight projects in `Parlotype.slnx`. Dependencies point one way:
`Desktop → Platform → Core`, `Benchmark → Platform → Core`, `MicroBenchmarks → Platform → Core`;
test projects reference what they test.

- **Core**: contracts and pure domain logic. No platform packages, ever.
- **Platform**: native libraries, P/Invoke and managed external processes (`llama-server`),
  behind Core interfaces. Platform's DI registrations are in `PlatformServiceExtensions.cs`.
- **Desktop**: the Avalonia tray app and entry point. It registers its own services and view
  models in `App.axaml.cs`. Services are singletons in both.

Read the relevant part of [`docs/architecture/`](docs/architecture/) before changing a
subsystem; `overview.md` says which document covers what.

## Rules that are easy to get wrong

- **Paths.** Every path the app writes comes from `IAppPaths` (`AppPaths.Default`). Never
  compose one by hand. The Windows data root is `%LOCALAPPDATA%/parlotype-data`, not
  `parlotype`: that folder belongs to the Velopack installer and is deleted on uninstall
  ([ADR-053](docs/decisions/053-velopack-packaging-and-auto-update.md)).
- **Network.** In local mode, apart from downloads the user starts (models, `llama-server`),
  the only outbound request is the update check (`IUpdateService`, opt-out via
  `SettingsKeys.UpdatesCheckAutomatically`). A new one needs an ADR.
- **Logs.** Recognized text is never logged at any level; log lengths and counts only
  ([ADR-046](docs/decisions/046-security-hardening-batch.md)).
- **Models.** Never load two Whisper models at once; switching is unload → load
  ([ADR-017](docs/decisions/017-whisper-model-hot-swap.md)).
- **MVVM.** CommunityToolkit.Mvvm source generators: `[ObservableProperty]` on private
  fields, `[RelayCommand]` on methods, view models are `partial`.
- **AXAML.** Files are `.axaml`. A view that binds to a view model sets
  `x:CompileBindings="True"` and `x:DataType`; never `{ReflectionBinding}`. Visual states use `Classes.name="{Binding Prop}"`. Flyouts are
  outside the visual tree, so give their items their own commands (the `*DisplayItem` types
  in `ViewModels/`) instead of binding up through `$parent`.
- **Threads.** Mutate `ObservableCollection`s and other UI state on
  `Dispatcher.UIThread`.
- **Tests.** Write tests for Core and Platform logic. `Parlotype.Desktop.Tests` is on xUnit
  v3 (`Avalonia.Headless.XUnit` 12 requires it; the others stay on v2): use `[AvaloniaFact]`
  for anything touching the dispatcher or the test hangs, take mocks from its `Mocks/`
  folder, and pass `TestContext.Current.CancellationToken` where an API takes a token
  (analyzer xUnit1051).
- **Benchmark output** goes through Spectre.Console, never `Console.WriteLine`.

## Localization

The UI ships in the languages listed in `SupportedUiLanguages.All` (Core); read it, never
hardcode a language list. Load the `localization` skill (`.claude/skills/localization/SKILL.md`)
whenever you touch UI copy. The hard rules:

- **A change to user-facing text updates every language in the same change.** A locale left
  behind is a broken build, not a follow-up.
- Copy lives in `src/Parlotype.Desktop/Resources/Strings.resx` (the neutral file is English).
  **Never hand-edit a `Strings.<culture>.resx`**: translations go through
  `scripts/export-translation-brief.ps1` and `scripts/import-translations.ps1`.
- AXAML never holds literal copy: `Text="{loc:Tr Key}"`. In C#, use `Strings.Key` and the
  generated `Strings.Format_Key(...)` helpers. `Strings.cs` is generated by
  `pwsh scripts/gen-strings.ps1`; never edit it.
- Copy composed in C# does not follow a live language switch. The owning settings view model
  overrides `OnCultureChanged()` and keeps the input, not the finished string.
- `pwsh scripts/check-localization.ps1` must pass; `LocalizationParityTests` enforces the
  same rules in `dotnet test`. Log and exception messages stay English.

## Where knowledge lives

| What | Where | Rule |
|---|---|---|
| Decisions | `docs/decisions/` | ADR from `_template.md`. Body immutable once accepted; frontmatter (status, relations) kept current. |
| How the system fits together, and why | `docs/architecture/` | Living. Fix the statement you made false. |
| Gotchas not derivable from code | `docs/knowledge/` | One fact per note; frontmatter `title`, `area`, `summary`, `verified`. Check it before debugging a dependency. |
| Research that fed a decision | `docs/research/YYYY-MM-DD-topic/` | Dated snapshot, not updated later. |
| Translator material | `docs/localization/` | Glossaries, brief, terminology, outstanding items. |
| Work in flight | `plans/` | One folder per live plan, never a bare `.md` in `plans/`; deleted on completion. See [`plans/WORKFLOW.md`](plans/WORKFLOW.md). |

There is no session journal. Git history and the PR description are the record of what was
done; unfinished work belongs in a plan. `pwsh scripts/gen-docs-index.ps1` regenerates
`docs/decisions/README.md` and `docs/knowledge/README.md`; never edit those by hand.

## Definition of Done

A non-trivial change is not done until all of these hold:

1. **Build and tests.** `dotnet build Parlotype.slnx` has zero warnings and `dotnet test` passes.
2. **Behaviour verified.** The original symptom is reproduced and shown gone, or the feature
   is exercised end to end (manual run, log line, test).
3. **ADR written** when any of these fire:
   - a new interface, record or enum in `Parlotype.Core`
   - a new registration in `PlatformServiceExtensions.cs`
   - a new dependency in any `.csproj`
   - behaviour that intentionally differs by OS, build flag or runtime
   - a new external process, native library or P/Invoke call
   - a change to the audio pipeline, hotkey, settings or Whisper subsystems
4. **Docs kept true.** If the change makes a statement in `docs/architecture/`, `AGENTS.md`,
   a skill or a knowledge note false, correct or delete that statement in the same change.
   Correct it; do not append a new paragraph beside the old one. Do not add per-class
   inventories.
5. **Knowledge captured.** A fact learned along the way that the code does not show (a
   third-party quirk, an environment gotcha) gets a `docs/knowledge/` note.
6. **Indexes regenerated.** After adding or changing an ADR or a knowledge note, run
   `pwsh scripts/gen-docs-index.ps1`; `-Check` must pass.
7. **Translations updated.** Every `Strings.<culture>.resx` carries the copy change, via the
   pipeline, and `pwsh scripts/check-localization.ps1` passes. Never deferrable.
8. **Ask before deferring.** If you want to leave any of 3 to 6 for a follow-up, ask the
   owner first; do not decide it alone.
