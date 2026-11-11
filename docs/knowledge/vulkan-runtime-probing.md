---
title: "Vulkan Runtime Probing on Windows"
area: "speech-engines"
summary: "Vulkan packs minor at bit 12 and major at bit 22, Whisper.net's LoadedLibrary stays null until a factory is built, and users need only the driver's loader."
verified: 2026-11-11
---

# Vulkan Runtime Probing on Windows

Facts learned while implementing [ADR-022](../decisions/022-vulkan-gpu-acceleration.md)'s `WindowsVulkanEnvironmentProvider`. None are derivable from current Parlotype code — they live in upstream Vulkan / Whisper.net headers.

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

- Setting `RuntimeLibraryOrder` is process-global one-shot ([ADR-048](../decisions/048-whisper-runtime-latch-and-factory-lifetime.md); originally ADR-012, superseded) — we double down on this in `WhisperRuntimeBootstrap` with first-call-wins semantics.
- For strict-mode verification we use a **post-load assertion**: after `WhisperFactory.FromPath` succeeds, compare `LoadedLibrary` against the user's `RuntimePreference`. Mismatch ⇒ throw `RuntimeUnavailableException`. See `WhisperSpeechRecognizer.AssertLoadedRuntimeMatches`.
- Pre-load checks (env-provider probes) are still needed because the factory itself may throw obscure native errors when the requested runtime can't load — wrapping those in `RuntimeUnavailableException` gives the user an actionable message.

**Verification**: `Whisper.net.LibraryLoader.RuntimeOptions` and `NativeLibraryLoader.LoadNativeLibrary` in the decompiled NuGet assembly (or upstream `github.com/sandrohanea/whisper.net` at the matching tag).

## 4. Shipping Vulkan needs the loader only — the SDK is a build-time tool

The LunarG **Vulkan SDK is a development tool**. A runtime application needs only
the **Vulkan loader**, `vulkan-1.dll`, which ships as part of the GPU driver on
any machine with a Vulkan-capable card. Nothing has to be installed, detected or
redistributed for `Whisper.net.Runtime.Vulkan` to work on a user's machine.

This is the opposite of the CUDA situation and is easy to assume symmetric:
`Whisper.net.Runtime.Cuda` bundled no cudart/cublas, so a CUDA build still
required the **user** to have the CUDA toolkit. That asymmetry is a large part of why Vulkan
became the single shipped GPU runtime in [ADR-049](../decisions/049-drop-whisper-cuda-runtime.md): one ~30 MB
package that works on AMD, Intel and NVIDIA with no user-side prerequisite.
