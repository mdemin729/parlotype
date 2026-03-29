---
title: Parlotype.Tests
type: service-profile
status: active
tags: [tests, xunit, core, platform]
criticality: medium
last_updated: 2026-03-28
summary: xUnit tests for Core and Platform — audio pipeline, VAD, Whisper integration
---

# Parlotype.Tests

## Purpose
Unit and integration tests for Core contracts and Platform implementations.

## Key Path
`src/Parlotype.Tests/`

## Run
```bash
dotnet test src/Parlotype.Tests
dotnet test src/Parlotype.Tests -p:EnableCuda=false   # CPU-only
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"
```

## Dependencies
- [[core]], [[platform]]
- xUnit, FluentAssertions
