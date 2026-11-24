---
title: "xunit.v3 4.x does not run under VSTest dotnet test"
area: "testing"
summary: "xunit.v3 4.x pulls Microsoft.Testing.Platform 2.x, which fails the VSTest target on the .NET 10 SDK; Parlotype.Desktop.Tests stays on xunit.v3 3.2.2, the version Avalonia.Headless.XUnit 12.2.0 is built against."
verified: 2026-11-23
---

# xunit.v3 4.x does not run under VSTest `dotnet test`

Found while updating packages
([ADR-077](../decisions/077-dependency-refresh-naudio3-sharphook8-sherpa2.md)).

With `xunit.v3` 4.0.3 in `Parlotype.Desktop.Tests`, the project builds but `dotnet test` fails
before running anything:

```
Microsoft.Testing.Platform.MSBuild.targets(395,5): error : Testing with VSTest target is no
longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later. If you use dotnet
test, you should opt-in to the new dotnet test experience.
```

- The opt-in is solution-wide (`global.json`, `"test": { "runner": "Microsoft.Testing.Platform" }`),
  and the other two test projects are xUnit v2, which runs through VSTest. Moving
  one project means moving all three.
- `Avalonia.Headless.XUnit` 12.2.0 depends on `xunit.v3.extensibility.core` 3.2.2, so 3.2.2 is
  also the version it is tested with.
- `xunit.runner.visualstudio` 4.0.1 works with both xUnit v2 2.9.3 and xunit.v3 3.2.2.

`dotnet list package --outdated` will keep reporting xunit.v3 4.x as available. Leave it until
the solution moves to Microsoft.Testing.Platform as a whole.
