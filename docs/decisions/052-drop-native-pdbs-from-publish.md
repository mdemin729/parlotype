---
status: accepted
date: 2026-08-01
---

# 052. Drop Native PDBs from Release Publish Output

## Context

[ADR-050](050-drop-onnx-runtime-gpu-providers.md) flagged it directly: after the CUDA
provider and foreign-RID Whisper natives were filtered out, `libSkiaSharp.pdb` (80 MB) and
`libHarfBuzzSharp.pdb` (20 MB) became the two largest files in a 278 MB self-contained
`win-x64` publish — nearly 100 MB, "left for a separate change."

Both come from Avalonia's Skia rendering backend, transitively through
`SkiaSharp.NativeAssets.Win32` and `HarfBuzzSharp.NativeAssets.Win32`. Their
`runtimes/win-x64/native/` payloads ship the native DLL and its PDB side by side; the SDK
publishes both by default. Nobody debugs into Skia's or HarfBuzz's native code from a
Parlotype crash — these symbols are dead weight in a shipped build.

Enumerating the actual publish output confirmed the full PDB set is small: alongside the two
native PDBs sit `Parlotype.Desktop.pdb`, `Parlotype.Platform.pdb`, and `Parlotype.Core.pdb` —
Parlotype's own managed symbols, small (under 300 KB combined) and worth keeping for crash
symbolication. Both groups land flat at the publish root, so filename or folder position
cannot distinguish them the way ADR-051's RID-directory match could. What does distinguish
them is `%(ResolvedFileToPublish.AssetType)`: NuGet stamps `native` on assets pulled from a
`runtimes/<rid>/native/` package path, and leaves it empty on Parlotype's own project output.

## Decision

Add a third target to the `Directory.Build.targets` introduced by ADR-050/051:
`RemoveNativePdbsFromPublish`, after `ComputeResolvedFilesToPublishList`, filtering
`ResolvedFileToPublish` down to items where `%(Extension) == '.pdb'` and
`%(AssetType) == 'native'`.

1. **Key on `%(AssetType)`, not filename.** It is the same signal NuGet itself uses to mark
   an asset as coming from a `runtimes/<rid>/native/` package path, so it survives a version
   bump or a new native dependency without the target needing a name added to a list — and it
   cannot accidentally match a managed PDB, which carries no `AssetType` at all.
2. **Release only** — `Condition="'$(Configuration)' == 'Release'"`. A `dotnet publish -c
   Debug` keeps every symbol, matching the explicit intent that local debugging should not
   lose native PDBs even from a publish folder. `bin/` dev output is untouched either way;
   this target only runs after `ComputeResolvedFilesToPublishList`, which build never invokes.
3. **No RID gating needed**, unlike ADR-051's target — `AssetType == 'native'` only exists on
   items resolved for the actual publish RID in the first place, so a build with no
   `RuntimeIdentifier` set has no native-tagged PDBs to remove.

## Consequences

- **Easier:** Published `win-x64` Release output drops **278 MB → 180 MB**, a further 35%
  cut. Combined with ADR-049/050/051 the artifact went 731 MB → 180 MB across four changes.
- **Easier:** The filter is asset-classification-driven rather than a filename list, so any
  future native dependency that ships PDBs under `runtimes/<rid>/native/` is caught
  automatically — no maintenance needed when the next native package is added.
- **Note:** Managed PDBs (`Parlotype.Desktop.pdb`, `Parlotype.Platform.pdb`,
  `Parlotype.Core.pdb`) are deliberately kept in Release publish output for crash
  symbolication. They are small enough (under 300 KB combined) that this was never in
  question.
- **Verified:** Published `Parlotype.Desktop.exe` was launched directly from the trimmed output —
  process starts, tray hotkey listener and Vulkan/GPU detection log lines fire, no missing
  native rendering assembly errors — confirming the two removed files were never touched at
  runtime, only referenced for symbolication that nobody uses in a shipped build. A
  `-c Debug` publish was verified separately to still contain both native PDBs, confirming
  the Release-only condition holds.
