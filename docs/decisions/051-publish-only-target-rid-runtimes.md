---
status: accepted
date: 2026-07-31
---

# 051. Publish Only the Target RID's Whisper Native Libraries

## Context

A `-r win-x64` publish still carried native Whisper libraries for every desktop platform:

| Folder | Size | Needed on Windows |
|---|---|---|
| `runtimes/vulkan/win-x64` | 46.6 MB | yes |
| `runtimes/win-x64` | 1.6 MB | yes |
| `runtimes/vulkan/linux-x64` | 47.4 MB | no |
| `runtimes/{linux-x64,linux-arm,linux-arm64}` | 5.1 MB | no |
| `runtimes/{macos-x64,macos-arm64}` | 4.3 MB | no |
| `runtimes/{win-x86,win-arm64}` | 2.9 MB | no — wrong architecture |

~60 MB of a 338 MB artifact, 45 of it `libggml-vulkan-whisper.so` — the Linux twin of
`ggml-vulkan-whisper.dll`.

`-r win-x64` cannot filter these. `Whisper.net.Runtime` and `Whisper.net.Runtime.Vulkan`
do not use the `runtimes/<rid>/native/` convention that makes an asset RID-scoped. Their
`.targets` declare plain content instead:

```xml
<None Include="$(MSBuildThisFileDirectory)linux-x64\libggml-vulkan-whisper.so">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  <TargetPath>runtimes/vulkan/linux-x64/libggml-vulkan-whisper.so</TargetPath>
</None>
```

The SDK has no idea the file is platform-specific, so RID filtering never applies. What
gating the packages do have keys on **TargetFramework**, never on RuntimeIdentifier — and
because `net10.0` carries no platform suffix it satisfies
`$(TargetFramework.Contains('-')) == false`, firing the Windows *and* macOS blocks, while
the Linux ones are unconditional. Only the mobile RIDs (iOS/Android/tvOS/Catalyst) are
gated properly.

## Decision

Extend the `Directory.Build.targets` introduced by [ADR-050](050-drop-onnx-runtime-gpu-providers.md)
with a `RemoveForeignRuntimeAssetsFromPublish` target.

1. **Key on the immediate parent directory.** In both layouts the RID is the directory
   directly above the file (`runtimes/win-x64/whisper.dll`,
   `runtimes/vulkan/win-x64/whisper.dll`), so matching on the parent keeps the `vulkan/`
   variant without special-casing it.
2. **Match against a known RID list**, `WhisperRuntimeRidDirectories`, minus the current
   `$(RuntimeIdentifier)` — not "anything that is not our RID". A blanket rule would also
   strip `runtimes/` paths owned by other packages (`runtimes/win/lib/...` and friends);
   an explicit list cannot.
3. **Publish only, and only when a RID is set.** `bin/` output is untouched, so dev builds
   and the test suite behave exactly as before.

## Consequences

- **Easier:** Published `win-x64` output drops **338 MB → 278 MB**. Combined with ADR-049
  and ADR-050 the artifact went 731 MB → 278 MB in one session, a 62% cut.
- **Easier:** The rule is RID-driven, so a future macOS or Linux publish keeps its own
  natives and drops Windows' without further work.
- **Harder:** The RID list is hard-coded. A new platform in a future Whisper.net release
  would be published unfiltered — a size regression, not a break — and a rename of the
  `runtimes/vulkan/` layout would silently stop matching.
- **Verified:** Publishing `Parlotype.Benchmark` with the filter and running a Whisper
  transcription reproduces the dev build exactly (WER 6.4%, RTF 0.023). Hiding
  `runtimes/win-x64` leaves it working (loading from `runtimes/vulkan/win-x64`), and hiding
  `runtimes/vulkan` leaves it working too — both surviving folders are functional, so
  nothing load-bearing was removed.
- **Note:** The benchmark reports `Whisper Runtime: unknown` — `RuntimeOptions.LoadedLibrary`
  is null after a successful model load. Identical before and after this change, so it is
  pre-existing and not caused by the filter, but it means the benchmark cannot confirm
  *which* backend ran, and ADR-048's latch detection may be reading the same null. Tracked
  separately.
