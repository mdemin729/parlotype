---
status: accepted
date: 2026-04-30
---

# 016. Avalonia 12 Developer Tools (V2)

## Context

`Parlotype.Desktop.V2` runs on Avalonia 12. The classic free
`Avalonia.Diagnostics` package — the F12 visual-tree inspector that V1 (Avalonia 11)
relies on — has been **retired in Avalonia 12**. Without an inspector, debugging
visual-tree, layout, binding, and styling issues in V2 is reduced to guesswork
and rebuild loops, which slows UI iteration significantly.

AvaloniaUI's replacement is split across two artifacts:

- `AvaloniaUI.DiagnosticsSupport` — an in-app NuGet library that exposes
  `Application.AttachDeveloperTools()` and listens for inbound connections.
- `AvaloniaUI.DeveloperTools` (`avdt`) — a standalone .NET global tool installed
  per-developer that hosts the inspector UI and connects to the running app
  on F12.

The Essentials edition (visual tree, property editor, layout, styles) is
covered by AvaloniaUI's **community licence**, free for organisations under
€1M revenue. Parlotype clearly qualifies. The Complete edition (3D view,
profiling, mobile, remote, MCP) is paid and not in scope. Use of either
edition requires each developer to register a free **AvaloniaUI Portal**
account for one-time tool activation.

A community port of the classic inspector to Avalonia 12 was advertised online
as `ClassicDiagnostics.Avalonia` but is not actually published on NuGet.org,
so it is not a viable alternative.

## Decision

Add `AvaloniaUI.DiagnosticsSupport` 2.2.1 to `Parlotype.Desktop.V2` with a
**Debug-only** `<PackageReference>` (`Condition="'$(Configuration)' == 'Debug'"`),
and call `this.AttachDeveloperTools()` from `App.Initialize()` inside
`#if DEBUG` / `#endif`. The standalone `avdt` tool is installed per-developer
via `dotnet tool install --global AvaloniaUI.DeveloperTools` and documented in
`README.md`.

Scope is **V2 only**. V1 (`Parlotype.Desktop`) stays on Avalonia 11 with the
classic `Avalonia.Diagnostics` until V1 is retired (separate decision).

## Consequences

**Easier:**

- V2 visual-tree, property, and style debugging via F12 — feature-parity (for
  our needs) with V1's classic DevTools.
- Release builds are unaffected: the conditional `<PackageReference>` excludes
  both `AvaloniaUI.DiagnosticsSupport` and its transitive
  `Microsoft.IO.RecyclableMemoryStream` dependency from Release output. The
  `#if DEBUG` guard keeps the call out of Release IL.

**Harder / new constraints:**

- Each contributor must install the `avdt` global tool and register a free
  AvaloniaUI Portal account before F12 works locally.
- Future inspector functionality beyond Essentials would require a paid
  upgrade or a different tool — this decision does not commit to that.

**Notes / non-goals:**

- A pre-existing build-time message — `"Avalonia Accelerate Community requires
  telemetry. To opt out, please upgrade to a paid tier."` — is emitted by the
  base Avalonia 12 SDK targets, **not** by the change introduced here. It
  appears in V2 builds even with `DiagnosticsSupport` removed, and is unrelated
  to the in-app DevTools wiring. Investigating and (if desired) suppressing
  that message is tracked separately, not by this ADR.

## References

- Package: <https://www.nuget.org/packages/AvaloniaUI.DiagnosticsSupport>
- Tool: <https://www.nuget.org/packages/AvaloniaUI.DeveloperTools>
- Editions / pricing: <https://avaloniaui.net/devtools/>
- Setup guide: <https://docs.avaloniaui.net/tools/developer-tools/installation>
- Related: ADR 015 (Parlotype.Desktop.V2 / Avalonia 12)
