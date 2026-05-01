---
title: Avalonia 12 Developer Tools
type: knowledge
status: active
tags: [avalonia, avalonia12, devtools, diagnostics, tooling, licensing]
created: 2026-04-30
summary: Avalonia 12 retired classic Avalonia.Diagnostics; the official replacement is split into an in-app library plus a per-developer global tool, with a free community-licence Essentials tier and a paid Complete tier.
---

# Avalonia 12 Developer Tools

## Replacement for the classic F12 inspector

The free `Avalonia.Diagnostics` NuGet package (the classic F12 visual-tree
inspector that shipped with Avalonia 11) was **retired in Avalonia 12**. It has
no 12.x release and will not get one.

The official replacement is a two-part system:

| Artifact | Kind | Where it lives | Who installs |
|----------|------|----------------|--------------|
| `AvaloniaUI.DiagnosticsSupport` | NuGet library | Referenced by the app project | Build / package restore |
| `AvaloniaUI.DeveloperTools` (`avdt`) | .NET global tool | Standalone process on the dev machine | Each developer, once |

The in-app library exposes `Application.AttachDeveloperTools()` and listens for
inbound connections. The standalone tool hosts the inspector UI; the app
connects to it on the F12 gesture.

A community port advertised as `ClassicDiagnostics.Avalonia` is **not actually
published on NuGet.org** as of 2026-04 — do not rely on it.

## Licensing tiers

| Edition | Cost | What you get | Who can use |
|---------|------|--------------|-------------|
| Essentials | Free under "community licence" | Visual / logical tree inspector, property editor, layout debugger, style viewer | Organisations with under €1M revenue |
| Complete | Paid (Avalonia Accelerate) | 3D layout view, performance profiling, advanced logging, asset manager, in-app overlays, mobile + remote, MCP integration | Anyone willing to pay |

First-time activation of either edition requires a **free AvaloniaUI Portal
account** per developer. This is enforced by the standalone tool, not the
in-app library.

## Privacy notes — build-time telemetry (investigated 2026-04-30)

The `Avalonia.BuildServices` NuGet package (v11.3.2, bundled with Avalonia 12)
runs an MSBuild task (`AvaloniaStatsTask`) on **every build** that:

1. Collects: SHA256-hashed project name, TFM, RID, Avalonia version, OS
   description, CPU architecture, detected IDE, detected CI provider, and a
   **DeviceUniqueId** (`SHA256(MachineName-UserName-OSPlatform)`).
2. Writes a binary record to `%LOCALAPPDATA%/AvaloniaUI/BuildServices/`.
3. Spawns a **background process** (`Avalonia.BuildServices.Collector.dll`)
   that HTTP-POSTs the records to
   `https://av-build-tel-api-v1.avaloniaui.net/api/usage`.

### Opt-out behaviour

| Tier | `AVALONIA_TELEMETRY_OPTOUT` env var | Effect |
|------|-------------------------------------|--------|
| Community (free) | Ignored | Telemetry **always runs** |
| Trial | Ignored | Telemetry **always runs** |
| Indie / Business / Enterprise | Honoured | Telemetry **skipped** |

The env var check is in `AvaloniaStatsTask.HasOptedOut()`. Community and Trial
tiers bypass it — the build message is accurate.

### No runtime telemetry

Scan of all `Avalonia*.dll` in the Release output confirms **zero** telemetry
types in the runtime assemblies. This is strictly build-time.

### Current decision

Accepted for now. The data collected is hashed / anonymised and build-only.
**When upgrading to a paid Avalonia tier, set `AVALONIA_TELEMETRY_OPTOUT=1`**
as a machine/CI environment variable to opt out.

## DEBUG-only wiring pattern

To avoid shipping inspector code in Release builds, gate both the package
reference and the call site:

```xml
<!-- csproj -->
<ItemGroup Condition="'$(Configuration)' == 'Debug'">
  <PackageReference Include="AvaloniaUI.DiagnosticsSupport" Version="2.2.1" />
</ItemGroup>
```

```csharp
// App.axaml.cs
public override void Initialize()
{
    AvaloniaXamlLoader.Load(this);
#if DEBUG
    this.AttachDeveloperTools();
#endif
}
```

Verified on 2026-04-30: Release output contains zero `AvaloniaUI.*`,
`*Diagnostics*`, or `Microsoft.IO.RecyclableMemoryStream` DLLs.

## Activation flow for a developer

1. `dotnet tool install --global AvaloniaUI.DeveloperTools`
2. Launch the inspector: `avdt`
3. Run the target app in Debug, focus a window, press **F12**
4. First time only: log in via AvaloniaUI Portal credentials

## See also
- `docs/decisions/016-avalonia12-developer-tools.md`
- `memory/services/desktop-v2.md` → "Diagnostics"
- <https://avaloniaui.net/devtools/>
- <https://docs.avaloniaui.net/tools/developer-tools/installation>
