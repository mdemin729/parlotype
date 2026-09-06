# Research — what is actually there today

Survey of the current codebase, 2026-09-05. Counts are from the worktree at commit
`65a97e9`.

## What already exists

ADR-056 (onboarding wizard) shipped the first localization layer:

- `src/Parlotype.Desktop/Resources/Strings.resx` — 30 keys, all `Onboarding_*` or
  `Help_*`.
- `src/Parlotype.Desktop/Resources/Strings.cs` — hand-written accessor over
  `ResourceManager`, reading at `CultureInfo.CurrentUICulture`, falling back to the key
  name on a miss. Its own doc comment states the intent: *"Translations are added later
  as satellite Strings.&lt;culture&gt;.resx files; no markup or code changes needed."*
- `src/Parlotype.Desktop.Tests/StringsTests.cs` — asserts every accessor property
  resolves to real resx content, using "value equals property name" as the signature of
  a missing entry.

So the resource mechanism, the fallback behaviour and the test idiom are all decided
already. This plan extends that decision rather than replacing it.

Three consumers use it: `OnboardingStepFactory`, `OnboardingWizardViewModel`,
`HelpSettingsViewModel`. **No AXAML file references `Strings` at all** — the onboarding
window binds view-model properties instead. There is no markup path yet.

## Scale of the remaining work

| Surface | Count | Notes |
|---|---|---|
| `.axaml` files | 27 | 26 with literal UI text (`HelpSettingsView` is already clean) |
| Literal `Text`/`Content`/`Header`/`Watermark`/`ToolTip.Tip`/`Title` attributes | 211 | worst offenders: `LlamaCppSettingsView` 44, `PromptSettingsView` 28, `CloudProviderSettingsView` 23 |
| View models | 48 | ~200 sentence-shaped string literals |
| Settings section titles | 18 | `public override string Title => "..."` |
| `ShowToast(...)` call sites | 6 | all in `LanguageRelationshipViewModel`, all interpolated |
| Display-name constants in Core | 28 | `WhisperModelInfo`, `ParakeetModelInfo`, `Gemma4ModelInfo`, `WaitTimeOption` |

Estimated total key count after extraction: **~450**.

## Constraints found in the code

**`Strings.cs` is hand-written on purpose.** Its comment says designer generation was
avoided "so the CLI build stays deterministic under warnings-as-errors"
(`TreatWarningsAsErrors=true` in `Directory.Build.props`, repo-wide). At 30 keys that is
free; at 450 it is not. Hence the generator-script-plus-parity-test approach rather than
either extreme.

**Compiled bindings are mandatory.** CLAUDE.md: *"Always use `x:CompileBindings="True"`
and `x:DataType`. Never use `{ReflectionBinding}`."* A localizer-indexer binding is
structurally what that rule forbids, which is why live-vs-restart is called out as a
decision needing an ADR rather than settled silently in a markup extension.

**Flyouts are disconnected from the visual tree.** CLAUDE.md documents the existing
workaround — commands embedded in display-item wrappers (`MicrophoneDisplayItem`,
`WhisperModelDisplayItem`) rather than `$parent` traversal. Any live-refresh mechanism
has to work through those wrappers too, since a flyout's `Localizer` binding lives
outside the window's tree.

**A "restart required" pattern already exists.** `RuntimePreference` is process-global
one-shot (ADR-012 / 022 / 048) and the Settings page surfaces a restart panel when a
change is pending. Reusing it for interface language is cheap but adds a second one, in a
place where users will expect Theme-like immediacy.

## Cultural / formatting risks

`CurrentUICulture` (copy) and `CurrentCulture` (numbers, dates) must be set separately.
Places where `CurrentCulture` already matters:

- `UpdateSettingsViewModel.cs:150` — `checkedAt.ToLocalTime().ToString("f")`, a
  culture-sensitive full date/time shown in Settings. Correct behaviour: follows the OS
  regional setting, **not** the interface language. A user running an English Windows in
  Russian UI should still see their own date format.
- `OnboardingWizardViewModel.cs:57` — already passes `CultureInfo.CurrentCulture`
  explicitly to `string.Format` for the "step N of M" progress line.
- Machine-facing values are already invariant-cultured: `VelopackUpdateService`
  (`ToString("O")`), `WindowsVulkanEnvironmentProvider` and
  `WindowsNvidiaEnvironmentProvider` (`string.Create(CultureInfo.InvariantCulture, ...)`).
  Nothing found that parses a settings value under the current culture, so the decimal
  comma in `ru-RU` and `es-ES` should not corrupt persisted state — worth one explicit
  test in Phase 1 rather than an assumption.

## Interpolated copy needing composite formats

`LanguageRelationshipViewModel` is the concentration point — 30 literals, most assembling
a sentence from state:

```
$"You speak {spoken} → Parlotype types {typed}."
$"Translation paused — \"{WhisperModelDisplayName}\" can't translate"
$"{previous} isn't a source in {engineName}. Using your keyboard layout."
$"{engineName} can't translate — output now matches your spoken language."
$"Previous target reset — not supported by {engineName}."
```

These are the strings where Russian grammar bites: a language name substituted into a
sentence needs a grammatical case the English original does not mark. Some will need
rephrasing into a form that survives substitution (label-and-value, rather than a
sentence with a noun slot) instead of a literal translation. That is a copy decision, and
it belongs in Phase 3d where the English is already in front of us — not deferred to the
translator in Phase 4.

Others of the same shape: `LanguagePickerViewModel.cs:47`
(`$"No languages match \"{Filter.Trim()}\"."`) and the three
`ModelDownloadViewModel` confirmation prompts, which interpolate a model name and a size.

## Verification assets already in place

`Parlotype.Desktop.Tests` has screenshot tests covering most settings sections
(`SpeechEngineScreenshotTests`, `LlamaCppScreenshotTests`, `PromptSettingsScreenshotTests`,
`HotkeySettingsScreenshotTests`, `ThemeSettingsScreenshotTests`, `DataSettingsScreenshotTests`,
`MicrophoneSettingsScreenshotTests`, `RuntimeSettingsScreenshotTests`,
`Gemma4ModelSettingsScreenshotTests`, `CloudProviderScreenshotTests`,
`SpeechSettingsScreenshotTests`, `TranscribeWindowScreenshotTests`) plus
`ScreenshotReportGenerator`.

Rendering those under `ru` and `qps-ploc` is the cheapest possible truncation review, and
it needs no new infrastructure — only a culture set before the headless window is built.
This is the single most valuable existing asset for Phases 4 and 5.
