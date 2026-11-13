# llama.cpp Subsystem Architecture

> **Scope:** This document describes the managed `llama-server` subsystem
> and its two consumers: `LlamaCppSpeechRecognizer` (Gemma 4
> transcription) and `LlamaServerSpeechCleanupService` (speech cleanup
> with S1-mini, ADR-073). They share the installed binary and the
> active-install selection. Each runs its **own** server process, because
> a `llama-server` hosts one model and they need different ones.
>
> The server-side components (catalog, installer, registry, lifecycle,
> settings) live under `Parlotype.Core.LlamaServer` /
> `Parlotype.Platform.LlamaServer` and are deliberately
> workload-agnostic — they were moved out of `Speech.*` in ADR-027 so a
> second consumer could plug in cleanly. The speech consumer stays in
> `Parlotype.Platform.Speech`; the cleanup consumer lives in
> `Parlotype.Platform.TextProcessing`.
>
> Sections 2 to 11 describe the **speech** consumer: engine selection, the
> lifecycle of its spawned/adopted server process, its HTTP request
> contract, configuration surface, and failure modes. Section 12 covers
> server-installation lifecycle (ADR-026) and section 13 the speech
> cleanup consumer.

See also: [ADR-025](../decisions/025-gemma4-llamacpp-desktop.md),
[ADR-026](../decisions/026-managed-llama-server-install.md),
[ADR-027](../decisions/027-llamaserver-namespace-rescope.md),
[ADR-073](../decisions/073-speech-cleanup-s1-mini.md),
[Audio pipeline](./audio-pipeline.md),
[`llama-cpp-release-assets`](../knowledge/llama-cpp-release-assets.md).

---

## 1 Component Overview

### Server-side (workload-agnostic)

These live under `Parlotype.*.LlamaServer` (post-ADR-027) and have no
dependency on speech. Both consumers use them unchanged.

| Component | Project | Responsibility |
|-----------|---------|----------------|
| `ILlamaServerCatalog` / `GitHubLlamaServerCatalog` | Core / Platform | Fetches release groups from GitHub, parses asset names, pairs CUDA-Windows variants with their cudart companion, filters to current OS/arch, caches result + ETag on disk for 1 h. Lists the build upstream's newest versioned release names first, marked with that version (ADR-074). |
| `ILlamaServerRegistry` / `JsonLlamaServerRegistry` | Core / Platform | Reads/writes `manifest.json` (managed installs). Resolves the active install by reading `LlamaCppActiveInstall` from `ISettingsService`. Manual mode returns a synthetic install pointing at `LlamaCppServerFolder`. |
| `ILlamaServerInstaller` / `LlamaServerInstaller` | Core / Platform | Downloads main + companion to a staging dir, SHA256-verifies, extracts, atomically renames into `{root}/{id}/`, updates the manifest. |
| `LlamaCppServerInfo` | Platform | Pure probe helper. Hits `/health` then `/props` to classify a port as `Connected`, `Loading`, `Disconnected`, `PortConflict`, or `Error`. Moved to `Parlotype.Platform.LlamaServer` in ADR-027. |
| `ILlamaCppServerLifecycle` / `CompositeLlamaServerLifecycle` | Core / Platform | Stops running sidecars so the installer can replace their files. Each consumer implements the interface for its own process; DI hands the installer a composite over both (ADR-073). |
| `LlamaServerPathResolver` | Platform | The one rule for which `llama-server.exe` to launch: a managed active install, else the manual folder, else none. Used by both consumers. |
| `LlamaServerProcess` | Platform | One spawned server: start with an argument list, poll `/health`, stop. Used by the cleanup consumer; the Gemma 4 recognizer predates it and keeps its own copy of this logic. |
| `LlamaServerVariantPicker` | Core | Picks a CPU build for features that install a server on the user's behalf: the released build's when the catalog marks one, otherwise the newest. |
| `StreamingFileDownloader` | Platform (`Speech` namespace, shared) | HTTP → temp → atomic-move helper with SHA-256 verification. Used by the Whisper, Parakeet and Gemma 4 model download services and the installer. |
| `LlamaServerInstallDialogService` | Desktop | UI wrapper around `LlamaServerInstaller`; opens the generalized `ModelDownloadDialog` and maps installer phase strings to friendly status text. |
| `SettingsKeys.LlamaCppActiveInstall` / `LlamaCppServerFolder` / `LlamaCppPort` | Core | Persisted configuration for the server itself (workload-neutral). |
| `LlamaCppSettingsView` / `LlamaCppSettingsViewModel` | Desktop | UI for browse/install/uninstall/set-active + manual folder picker + port + probe status. |

### Consumers (workload-specific)

