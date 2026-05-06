---
title: Vulkan Runtime Probing on Windows
type: knowledge
tags: [vulkan, whisper, runtime, pinvoke, windows]
created: 2026-05-06
summary: Non-derivable facts about probing the Vulkan loader from .NET on Windows — version packing, VkPhysicalDeviceProperties layout, and Whisper.net's LoadedLibrary semantics.
---

# Vulkan Runtime Probing on Windows

Three facts learned while implementing [[decisions/_index|ADR-022]]'s `WindowsVulkanEnvironmentProvider`. None are derivable from current Parlotype code — they live in upstream Vulkan / Whisper.net headers.

## 1. Vulkan API version packing

`vkEnumerateInstanceVersion` (and the `apiVersion` field of `VkPhysicalDeviceProperties`) returns a packed `uint32_t`:

```
variant : bits 31..29  (3 bits)   — usually 0 for standard Vulkan
major   : bits 28..22  (7 bits)
minor   : bits 21..12  (10 bits)
patch   : bits 11..0   (12 bits)
```

So `VK_MAKE_API_VERSION(0, 1, 3, 0) = (1<<22) | (3<<12) = 0x403000`, **not** `0x402000` — easy off-by-one to hit when writing test fixtures. Decoder lives in `WindowsVulkanEnvironmentProvider.DecodeVulkanVersion`. Spec reference: Vulkan 1.x §3.3 "Versions".

## 2. `VkPhysicalDeviceProperties` is 824 bytes; only the head is stable

The full C struct is 824 bytes and contains nested structs (`VkPhysicalDeviceLimits`, `VkPhysicalDeviceSparseProperties`) that are tedious to model in C#. The **head 276 bytes** are sequential and stable across Vulkan versions:

```
offset  0  uint32 apiVersion
offset  4  uint32 driverVersion
offset  8  uint32 vendorID
offset 12  uint32 deviceID
offset 16  int32  deviceType            (VkPhysicalDeviceType enum)
offset 20  char   deviceName[256]       (UTF-8, null-terminated)
offset 276 ...                          (UUID, limits, sparse props — unused here)
```

Pragmatic interop: allocate one 824-byte buffer with `Marshal.AllocHGlobal(824)`, call `vkGetPhysicalDeviceProperties(device, buffer)`, and read just the head with `Marshal.ReadInt32` + `Marshal.Copy` for the name. Avoids modelling the entire struct. See `WindowsVulkanEnvironmentProvider.ReadDeviceInfo`.

`VkPhysicalDeviceType` raw values: 0=Other, 1=IntegratedGpu, 2=DiscreteGpu, 3=VirtualGpu, 4=Cpu.

## 3. `RuntimeOptions.LoadedLibrary` (Whisper.net) is null until first factory creation

`Whisper.net.LibraryLoader.RuntimeOptions.LoadedLibrary` (a `RuntimeLibrary?`) only becomes non-null **after** the first `WhisperFactory` is created — that's when Whisper.net actually loads native libs in priority order from `RuntimeLibraryOrder`.

Implications:

- Setting `RuntimeLibraryOrder` is process-global one-shot (already documented in [[decisions/_index|ADR-012]]) — we double down on this in `WhisperRuntimeBootstrap` with first-call-wins semantics.
- For strict-mode verification we use a **post-load assertion**: after `WhisperFactory.FromPath` succeeds, compare `LoadedLibrary` against the user's `RuntimePreference`. Mismatch ⇒ throw `RuntimeUnavailableException`. See `WhisperSpeechRecognizer.AssertLoadedRuntimeMatches`.
- Pre-load checks (env-provider probes) are still needed because the factory itself may throw obscure native errors when the requested runtime can't load — wrapping those in `RuntimeUnavailableException` gives the user an actionable message.

**Verification**: `Whisper.net.LibraryLoader.RuntimeOptions` and `NativeLibraryLoader.LoadNativeLibrary` in the decompiled NuGet assembly (or upstream `github.com/sandrohanea/whisper.net` at the matching tag).
