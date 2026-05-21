# Gemma 4 model download — Implementation Plan

## Context

Today the codebase pretends to support exactly one Gemma 4 variant via the
hardcoded record [Gemma4ModelInfo.Default](../../src/Parlotype.Core/Speech/Gemma4ModelInfo.cs)
(`E4B Q4_K_M`). [LlamaCppSpeechRecognizer.cs:121](../../src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs:121)
throws `"Download it first in Settings"` if the GGUF/mmproj files are missing —
but **there is no UI in Settings that does the download**, only the existing
[Gemma4ModelDownloadService.cs](../../src/Parlotype.Platform/Speech/Gemma4ModelDownloadService.cs)
that nobody calls from the desktop.

We want to:

1. Offer two Gemma 4 variants — **E2B** and **E4B** (both `Q4_K_M`).
2. Add a Settings section that lists variants, shows installed/not-installed
   status, and downloads on demand with a progress bar.
3. Make `LlamaCppSpeechRecognizer` consume the user-selected variant instead
   of the hardcoded `Default`.

### Decisions (locked via clarifying questions)

- **Downloader:** our own C# code with a progress dialog (not llama-server's
  `-hf` flag — that delegation would skip the progress UI for a 6 GB download).
- **Quantization:** only `Q4_K_M` in v1. Two catalog entries total.
- **Migration:** none. Existing users (if any) re-download via the new UI.

### What we deliberately do *not* build now

- HuggingFace token / gated-repo support (`ggml-org` mirrors are public).
- SHA256 verification — `ggml-org` doesn't publish per-file SHAs and the HF
  blob redirects expose them only at runtime; defer until we hit a real
  corruption bug.
- "Browse any HF repo" UI — curated catalog only. Compatibility-by-version
  with llama-server is too fragile to expose freeform.
- Delegation to llama-server's `-hf` — surveyed in `C:\projects\ggml-org\llama.cpp\common\arg.cpp:2622`;
  the no-progress-bar UX for 6 GB blocks that route in v1. Keep as future
  fallback path.
- Generic "any HF model" catalog abstraction. Three parallel download
  subsystems (Whisper, llama-server, Gemma) already exist — a fourth
  abstraction without a second real consumer would be wrong-shaped.

### Skeptical notes carried forward