| Component | Project | Responsibility |
|-----------|---------|----------------|
| `DelegatingSpeechRecognizer` | Platform | Registered as the `ISpeechRecognizer` singleton. At `InitializeAsync` time, asks the factory for the concrete recognizer and forwards all calls to it. |
| `SpeechRecognizerFactory` | Platform | Reads `SettingsKeys.SpeechEngine` and resolves one of five recognizers from DI: `ParakeetSpeechRecognizer` (default), `WhisperSpeechRecognizer`, `LlamaCppSpeechRecognizer` (Gemma 4), `OpenAiCompatibleSpeechRecognizer`, `XaiGrokSpeechRecognizer`. |
| `LlamaCppSpeechRecognizer` | Platform (`Speech` namespace) | Owns the `llama-server.exe` sidecar lifecycle as a speech consumer: probes, spawns, health-polls, transcribes via HTTP, and stops the process. Also implements `ILlamaCppServerLifecycle` for the installer. Uses the server-side components via `Parlotype.Core.LlamaServer` + `Parlotype.Platform.LlamaServer`. |
| `Gemma4ModelInfo` | Core (`Speech` namespace) | Catalog of Gemma 4 variants (ADR-029): GGUF + mmproj filenames and SHA-256 digests, HuggingFace repo. Cache directory is `IAppPaths.ModelsDirectory` (`%LOCALAPPDATA%\parlotype-data\models\` on Windows, ADR-053). Stays in `Speech` — Gemma 4 is the speech model. |
| `SpeechEngine` (enum) | Core (`Speech` namespace) | `Whisper` \| `Gemma4` \| `Parakeet` \| `OpenAiCompatible` \| `XaiGrok`; unset or unparsable ⇒ `Parakeet` (ADR-042). |
| `SettingsKeys.SpeechEngine` | Core | Selects the speech engine. |

The second consumer, speech cleanup, is described in
[section 13](#13-speech-cleanup-consumer).

### DI registration

[`PlatformServiceExtensions.cs`](../../src/Parlotype.Platform/PlatformServiceExtensions.cs):

```csharp
// Server-side
services.AddSingleton<ILlamaServerRegistry, JsonLlamaServerRegistry>();
services.AddSingleton<ILlamaServerCatalog, GitHubLlamaServerCatalog>();
services.AddSingleton<LlamaServerInstaller>();
services.AddSingleton<ILlamaServerInstaller>(sp => sp.GetRequiredService<LlamaServerInstaller>());

// Speech consumer
services.AddSingleton<LlamaCppSpeechRecognizer>();
services.AddSingleton<SpeechRecognizerFactory>();
services.AddSingleton<ISpeechRecognizer, DelegatingSpeechRecognizer>();

// Speech cleanup consumer
services.AddSingleton<SpeechCleanupModelDownloadService>();
services.AddSingleton<LlamaServerSpeechCleanupService>();
services.AddSingleton<ISpeechCleanupService>(sp => sp.GetRequiredService<LlamaServerSpeechCleanupService>());

// One lifecycle over both, for the installer
services.AddSingleton<ILlamaCppServerLifecycle>(sp => new CompositeLlamaServerLifecycle(
    sp.GetRequiredService<LlamaCppSpeechRecognizer>(),
    sp.GetRequiredService<LlamaServerSpeechCleanupService>()));
```

[`App.axaml.cs`](../../src/Parlotype.Desktop/App.axaml.cs) overrides the
installer interface with the Desktop dialog wrapper (last-AddSingleton wins):

```csharp
services.AddSingleton<ILlamaServerInstaller, LlamaServerInstallDialogService>();
```

---

## 2 Request Path

```mermaid
flowchart LR
    PIPE["AudioPipelineService<br/>16 kHz mono float[]"]
    DEL["DelegatingSpeechRecognizer<br/>ISpeechRecognizer singleton"]
    FACT["SpeechRecognizerFactory<br/>reads SpeechEngine setting"]
    LC["LlamaCppSpeechRecognizer"]
    WAV["WavEncoder.Encode<br/>float → 16-bit PCM WAV"]
    B64["Convert.ToBase64String"]
    HTTP[/"HTTP POST<br/>/v1/chat/completions"/]
    SRV[("llama-server.exe<br/>sidecar process")]
    TXT["TranscriptionResult"]

    PIPE -- "TranscribeAsync" --> DEL
    DEL -- "resolve once" --> FACT
    FACT -. "on InitializeAsync" .-> DEL
    DEL --> LC
    LC --> WAV --> B64 --> HTTP --> SRV
    SRV -- "choices[0].message.content" --> LC --> TXT --> PIPE
```

The `AudioPipelineService` is unchanged from the Whisper path — it always sees
`ISpeechRecognizer`. The branching is hidden inside
`DelegatingSpeechRecognizer`.

---

## 3 Engine Selection Flow

Engine selection happens **once per `InitializeAsync` call**, not per
transcription:

1. `DelegatingSpeechRecognizer.InitializeAsync` acquires its `_lock`.
2. It calls `SpeechRecognizerFactory.GetRecognizerAsync`.
3. The factory reads `SettingsKeys.SpeechEngine`, parses it as
   `SpeechEngine` (default `Parakeet` on parse failure), and returns the
   matching singleton (`LlamaCppSpeechRecognizer` for `Gemma4`).
4. The delegating wrapper caches the chosen `_inner` and forwards
   `InitializeAsync`, `TranscribeAsync`, `UnloadAsync`, and `DisposeAsync`
   to it.

A second `InitializeAsync` call **re-resolves** from settings, so a runtime
engine switch becomes effective after an explicit
`UnloadAsync → InitializeAsync` cycle. The `InitializeAsync(WhisperOptions)`
overload still calls the same factory; `WhisperOptions` is a no-op on the
Llama path (it is not surfaced to `LlamaCppSpeechRecognizer.InitializeAsync`).

---

## 4 `llama-server` Lifecycle

The heart of this integration is the lifecycle of the sidecar process
itself. The state machine below is the one `LlamaCppSpeechRecognizer`
runs for the Gemma 4 server it owns (probes / spawns / kills). The
speech cleanup consumer owns a separate process with a simpler
lifecycle, with no probing and no adoption; see
[section 13](#13-speech-cleanup-consumer).

```mermaid
stateDiagram-v2
    [*] --> Uninitialized

    Uninitialized --> Probing : "InitializeAsync()<br/>LlamaCppServerInfo.ProbeAsync"

    Probing --> Adopted : "probe = Connected<br/>(/health 200 + /props 200)"
    Probing --> WaitingForLoad : "probe = Loading<br/>(/health 503)"
    Probing --> Spawning : "probe = Disconnected<br/>(port free)"
    Probing --> Failed_PortConflict : "probe = PortConflict<br/>(/health 200 but /props ≠ 200)"

    Spawning --> WaitingForLoad : "Process.Start OK<br/>health poll begins"
    Spawning --> Failed_BinaryMissing : "no server configured, or llama-server.exe not found"
    Spawning --> Failed_ModelMissing : "GGUF or mmproj missing"

    WaitingForLoad --> Ready : "/health = 200"
    WaitingForLoad --> Failed_ExitedEarly : "process.HasExited"
    WaitingForLoad --> Failed_Timeout : "120 s elapsed"

    Adopted --> Ready

    Ready --> Transcribing : "TranscribeAsync()<br/>POST /v1/chat/completions"
    Transcribing --> Ready : "HTTP 2xx"
    Transcribing --> Ready : "HTTP non-2xx<br/>(throws, state preserved)"

    Ready --> Stopped : "UnloadAsync() / DisposeAsync()<br/>StopServerAsync"
    note right of Stopped
        Only **spawned** processes are killed.
        Adopted servers are left running
        because Parlotype did not start them.
    end note

    Failed_PortConflict --> [*]
    Failed_BinaryMissing --> [*]
    Failed_ModelMissing --> [*]
    Failed_ExitedEarly --> [*]
    Failed_Timeout --> [*]
    Stopped --> [*]
```

### Transition reference

| From → To | Driver | Notes |
|-----------|--------|-------|
| `Uninitialized → Probing` | `InitializeAsync` under `_initLock` | Double-checks `IsReady` after lock. |
| `Probing → Adopted` | `LlamaCppServerInfo.ProbeAsync` returns `Connected` | Logs model alias from `/props`. No process is spawned or owned. |
| `Probing → WaitingForLoad` | Probe returns `Loading` (`/health` = 503) | Same wait loop as a freshly spawned server. |
| `Probing → Failed_PortConflict` | `/health` is 200 but `/props` is not 200 | Surfaces an `InvalidOperationException` telling the user to change the port in Settings → llama.cpp. |
| `Probing → Spawning` | Probe = `Disconnected` (no listener) | Resolves server path and model paths first. |
| `Spawning → Failed_*` | File checks fail before `Process.Start` | `llama-server.exe`, GGUF, or mmproj missing. |
| `Spawning → WaitingForLoad` | `Process.Start` succeeds, `DrainStreamAsync` tasks started | stdout/stderr are read into `_logger` at `Debug` level to prevent pipe deadlocks. |
| `WaitingForLoad → Ready` | `WaitForServerReadyAsync` sees `/health` 200 | Exponential backoff: 500 ms × 1.5, capped at 5 s. |
| `WaitingForLoad → Failed_ExitedEarly` | `_serverProcess.HasExited` while polling | Includes exit code in the thrown `InvalidOperationException`. |
| `WaitingForLoad → Failed_Timeout` | `StartupTimeoutSeconds = 120` reached | Throws `TimeoutException`. |
| `Ready → Transcribing → Ready` | `TranscribeAsync` | Per request. State does not change on transcription failure; the exception propagates to the caller. |
| `Ready → Stopped` | `UnloadAsync` or `DisposeAsync` → `StopServerAsync` | `Kill(entireProcessTree: true)` + `WaitForExitAsync` with a 5 s `CancellationTokenSource`. `_serverProcess` is null for adopted servers, so this is a no-op for them. |

---

## 5 Process Management

**Spawn command** (constructed in `LlamaCppSpeechRecognizer.InitializeAsync`):

```text
llama-server.exe
  -m "<GGUF>"
  --mmproj "<mmproj>"
  --host 127.0.0.1
  --port <configured>
  -ngl 99
  --flash-attn on
  --jinja
  -c 24576
```

`--flash-attn on` and `--jinja` are required for audio support; `--mmproj`
is mandatory or the server returns "audio input is not supported".
GGUF file names on HuggingFace are case-sensitive, and the projector is
`bf16`, not `f16` (`mmproj-gemma-4-E4B-it-bf16.gguf`); the exact names live
in `Gemma4ModelInfo`.

**Process options:** `UseShellExecute = false`, `CreateNoWindow = true`,
stdout and stderr redirected and continuously drained by
`DrainStreamAsync` to prevent the child process from blocking on a full
pipe.

**Health polling** (`WaitForServerReadyAsync`):

- Calls `GET /health` against the shared `HttpClient` (5-minute request
  timeout, base address `http://127.0.0.1:<port>`).
- Delay starts at **500 ms** and multiplies by **1.5** up to a **5 s** cap.
- Aborts early if `_serverProcess.HasExited`.
- Overall timeout: **120 s** (`StartupTimeoutSeconds`).

**Stop semantics** (`StopServerAsync`):

- No-op if `_serverProcess` is null (adopted server) or already exited.
- `Kill(entireProcessTree: true)` followed by `WaitForExitAsync` with a
  5 s `CancellationTokenSource`. Exceptions are logged at `Warning`, not
  rethrown.

---

## 6 HTTP Contract

**Endpoint:** `POST /v1/chat/completions` (OpenAI-compatible, served by
llama-server).

**Request body:**

```json
{
  "model": "gemma-4-e4b",
  "stream": false,
  "temperature": 1.0,
  "top_p": 0.95,
  "top_k": 64,
  "messages": [
    {
      "role": "user",
      "content": [
        { "type": "text",
          "text": "<prompt body chosen by BuildPromptTextAsync>" },
        { "type": "input_audio",
          "input_audio": { "data": "<base64 WAV>", "format": "wav" } }
      ]
    }
  ]
}
```

The text part is the active prompt template, resolved per request by
`BuildPromptTextAsync` from the source/target language matrix with
`{speech_lang}` / `{text_lang}` substituted (ADR-030, ADR-037).

The audio payload is produced by `WavEncoder.Encode` (a shared internal
helper, also used by the OpenAI-compatible recognizer; rewritten in ADR-045)
— a 16-bit, mono, 16 kHz PCM WAV with a 44-byte RIFF/fmt/data header. Float samples are clamped to
`[-1, 1]` and scaled to `short` before writing.

**Response parsing:** the result text is read from
`choices[0].message.content` and trimmed. Any non-2xx status raises
`InvalidOperationException` including the response body. The shared
`HttpClient` uses a 5-minute timeout (`TimeSpan.FromMinutes(5)`).

---

## 7 Adopt-vs-Spawn Semantics

`LlamaCppServerInfo.ProbeAsync` exists specifically to distinguish
llama-server from other listeners on the same port:

1. **`GET /health`** — present on most HTTP servers. A non-success
   response is mapped to `Loading` (503) or `Error`.
2. **`GET /props`** — llama-server-specific. If `/health` is 200 but
   `/props` is not, the port is treated as a `PortConflict` and the
   user is told to change the port in Settings.
3. On `Connected`, the response is parsed for `model_alias`,
   `model_path`, `build_info`, and `modalities.audio`. The recognizer
   logs the model alias and adopts the process.

Adoption means **Parlotype does not own the process** — `_serverProcess`
remains null and `StopServerAsync` will leave the external server
running. This allows developers to run a long-lived llama-server in a
terminal across multiple app launches.

---

## 8 Configuration Surface

| Setting key | Default | Description |
|-------------|---------|-------------|
| `SettingsKeys.SpeechEngine` | (unset ⇒ `Parakeet`) | Selects engine. Set to `"Gemma4"` to route through `LlamaCppSpeechRecognizer`. |
| `SettingsKeys.LlamaCppActiveInstall` | (empty) | `managed:{id}` selects a managed install (resolved via `ILlamaServerRegistry`); `manual` uses `LlamaCppServerFolder`; empty is treated like `manual`. |
| `SettingsKeys.LlamaCppServerFolder` | (empty) | Folder containing `llama-server.exe`. Used by **manual** mode and whenever no managed install is active. There is no default folder since ADR-066; the user picks one. A folder inside the Velopack pack folder gets a warning (ADR-065). |
| `SettingsKeys.LlamaCppPort` | `8321` (`PreferredPort` constant) | TCP port for the sidecar. Parsed as int; out-of-range values silently fall back to the default. |
| Managed-install root (not a setting) | `%LOCALAPPDATA%\parlotype-data\llama-servers\` | `IAppPaths.LlamaServerInstallsDirectory`, via `JsonLlamaServerRegistry.DefaultRootDirectory()`. Holds `manifest.json`, `.cache/releases.json`, `.staging/{guid}/`, and one folder per managed install (`{build}-{os}-{backend}-{arch}`). |
| Model cache (not a setting) | `%LOCALAPPDATA%\parlotype-data\models\` | `IAppPaths.ModelsDirectory`, via `Gemma4ModelInfo.GetModelCacheDirectory()`. Holds the GGUF + mmproj of the selected variant (`SettingsKeys.SelectedGemma4Model`, ADR-029); the default is E4B Q4_K_M. |

The host is hard-coded to `127.0.0.1` (`DefaultHost`). The model alias
sent in HTTP requests is hard-coded to `"gemma-4-e4b"` and is independent
of `Gemma4ModelInfo.ModelId`.

**Server-path resolution order** (in `LlamaServerPathResolver`, which both consumers call):

1. Read `LlamaCppActiveInstall`. If it starts with `managed:`, ask
   `ILlamaServerRegistry.GetActiveAsync()` and use that install's
   absolute path.
2. Else (selector is `manual`, missing, or a stale managed id whose
   install is no longer in the manifest), read `LlamaCppServerFolder`.
3. If that folder is empty, `GetServerPathAsync` returns null and
   initialization fails with an actionable "No llama-server is configured"
   error. There is no implicit fallback folder (ADR-066).

---

## 9 Failure Modes

| Precondition / event | Detected in | Exception |
|----------------------|-------------|-----------|
| Port in use by non-llama-server process | Probe | `InvalidOperationException` ("Port X is already in use…") |
| No server configured (no managed install, empty manual folder) | `InitializeAsync` post-probe | `InvalidOperationException` ("No llama-server is configured…") |
| `llama-server.exe` not at configured path | `InitializeAsync` post-probe | `InvalidOperationException` ("llama-server not found at…") |
| GGUF missing in model cache | `InitializeAsync` post-probe | `InvalidOperationException` ("Gemma 4 model not found at…") |
| mmproj missing in model cache | `InitializeAsync` post-probe | `InvalidOperationException` ("Gemma 4 mmproj not found at…") |
| `Process.Start` returns null | `InitializeAsync` | `InvalidOperationException` ("Failed to start llama-server process.") |
| Server exits before becoming ready | `WaitForServerReadyAsync` | `InvalidOperationException` with exit code |
| 120 s health-poll timeout | `WaitForServerReadyAsync` | `TimeoutException` |
| Non-2xx from `/v1/chat/completions` | `TranscribeAsync` | `InvalidOperationException` with HTTP status + body |
| `Kill` / `WaitForExitAsync` failure | `StopServerAsync` | **Logged**, not thrown (`Warning` level) |

All errors during initialization leave `IsReady = false`. A failed
transcription does **not** clear `IsReady`; the next request will retry
against the same server.

---

## 10 Threading & Concurrency

- **`_initLock` (`SemaphoreSlim(1, 1)`)** guards `InitializeAsync` and
  `UnloadAsync`. Double-checked `IsReady` on entry.
- **`TranscribeAsync`** is not serialized — it can run concurrently with
  itself against the shared `HttpClient`. llama-server will queue
  requests internally.
- **Race between `UnloadAsync` and in-flight `TranscribeAsync`**: a
  request that is already on the wire when `UnloadAsync` runs will see
  the server torn down and fail with an `HttpRequestException` (or
  `InvalidOperationException` with HTTP error body). The recognizer
  does not cancel outstanding requests during unload.
- **`DrainStreamAsync` background tasks** are fire-and-forget. They end
  naturally when the child process closes stdout/stderr.
- **`HttpClient`** is a single instance with `BaseAddress` set after
  port resolution. It is reused across all requests and disposed in
  `DisposeAsync`.

---

## 11 Observations

These are read-only notes captured while writing this document. They
are intentionally **not** proposals; remediation (if any) belongs in a
follow-up ADR.

- **Adopted-process crash detection is absent.** Once probing returns
  `Connected`, the recognizer does not re-probe; if the adopted server
  dies later, the next `TranscribeAsync` fails with a generic HTTP
  error instead of a clear "server gone" signal.
- **No retry / circuit-breaking** on transient HTTP failures from
  `/v1/chat/completions`. A single 5xx surfaces directly to the user.
- **Model alias `"gemma-4-e4b"` is hard-coded** in `TranscribeAsync` and
  decoupled from `Gemma4ModelInfo.ModelId`. Switching to a different
  Gemma variant would require touching this string.
- **`WhisperOptions` overload is a no-op on the Llama path.** The
  delegating recognizer forwards the call, but
  `LlamaCppSpeechRecognizer` does not implement the overload's
  parameter — beam size, temperature, etc. configured for Whisper do
  not transfer.
- **`UnloadAsync` does not cancel in-flight transcriptions.** A request
  initiated immediately before unload races with the kill and produces
  noisy errors rather than a graceful cancellation.
- **`TranscribeAsync` doesn't re-check `_serverProcess.HasExited`** for
  spawned servers, so a process crash during a long transcription
  surfaces as an HTTP transport failure rather than a process-exit
  exception.
- **Stop is fire-and-forget for adopted servers** — intentional, but
  worth noting that `IsReady` flips to `false` on `UnloadAsync` even
  though the external server is still running and could be re-adopted
  on the next `InitializeAsync`.

---

## 12 Server Installation Lifecycle

Added in ADR-026. Lets users browse, install, and switch between
managed llama-server builds from GitHub, alongside the existing
manual-folder option.

### Components

| Component | Project | Responsibility |
|-----------|---------|----------------|
| `ILlamaServerCatalog` / `GitHubLlamaServerCatalog` | Core / Platform | Fetches `https://api.github.com/repos/ggml-org/llama.cpp/releases` and the release upstream currently names (see [Catalog request semantics](#catalog-request-semantics)), parses asset names via `LlamaServerAssetParser`, pairs CUDA-Windows variants with their matching `cudart-llama-bin-win-cuda-*.zip` companion, filters to the current OS/arch, caches result + ETag on disk for 1 h. |
| `ILlamaServerRegistry` / `JsonLlamaServerRegistry` | Core / Platform | Read/write `manifest.json` (list of managed installs). Resolves the active install by reading `LlamaCppActiveInstall` from `ISettingsService` and looking up the matching entry. Manual mode returns a synthetic install pointing at `LlamaCppServerFolder`. |
| `ILlamaServerInstaller` / `LlamaServerInstaller` | Core / Platform | Downloads main + companion to a staging dir, SHA256-verifies, extracts, atomically renames into `{root}/{id}/`, updates the manifest. Uninstall stops the active sidecar via `ILlamaCppServerLifecycle` before deleting the folder. |
| `StreamingFileDownloader` | Platform | Shared HTTP → temp-file → atomic-rename helper. Reused by the model download services and the installer. |
| `ILlamaCppServerLifecycle` | Core | Implemented by both consumers and composed by `CompositeLlamaServerLifecycle`. Each `StopForReplacementAsync` delegates to that consumer's `UnloadAsync`, so the installer releases every Windows file lock on `llama-server.exe` before deleting it. |
| `LlamaServerInstallDialogService` | Desktop | Implements `ILlamaServerInstaller`; opens the generalized `ModelDownloadDialog` and maps the installer's phase strings (`downloading`, `downloading-companion`, `verifying`, `extracting`, `finalizing`) to friendly status text. Registered in `App.axaml.cs` as an override of the Platform default. |
| `LlamaCppSettingsViewModel` / `LlamaCppSettingsView` | Desktop | Sections: Active server, Update banner, Installed (RadioButton + Uninstall per row), Manual install (distinct background + "Not managed by Parlotype" badge), Available builds, port + Save/Reset. The released build's rows carry upstream's version as a badge, and the banner shows when every installed build is older than the released build; with no released build known it compares against the newest build by tag. |

### Install flow (success path)

```mermaid
flowchart TD
    UI["LlamaCppSettingsView<br/>'Install' button on a catalog row"]
    DLG["LlamaServerInstallDialogService<br/>(Desktop)"]
    INST["LlamaServerInstaller<br/>(Platform)"]
    DL["StreamingFileDownloader"]
    SHA["SHA256.ComputeHashAsync"]
    EXT["ZipFile.ExtractToDirectory"]
    MV[/"Directory.Move<br/>.staging/{guid}/payload → {root}/{id}/"/]
    REG["ILlamaServerRegistry.AddOrUpdateAsync<br/>manifest.json"]

    UI --> DLG --> INST
    INST --> DL
    DL -- "main + companion" --> SHA
    SHA --> EXT
    EXT --> MV --> REG
```

### Storage layout

```
%LOCALAPPDATA%\parlotype-data\llama-servers\
   .staging\{guid}\               # in-progress install (deleted in finally)
      payload\                    # extracted contents; renamed into place on success
      main.zip / companion.zip    # downloaded archives
   .cache\releases.json           # GitHub catalog cache: { version, fetchedAt, etag, releases[], versionedRelease }
   manifest.json                  # source of truth (folder names not load-bearing)
   b9198-win-cuda-12.4-x64\       # managed install — id = build-os-backend-arch
      llama-server.exe
      ggml-cuda.dll
      cudart64_12.dll             # cudart companion zip merged into the same folder
```

### Manifest schema (`manifest.json`)

```jsonc
{
  "version": 1,
  "installs": [
    {
      "id": "b9198-win-cuda-12.4-x64",
      "build": "b9198",
      "backend": "Cuda12",
      "os": "Windows",
      "arch": "X64",
      "assetName": "llama-b9198-bin-win-cuda-12.4-x64.zip",
      "companionAssetName": "cudart-llama-bin-win-cuda-12.4-x64.zip",
      "sha256": "8c79a9b226de4b3ca...",   // null when GitHub had no `digest`
      "companionSha256": "...",
      "installedAt": "2026-05-17T10:23:00Z"
    }
  ]
}
```

Corrupt-read recovery: the registry renames `manifest.json` to
`manifest.json.bak.{timestamp}`, logs a warning, and starts fresh.

### Failure-mode summary

| Phase | Failure | Outcome |
|-------|---------|---------|
| Disk-space precheck | `AvailableFreeSpace < bytes * 3` | `IOException` before any HTTP. |
| Download (main or companion) | Non-2xx / `HttpRequestException` / cancel | Staging dir deleted; manifest untouched. |
| SHA256 verify | digest present + hash mismatch | `InvalidOperationException`; staging dir deleted. |
| Extract | `ZipFile.ExtractToDirectory` throws | Staging dir deleted. |
| Rename (`Directory.Move`) | Target locked | Throws; staging dir kept for the finally cleanup. |
| Manifest write | I/O failure | Logged; on-disk folder may exist without a manifest entry — next `ListManagedAsync` won't show it (manifest is the source of truth). |
| Uninstall while active | Sidecar holds files open | `ILlamaCppServerLifecycle.StopForReplacementAsync` runs first; active selector is cleared before deletion. |

### Catalog request semantics

- **URLs**: a refresh asks `https://api.github.com/repos/ggml-org/llama.cpp/releases?per_page=10`
  for the ten newest builds, then `.../releases/latest` for upstream's newest
  versioned release. A third request, `.../releases/tags/b{N}`, follows only
  when that release names a build that is neither in the list nor cached.
- **Headers**: `User-Agent: parlotype/{version}` (required by GitHub),
  `Accept: application/vnd.github+json`, `If-None-Match: {etag}` when a
  cache exists.
- **Cache TTL**: 1 h. Within TTL, no HTTP is issued unless
  `FetchAsync(forceRefresh: true)` is called (the "Check for updates"
  button).
- **304 handling**: extends the cache `fetchedAt` so subsequent calls
  remain offline. `EntityTagHeaderValue.IsWeak` is preserved on
  round-trip via a small `FormatETag` helper (RFC 7232 requires byte-
  exact echo).
- **Which releases are kept**: drafts are skipped; pre-releases are
  **not**. Upstream flags every `b{N}` build as a pre-release and its
  `v{X.Y.Z}` releases carry no binaries, so filtering on the flag leaves
  nothing to install (see
  [`llama-cpp-release-assets`](../knowledge/llama-cpp-release-assets.md)).
  A release with no asset we can install drops out on its own.
- **Cache version**: the file stores the parsed list, so it carries
  `GitHubLlamaServerCatalog.CacheVersion`. A cache written under another
  version is ignored rather than revalidated; otherwise a 304 would keep
  a list produced by older parsing rules alive indefinitely.
- **The released build** ([ADR-074](../decisions/074-prefer-llama-cpp-release-build.md)):
  a versioned release (`v1.0.0`) has no binaries, only `nightly-tag.txt`
  naming a build. The API returns that file's SHA-256 but not its content,
  so the catalog takes every build tag in the release notes as a candidate
  and accepts the one whose `sha256(tag + "\n")` equals the digest. That
  build is returned first with `LlamaServerReleaseGroup.ReleaseVersion` set,
  followed by the other builds newest first. Its assets are the list's while
  the build is on the first page; after that they are fetched by tag once and
  kept in the cache. `/releases/latest` has its own ETag in the cache.
- **No released build**: when `/releases/latest` fails, has no verifiable
  pointer, the build cannot be fetched or it has no asset for this machine,
  the result is the newest builds with none marked. A failure keeps what was
  known from the last successful refresh. These requests never fail the
  catalog.
- **Failure with cache**: returns stale snapshot, logs a warning, and skips
  the other requests of that refresh.
- **Failure without cache**: rethrows.
- **Filtering**: applied at read time (`Project`) so a single cache
  works across users with different machines and across future phases
  that add macOS/Linux without invalidating Windows users.

## 13 Speech Cleanup Consumer

Added in ADR-073. `LlamaServerSpeechCleanupService` rewrites recognized text
as clean written text with **S1-mini by Superwhisper**, a 462 MB text model.
It is opt-in (`SettingsKeys.SpeechCleanupEnabled`) and runs after any speech
engine; `AudioPipelineService` calls it in the transcription stage (see
[Audio pipeline](./audio-pipeline.md)).

### How it differs from the speech consumer

| | Gemma 4 recognizer | Speech cleanup |
|---|---|---|
| Model | Gemma 4 GGUF + mmproj | `s1-mini-q4_k_m.gguf` |
| Port | `SettingsKeys.LlamaCppPort`, default 8321 | A free loopback port asked from the OS at each launch; not a setting |
| Adopts a running server | Yes, if `/props` answers on the port | Never |
| Started | At recording start, blocking | At recording start, in the background; `CleanAsync` waits for it if needed |
| Stopped | Engine switch, model change, exit | Cleanup switched off, model deleted, exit |
| On failure | The recording does not start | The raw text is pasted |

### Spawn command

```text
llama-server.exe
  -m "<models>/s1-mini-q4_k_m.gguf"
  --host 127.0.0.1
  --port <free port>
  -c 4096
  -np 1
  --jinja
  --chat-template-kwargs {"enable_thinking":false}
  --temp 0
  --no-webui
```

None of the last five is a free choice; the reasons are in
[`s1-mini-quirks`](../knowledge/s1-mini-quirks.md). The server's output is
copied to the app log until `/health` answers and discarded after that,
because from then on requests carry dictated text.

### HTTP contract

`POST /v1/chat/completions` with the published system prompt
(`S1MiniPrompt.SystemPrompt`), a user message that is the control line, a
newline, and the transcript, plus `temperature: 0`, a `max_tokens` cap of
half the transcript's character count plus 64, and
`chat_template_kwargs: {"enable_thinking": false}`. The reply is
`choices[0].message.content`, trimmed; empty is valid. A non-2xx status
throws with the status code only, never the body. Requests time out after
20 s.

### Setup

The Speech cleanup settings page turns the feature on with one switch. It
downloads the model if missing, then makes sure a server is selected: the
active install if its executable exists, else a managed install already on
disk, else the CPU build of llama.cpp's current release, or the newest CPU
build when the catalog marks no release (`LlamaServerVariantPicker`). Selecting an install writes
`LlamaCppActiveInstall`, the same selector the Gemma 4 engine reads. The
llama.cpp server page is shown in the navigation while cleanup is on, as
well as while Gemma 4 is the engine.

---

## 14 Related Documents

- [ADR-025: Gemma 4 via llama.cpp Sidecar in Desktop](../decisions/025-gemma4-llamacpp-desktop.md)
- [ADR-026: Managed llama.cpp Server Installation](../decisions/026-managed-llama-server-install.md)
- [ADR-027: `LlamaServer` Namespace Rescope](../decisions/027-llamaserver-namespace-rescope.md)
- [ADR-029: Gemma 4 model download UI](../decisions/029-gemma4-model-download-ui.md)
- [ADR-030: Configurable Gemma 4 prompts](../decisions/030-configurable-gemma4-prompts.md), [ADR-037: source/target prompts](../decisions/037-gemma4-source-target-prompts.md)
- [ADR-073: speech cleanup with a local model (S1-mini)](../decisions/073-speech-cleanup-s1-mini.md)
- [ADR-065: manual folder pack-folder warning](../decisions/065-manual-llama-server-pack-folder-warning.md), [ADR-066: drop the vestigial llama-server directory](../decisions/066-drop-the-vestigial-llama-server-directory.md)
- [Audio pipeline](./audio-pipeline.md)
- [`llama-cpp-release-assets`](../knowledge/llama-cpp-release-assets.md) — asset naming, cudart pairing, build tagging, rate-limit notes
- [`s1-mini-quirks`](../knowledge/s1-mini-quirks.md) — the cleanup model's input format and the server flags it needs
