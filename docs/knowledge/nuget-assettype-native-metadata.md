---
title: "NuGet stamps %(AssetType) == 'native' on ResolvedFileToPublish items"
area: "packaging"
summary: "Files resolved from a package's runtimes/RID/native/ folder carry %(AssetType)=native at publish; project outputs have none, so filters can key on it."
verified: 2026-11-11
---

# NuGet stamps `%(AssetType) == 'native'` on `ResolvedFileToPublish` items

## The problem it solves

`libSkiaSharp.pdb` and `libHarfBuzzSharp.pdb` (from `SkiaSharp.NativeAssets.Win32` /
`HarfBuzzSharp.NativeAssets.Win32`, pulled in by Avalonia's Skia backend) publish flat at the
`win-x64` output root — same folder as Parlotype's own `Parlotype.Desktop.pdb`,
`Parlotype.Platform.pdb`, `Parlotype.Core.pdb`. A `.pdb` extension filter alone cannot tell
them apart; a filename list (as [ADR-050](../decisions/050-drop-onnx-runtime-gpu-providers.md) used for the ONNX providers)
would work but needs updating for every future native dependency.

## The fix

MSBuild's `ResolvedFileToPublish` items carry `%(AssetType)` metadata populated during NuGet
restore/resolution. Any file resolved from a package's `runtimes/<rid>/native/` folder gets
`AssetType == native`; Parlotype's own project outputs (managed DLLs and PDBs from
`ResolveReferences`) have no `AssetType` at all — the metadata is simply empty, not some
other value. Confirmed by dumping `%(_PdbFiles.AssetType)` for every `.pdb` in a real publish:

```
libHarfBuzzSharp.pdb  → AssetType=native  NuGetPackageId=HarfBuzzSharp.NativeAssets.Win32
libSkiaSharp.pdb      → AssetType=native  NuGetPackageId=SkiaSharp.NativeAssets.Win32
Parlotype.Desktop.pdb → AssetType=        NuGetPackageId=
Parlotype.Core.pdb    → AssetType=        NuGetPackageId=
```

So `Condition="'%(Extension)' == '.pdb' and '%(AssetType)' == 'native'"` removes exactly the
native symbol files, regardless of which native package they came from or what they're
named — see [ADR-052](../decisions/052-drop-native-pdbs-from-publish.md).

## Generalisable lesson

When a `Directory.Build.targets` filter needs to distinguish "comes from a native NuGet
asset" from "is Parlotype's own build output," check `%(AssetType)`/`%(NuGetPackageId)`
metadata on the item before reaching for a filename or path substring match — it's more
robust and needs no maintenance as new native dependencies are added. Discoverable by adding
a throwaway diagnostic `<Message>` target after `ComputeResolvedFilesToPublishList` that
dumps `%(Identity)`, `%(RelativePath)`, `%(NuGetPackageId)`, and `%(AssetType)` for the items
in question.

The same goes for size: a native package's *published* cost is invisible from the
`.csproj`. When an artifact is unexpectedly large, sort the publish output by file size
first. The 391 MB ONNX Runtime CUDA provider ([ADR-050](../decisions/050-drop-onnx-runtime-gpu-providers.md)) came from a
transitive dependency of the VAD library, not from any speech engine.