- "**Only models supported by llama.cpp server**" (user's words) cannot be
  enforced statically — GGUF format is necessary but not sufficient; specific
  model architectures depend on the installed llama-server version. We rely
  on the curated catalog (we tested these two variants with the supported
  server versions) instead of trying to detect compatibility at runtime.

---

## Catalog model (Parlotype.Core)

Convert [Gemma4ModelInfo.cs](../../src/Parlotype.Core/Speech/Gemma4ModelInfo.cs)
from a record with a single `Default` static into a record + static catalog:

```csharp
public enum Gemma4Variant { E2B, E4B }

public sealed record Gemma4ModelInfo(
    Gemma4Variant Variant,
    string DisplayName,           // "Gemma 4 E2B (Q4_K_M)"
    string GgufFileName,
    string MmprojFileName,
    string DiskSize,              // "~2.5 GiB" / "~6 GiB"
    string HuggingFaceRepo)       // "ggml-org/gemma-4-E2B-it-GGUF" etc.
{
    public static Gemma4ModelInfo E2B { get; } = new(...);
    public static Gemma4ModelInfo E4B { get; } = new(...);

    public static IReadOnlyList<Gemma4ModelInfo> All { get; } = [E2B, E4B];

    public static Gemma4ModelInfo Get(Gemma4Variant v) =>
        All.First(m => m.Variant == v);

    public static string GetModelCacheDirectory() => /* unchanged */;
}
```

Keep `GetModelCacheDirectory()` semantics — it stays a single folder for all
variants; filenames disambiguate.

**New settings key:** `SelectedGemma4Variant` in [SettingsKeys.cs](../../src/Parlotype.Core/Settings/SettingsKeys.cs).
Default value is `Gemma4Variant.E4B` (matches current implicit default).

---

## Downloader (Parlotype.Platform)

Extend [Gemma4ModelDownloadService.cs](../../src/Parlotype.Platform/Speech/Gemma4ModelDownloadService.cs)
so each method takes a `Gemma4ModelInfo` instead of implicitly using `.Default`:

- `bool IsModelCached(Gemma4ModelInfo model)`
- `string GetGgufPath(Gemma4ModelInfo model)`
- `string GetMmprojPath(Gemma4ModelInfo model)`
- `Task DownloadModelAsync(Gemma4ModelInfo model, IProgress<ModelDownloadProgress>?, CancellationToken)`
- `Task DeleteModelAsync(Gemma4ModelInfo model)` — new; needed by the Delete button

`DownloadLock` (the static `SemaphoreSlim`) stays — guards against concurrent
downloads of any variant.

URL pattern stays `https://huggingface.co/{repo}/resolve/main/{file}` per the
existing implementation.

---

## Desktop UI (Parlotype.Desktop)

### New dialog adapter

New `Gemma4ModelDownloadDialogService` mirroring
[ModelDownloadDialogService.cs](../../src/Parlotype.Desktop/Services/ModelDownloadDialogService.cs).
Reuses the existing [ModelDownloadDialog](../../src/Parlotype.Desktop/Views/ModelDownloadDialog.axaml)
and [ModelDownloadViewModel](../../src/Parlotype.Desktop/ViewModels/ModelDownloadViewModel.cs)
verbatim — only the model-name + size + status text differ.

Add a `ForGemma4Model(...)` factory next to the existing
`ForWhisperModel(...)` in [ModelDownloadViewModel.cs](../../src/Parlotype.Desktop/ViewModels/ModelDownloadViewModel.cs).
Mention disk size and mmproj in the prompt: "Download Gemma 4 E4B
(Q4_K_M, ~6 GiB)? Includes vision projector."

### New settings section

`Gemma4ModelSettingsViewModel` (new file under `ViewModels/Settings/`):

- `Title = "Gemma 4 model"`
- `Category = SettingsCategory.SpeechEngine`
- `RestrictToEngine = SpeechEngine.Gemma4`  ← uses the visibility mechanism
  added in ADR-028, so it shows only when Gemma 4 is the active engine
- Constructor takes `ISettingsService`, `Gemma4ModelDownloadService`,
  `Gemma4ModelDownloadDialogService`, optional `TranscribeViewModel`,
  optional `ISpeechRecognizer`
- Properties:
  - `Gemma4ModelDisplayItem[] ModelOptions` (new lightweight wrapper similar
    to `WhisperModelDisplayItem`: model info + `IsInstalled` + Select/Download/Delete commands)
  - `Gemma4Variant SelectedVariant`
- Initialization reads `SettingsKeys.SelectedGemma4Variant`
- `SelectVariant`: same coordination pattern as `WhisperModelSettingsViewModel`
  (stop recording, unload recognizer if `IsReady`, persist)
- `Download`: calls the dialog service for the chosen variant, refreshes
  installed-state
- `Delete`: confirms, then `DeleteModelAsync`, refreshes

`Gemma4ModelSettingsView.axaml`: same visual idiom as the existing
[WhisperModelSettingsView.axaml](../../src/Parlotype.Desktop/Views/Settings/WhisperModelSettingsView.axaml) —
one row per variant with `DisplayName`, `DiskSize`, an installed badge,
selection indicator, and inline Download/Delete buttons.

### DI

Register in [App.axaml.cs:194](../../src/Parlotype.Desktop/App.axaml.cs:194):

```csharp
services.AddSingleton<Gemma4ModelDownloadDialogService>();
services.AddSingleton<Gemma4ModelSettingsViewModel>();
```

Then add the new VM to [SettingsWindowViewModel.cs](../../src/Parlotype.Desktop/ViewModels/SettingsWindowViewModel.cs)
constructor and `_allSections` array. The category projection picks it up
automatically.

The downloader (`Gemma4ModelDownloadService`) needs to be registered as a
singleton in [PlatformServiceExtensions.cs](../../src/Parlotype.Platform/PlatformServiceExtensions.cs)
if it isn't already — check during implementation.

---

## Recognizer (Parlotype.Platform)

[LlamaCppSpeechRecognizer.cs](../../src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs)
lines 114–125 currently hardcode `Gemma4ModelInfo.Default`. Replace with:

1. Read `SettingsKeys.SelectedGemma4Variant` from `ISettingsService`
   (already injected — verify).
2. Resolve via `Gemma4ModelInfo.Get(variant)`.
3. Use that record's filenames in the existing `File.Exists` checks and
   `-m`/`--mmproj` server args.
4. Keep the "Download it first in Settings" exception text — it's now
   actually actionable (the new section exists and is auto-visible when
   Gemma 4 is the active engine).

The model hot-swap pattern from ADR-017 (`UnloadAsync` before reload) is
already triggered from the new viewmodel; no change in the recognizer's
unload semantics.

---

## Tests (Parlotype.Tests + Parlotype.Desktop.Tests)

### Core/Platform

- `Gemma4ModelInfoTests`: `All` contains exactly the two variants in expected
  order; `Get(E2B)` and `Get(E4B)` return matching records; filenames non-empty.
- `Gemma4ModelDownloadServiceTests`: `IsModelCached(E2B)` returns false for
  empty cache; after a stub-file is placed at the expected path, returns
  true. (No live HTTP test — that'd hit HF.)

### Desktop

- `Gemma4ModelSettingsViewModelTests` (xUnit, non-headless):
  - Constructor populates `ModelOptions` with 2 entries.
  - Default `SelectedVariant` is `E4B`.
  - Saved variant in settings is honored on init.
  - Selecting a different variant persists `SelectedGemma4Variant`.
- One headless screenshot test under `Gemma4ModelSettingsScreenshotTests` —
  default state showing both variants, neither installed.

Update [SettingsWindowViewModelTests.cs](../../src/Parlotype.Desktop.Tests/SettingsWindowViewModelTests.cs)
to include "Gemma 4 model" in the Gemma 4-active nav ordering assertion.

---

## Verification

1. `dotnet build Parlotype.slnx -p:EnableCuda=false` clean.
2. `dotnet test Parlotype.slnx -p:EnableCuda=false` all green.
3. Manual:
   - `dotnet run --project src/Parlotype.Desktop`
   - Settings → Speech engine → Engine → Gemma 4
   - Confirm new "Gemma 4 model" row appears under Speech engine
   - Click E2B → Download → progress dialog → wait → installed badge appears
   - Trigger transcription via hotkey → llama-server starts with the E2B file
   - Switch to E4B (already not installed) → Download → repeat
   - Delete → confirm cache file removed and installed badge disappears
4. Confirm `%LOCALAPPDATA%/parlotype/settings.json` contains
   `"SelectedGemma4Variant": "E2B"` after switching.

---

## Definition-of-done bookkeeping

- **ADR-029** under `docs/decisions/` — adds new Core records, new
  `PlatformServiceExtensions` registration, new HF dependency point,
  changes a Whisper-/Gemma-related subsystem. All four triggers fire.
- **Memory vault**: update `memory/services/desktop.md`, `core.md`,
  `platform.md`, and `memory/decisions/_index.md`.
- **Knowledge**: capture the llama.cpp `-hf` flag survey result under
  `memory/knowledge/llama-server-hf-download.md` — non-derivable from our
  repo and informs future "should we delegate?" decisions.
- **Session note** following the standard template.

## Out of scope (deferred)

- HF token UI / gated-repo support.
- SHA256 verification of downloaded files.
- Multi-quantization choice per variant.
- Generic "any HF model" catalog abstraction.
- Migration detection for users with the previously-downloaded E4B file.
- Delegation to llama-server `-hf` as primary path.
- Background pre-download / scheduled download.
