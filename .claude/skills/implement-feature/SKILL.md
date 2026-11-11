---
name: implement-feature
description: Use when adding a new feature to Parlotype that spans Core/Platform/Desktop layers. Walks through the contract-first workflow (Core interface → Platform impl → DI registration → Desktop UI → localized copy → tests → ADR/docs).
---

# Implement Feature

The rules themselves (conventions, Definition of Done) are in `AGENTS.md`; this skill is the
order of work.

## Before you start

1. Read the part of `docs/architecture/` that covers the area (`overview.md` maps it).
2. Find the ADRs that constrain it: `docs/decisions/README.md` lists them all with status.
3. Check `docs/knowledge/README.md` for gotchas in the dependencies you will touch.

## Implementation workflow

### 1. Define the contract (Core)
- Add interface(s), records or enums to `src/Parlotype.Core/` in the matching subfolder.
- No platform packages. Pure, testable logic may live here too (e.g. `HotkeyGestureMatcher`).
- Anything the user will read is returned as data (an enum plus arguments); Desktop words it.

### 2. Implement (Platform)
- Add the implementation to `src/Parlotype.Platform/`, mirroring Core's subfolder.
- Register it in `PlatformServiceExtensions.cs` as a singleton.

### 3. Wire the UI (Desktop)
- View model in `src/Parlotype.Desktop/ViewModels/` (settings pages: `ViewModels/Settings/`,
  deriving from `SettingsSectionViewModelBase` and listed in `SettingsWindowViewModel`).
- View in `src/Parlotype.Desktop/Views/` (settings pages: `Views/Settings/`).
- Extract reusable UI into UserControls.

### 4. Localize the copy
- Every user-visible string goes into `Strings.resx` and is bound with `{loc:Tr Key}` (AXAML)
  or read via `Strings.Key` / `Strings.Format_Key(...)` (C#).
- Follow the `localization` skill to translate it into every language in the same change.
- Copy composed in C# must be refreshed in `OnCultureChanged()`.

### 5. Write tests
- Core/Platform: `src/Parlotype.Tests/`.
- UI: `src/Parlotype.Desktop.Tests/` with `[AvaloniaFact]` and the mocks in `Mocks/`.

#### Screenshot tests (for new or changed settings views)
If the feature adds or modifies a settings view, add headless screenshot tests:

1. **Create a fixture** — one `IAsyncLifetime` fixture per test class that collects `Scenario` objects and generates an HTML report on dispose:
   ```csharp
   public sealed class MyScreenshotReportFixture : IAsyncLifetime
   {
       private readonly List<Scenario> _scenarios = [];
       public void AddScenario(Scenario scenario) { lock (_scenarios) _scenarios.Add(scenario); }
       public ValueTask InitializeAsync() => ValueTask.CompletedTask;
       public ValueTask DisposeAsync()
       {
           // Generate to reports/<name>-scenarios.html
           // Use ScreenshotReportFixture.FindRepoRoot() for the repo root
       }
   }
   ```

2. **Create test class** with `IClassFixture<MyScreenshotReportFixture>`:
   - Each `[AvaloniaFact]` method = one scenario (e.g. default state, user interaction, edge case)
   - Build ViewModel with mocks, call `SettleAsync()` (100ms delay + `Dispatcher.UIThread.RunJobs()`)
   - Instantiate a **new view per screenshot** (not reuse) — set `DataContext` via `ScreenshotHelper`
   - Capture with `await ScreenshotHelper.CaptureBase64Async(view, vm)`
   - Collect steps as `ScenarioStep(description, base64Png)` → `_report.AddScenario(...)`

3. **Key helpers** (already in the test project):
   - `ScreenshotHelper.CaptureBase64Async(control, dataContext, width, maxHeight)` — renders in a headless window, auto-shrinks to content height, returns base64 PNG
   - `ScreenshotReportGenerator.Generate(path, title, scenarios)` — self-contained HTML with embedded base64 images
   - `Scenario(name, summary, steps)` / `ScenarioStep(description, base64Png)` — data records

4. **Settle pattern** — always settle before capturing to let bindings propagate:
   ```csharp
   private static async Task SettleAsync()
   {
       await Task.Delay(100);
       Avalonia.Threading.Dispatcher.UIThread.RunJobs();
   }
   ```

5. **Reports** are written to `reports/` (gitignored) and generated on-demand when screenshot tests run.

See existing examples: `RuntimeSettingsScreenshotTests.cs`, `MicrophoneSettingsScreenshotTests.cs`, `HotkeySettingsScreenshotTests.cs`, `SpeechSettingsScreenshotTests.cs`, `ThemeSettingsScreenshotTests.cs`.

### 6. Verify
```bash
dotnet build Parlotype.slnx                 # zero warnings
dotnet test                                 # all tests pass
pwsh scripts/check-localization.ps1         # if copy changed
dotnet run --project src/Parlotype.Desktop  # exercise the feature end to end
```

### 7. Document
Go through the Definition of Done in `AGENTS.md` item by item: ADR triggers, correcting any
statement in `docs/architecture/`, `AGENTS.md`, a skill or a knowledge note that the change
made false, a `docs/knowledge/` note for a non-derivable fact, then
`pwsh scripts/gen-docs-index.ps1`. If you want to defer any of it, ask the owner first.

ADRs go in `docs/decisions/` from `_template.md`. Take the next number from the highest
existing file, not from any "next" hint. Keep relations two-way: if the new ADR amends or
supersedes another, update that ADR's frontmatter too.

## Checklist
- [ ] Contract in Core, implementation in Platform, registered in `PlatformServiceExtensions.cs`
- [ ] View model + view in Desktop (if UI)
- [ ] Copy in resx, every language updated, `check-localization.ps1` passes
- [ ] Tests written and passing; screenshot tests for new or changed settings views
- [ ] Zero build warnings
- [ ] Behaviour verified end to end
- [ ] ADR written if a trigger fires; `gen-docs-index.ps1 -Check` passes
- [ ] No statement in `docs/` or `AGENTS.md` left false by the change
