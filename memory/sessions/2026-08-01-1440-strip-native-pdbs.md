---
title: "Session: 2026-08-01 — Strip native PDBs from publish output"
type: session
status: active
tags: [packaging, msbuild, publish, size]
created: 2026-08-01
summary: Added RemoveNativePdbsFromPublish target (ADR-052) to filter libSkiaSharp.pdb/libHarfBuzzSharp.pdb out of Release publish output — 278 MB to 180 MB
---

# Session: 2026-08-01 — Strip native PDBs from publish output

## Active Focus
`Directory.Build.targets` (repo root) — new `RemoveNativePdbsFromPublish` target, plus
`docs/decisions/052-drop-native-pdbs-from-publish.md`.

## Decisions Made
- Filter native PDBs by `%(AssetType) == 'native'` metadata (NuGet's own classification for
  `runtimes/<rid>/native/` package assets) rather than a filename list — see
  [[../knowledge/nuget-assettype-native-metadata]]. Robust to future native dependencies
  without maintenance.
- Scoped `Condition="'$(Configuration)' == 'Release'"` so `dotnet publish -c Debug` keeps all
  symbols for local troubleshooting; `bin/` dev builds are untouched regardless since the
  target only fires after `ComputeResolvedFilesToPublishList`, which `dotnet build` never runs.
- Left Parlotype's own managed PDBs (`Parlotype.Desktop/Platform/Core.pdb`) untouched — small
  (<300 KB combined) and useful for crash symbolication.

## Facts Learned
- `libSkiaSharp.pdb` (80 MB) and `libHarfBuzzSharp.pdb` (20 MB) come from
  `SkiaSharp.NativeAssets.Win32` / `HarfBuzzSharp.NativeAssets.Win32`, transitively pulled in
  by Avalonia's Skia rendering backend — not something previously documented in the vault.
- Both native and managed PDBs publish flat at the `win-x64` output root (no
  `runtimes/<rid>/native/` subfolder survives into the publish tree), so path-based
  filtering (as ADR-051 used for Whisper RIDs) doesn't apply here — `%(AssetType)` metadata
  does. Full write-up: [[../knowledge/nuget-assettype-native-metadata]].

## Open Blockers
- None.

## Documentation Status
- ADR: done — `docs/decisions/052-drop-native-pdbs-from-publish.md`
- Vault (services/architecture): done — `memory/architecture/dependency-graph.md` Avalonia
  line updated, `memory/decisions/_index.md` row 052 added
- Knowledge (non-derivable facts): done — `memory/knowledge/nuget-assettype-native-metadata.md`
  + index row

## Next Action
None outstanding for this change. Published `win-x64` Release output is now 731 MB → 180 MB
across ADR-049/050/051/052; no further known low-hanging packaging-size targets identified
during this session.
