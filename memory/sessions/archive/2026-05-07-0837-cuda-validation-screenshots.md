---
title: "Session: 2026-05-07 — CUDA validation & screenshot tests"
type: session
status: complete
tags: [cuda, runtime, screenshots, testing]
created: 2026-05-07
summary: "Added two-tier CUDA environment validation (no driver vs no SDK) with user-friendly guidance panels, clickable download links, and headless screenshot tests with HTML report generation."
---

# Session: 2026-05-07 — CUDA Validation & Screenshot Tests

## Active Focus

- `src/Parlotype.Desktop/ViewModels/Settings/RuntimeSettingsViewModel.cs` — added `CudaDriverMissing`, `CudaSdkMissing`, `CudaDriverVersion` observable properties; `OpenCudaDownloadLinkCommand` and `OpenVulkanSdkLinkCommand` relay commands
- `src/Parlotype.Desktop/Views/Settings/RuntimeSettingsView.axaml` — added CUDA guidance panels (no-driver, no-SDK with download button), made Vulkan link clickable
- `src/Parlotype.Desktop.Tests/RuntimeSettingsViewModelTests.cs` — 3 new tests for CUDA readiness states
- `src/Parlotype.Desktop.Tests/RuntimeSettingsScreenshotTests.cs` — 4 headless screenshot scenarios (9 screenshots total)
- `src/Parlotype.Desktop.Tests/ScreenshotHelper.cs` — headless window → `CaptureRenderedFrame` → base64 PNG
- `src/Parlotype.Desktop.Tests/ScreenshotReportGenerator.cs` — self-contained HTML report with embedded base64 images
- `src/Parlotype.Desktop.Tests/TestAppBuilder.cs` — enabled `.UseSkia()` + `UseHeadlessDrawing = false` for pixel rendering

## Decisions Made

- Two-tier CUDA check uses `NvidiaEnvironmentInfo.LoadableRuntimes` (P/Invoke probe) as the authoritative signal for "CUDA SDK works", not `InstalledToolkitVersions` (filesystem scan)
- Vulkan SDK is NOT required for Whisper.net Vulkan — only the loader (`vulkan-1.dll`). Current loader-only check kept as-is (confirmed via research)
- HTML chosen over Markdown for screenshot reports — self-contained with base64 images, no external files
- Screenshot window auto-sizes to content height (starts at 1200px, shrinks after layout) to avoid clipping guidance panels

## Facts Learned

- Avalonia 12 headless `CaptureRenderedFrame` requires `.UseSkia()` + `UseHeadlessDrawing = false` in `TestAppBuilder`. The default headless drawing backend produces no pixels (returns null from `CaptureRenderedFrame`)
- Vulkan SDK (LunarG) is a development tool; runtime apps only need the Vulkan loader (`vulkan-1.dll`), which ships with GPU drivers. This differs from CUDA where the toolkit IS required beyond just having drivers
- `IClassFixture<T>` with `IAsyncLifetime.DisposeAsync` is the correct xUnit v3 pattern for collecting data across tests and generating a report after all tests complete (avoids parallel execution timing issues)

## Open Blockers

- None

## Documentation Status

- ADR: none required — no new Core interfaces, no new platform registrations, no new dependencies in production projects
- Vault (services/architecture): none required — changes are UI-layer additions within existing patterns
- Knowledge (non-derivable facts): done — stored memory about Avalonia headless screenshot rendering requirement

## Next Action

- Consider adding screenshot tests for other settings sections (microphone, hotkey, speech, theme) following the same `ScreenshotHelper` + `ScreenshotReportGenerator` pattern
- The `reports/` folder is gitignored; reports are generated on-demand by running the screenshot tests
