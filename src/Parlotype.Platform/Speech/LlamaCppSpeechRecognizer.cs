using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.LlamaServer;
using Parlotype.Platform.LlamaServer;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Speech recognizer that delegates to a <c>llama-server</c> (llama.cpp) sidecar process.
/// Sends audio as base64-encoded WAV via the OpenAI-compatible
/// <c>/v1/chat/completions</c> endpoint with <c>input_audio</c> content blocks.
/// Also implements <see cref="ILlamaCppServerLifecycle"/> so the installer can
/// stop the sidecar before deleting or replacing its files (Windows file-lock
/// release).
/// </summary>
public sealed class LlamaCppSpeechRecognizer : ISpeechRecognizer, ILlamaCppServerLifecycle
{
    private const int PreferredPort = 8321;
    private const string DefaultHost = "127.0.0.1";
    private const int StartupTimeoutSeconds = 120;

    /// <summary>Google's prescribed prompt for accurate verbatim transcription.</summary>
    private const string TranscriptionPrompt =
        "Transcribe the following speech segment in English into English text. " +
        "Only output the transcription, with no newlines. " +
        "When transcribing numbers, write the digits.";

    private readonly ISettingsService _settings;
    private readonly ILlamaServerRegistry _registry;
    private readonly ILogger<LlamaCppSpeechRecognizer> _logger;
    // Recreated on each Initialize because HttpClient locks BaseAddress after first
    // request, so reusing the same instance across unload/init cycles (e.g. when the
    // user switches the active install) would throw InvalidOperationException.
    private HttpClient? _httpClient;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private Process? _serverProcess;
    private int _activePort;
    private bool _disposed;

    public bool IsReady { get; private set; }

    public LlamaCppSpeechRecognizer(
        ISettingsService settings,
        ILlamaServerRegistry registry,
        ILogger<LlamaCppSpeechRecognizer> logger)
    {
        _settings = settings;
        _registry = registry;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsReady)
            return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (IsReady)
                return;

            // Resolve configured port
            _activePort = await GetConfiguredPortAsync(cancellationToken);

            // Fresh HttpClient per init — see field comment.
            _httpClient?.Dispose();
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri($"http://{DefaultHost}:{_activePort}"),
                Timeout = TimeSpan.FromMinutes(5),
            };

            // Probe the port to check what's there
            var probe = await LlamaCppServerInfo.ProbeAsync(DefaultHost, _activePort, cancellationToken);

            switch (probe.Status)
            {
                case LlamaCppServerStatus.Connected:
                    _logger.LogInformation(
                        "Adopting existing llama-server on port {Port} (model: {Model})",
                        _activePort, probe.ModelAlias);
                    IsReady = true;
                    return;

                case LlamaCppServerStatus.PortConflict:
                    throw new InvalidOperationException(
                        $"Port {_activePort} is already in use by another application (not llama-server). " +
                        "Change the port in Settings → llama.cpp.");

                case LlamaCppServerStatus.Loading:
                    _logger.LogInformation("llama-server on port {Port} is still loading, waiting...", _activePort);
                    await WaitForServerReadyAsync(cancellationToken);
                    IsReady = true;
                    return;
            }

            // Port is free — spawn a new server
            var serverPath = await GetServerPathAsync(cancellationToken);
            if (!File.Exists(serverPath))
                throw new InvalidOperationException(
                    $"llama-server not found at '{serverPath}'. " +
                    "Set the path in Settings → llama.cpp.");

            var modelDir = Gemma4ModelInfo.GetModelCacheDirectory();
            var model = Gemma4ModelInfo.Default;
            var ggufPath = Path.Combine(modelDir, model.GgufFileName);
            var mmprojPath = Path.Combine(modelDir, model.MmprojFileName);

            if (!File.Exists(ggufPath))
                throw new InvalidOperationException(
                    $"Gemma 4 model not found at '{ggufPath}'. Download it first in Settings.");

            if (!File.Exists(mmprojPath))
                throw new InvalidOperationException(
                    $"Gemma 4 mmproj not found at '{mmprojPath}'. Download it first in Settings.");

            _logger.LogInformation("Starting llama-server (model={Gguf}, port={Port})", model.GgufFileName, _activePort);

            var psi = new ProcessStartInfo
            {
                FileName = serverPath,
                Arguments = $"-m \"{ggufPath}\" " +
                            $"--mmproj \"{mmprojPath}\" " +
                            $"--host {DefaultHost} --port {_activePort} " +
                            $"-ngl 99 --flash-attn on --jinja " +
                            $"-c 24576",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            _serverProcess = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start llama-server process.");

            // Drain stdout/stderr to prevent pipe deadlocks
            _ = DrainStreamAsync(_serverProcess.StandardOutput, "stdout");
            _ = DrainStreamAsync(_serverProcess.StandardError, "stderr");

            await WaitForServerReadyAsync(cancellationToken);
            IsReady = true;
            _logger.LogInformation("llama-server is ready (PID {Pid})", _serverProcess.Id);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsReady || _httpClient is null)
            throw new InvalidOperationException("Recognizer is not initialized. Call InitializeAsync first.");

        var wavBytes = EncodeWav(samples.Span, sampleRate: 16_000);
        var base64 = Convert.ToBase64String(wavBytes);

        _logger.LogDebug("Transcribing {SampleCount} samples via llama-server", samples.Length);

        var body = new
        {
            model = "gemma-4-e4b",
            stream = false,
            temperature = 1.0,
            top_p = 0.95,
            top_k = 64,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = TranscriptionPrompt },
                        new { type = "input_audio", input_audio = new { data = base64, format = "wav" } }
                    }
                }
            }
        };

        using var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", body, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"llama-server transcription failed (HTTP {(int)response.StatusCode}): {errorBody}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        return new TranscriptionResult { Text = text.Trim() };
    }

    public async Task UnloadAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            await StopServerAsync();
            _httpClient?.Dispose();
            _httpClient = null;
            IsReady = false;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Stops a running sidecar so the installer can delete or replace its files.
    /// Safe to call when nothing is running. Delegates to <see cref="UnloadAsync"/>
    /// which handles the lock and process-tree kill.
    /// </summary>
    public Task StopForReplacementAsync(CancellationToken cancellationToken = default)
        => UnloadAsync();

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopServerAsync();
        _httpClient?.Dispose();
        _httpClient = null;
        _initLock.Dispose();
    }

    private const string ServerExeName = "llama-server.exe";

    /// <summary>
    /// Resolves the absolute path to <c>llama-server.exe</c>. Consults the
    /// active-install selector first: a managed install wins; manual mode (or
    /// a missing managed selector) falls through to the legacy
    /// <see cref="SettingsKeys.LlamaCppServerFolder"/> setting, which itself
    /// defaults to <c>%LOCALAPPDATA%/parlotype/llama-server</c> when unset.
    /// </summary>
    internal async Task<string> GetServerPathAsync(CancellationToken cancellationToken)
    {
        var active = await _registry.GetActiveAsync(cancellationToken);
        if (active is { Source: LlamaServerSource.Managed })
            return Path.Combine(active.AbsolutePath, ServerExeName);

        var folder = await _settings.GetAsync<string>(SettingsKeys.LlamaCppServerFolder);
        if (!string.IsNullOrWhiteSpace(folder))
            return Path.Combine(folder, ServerExeName);

        return Path.Combine(GetDefaultServerFolder(), ServerExeName);
    }

    private static string GetDefaultServerFolder() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parlotype", "llama-server");

    private async Task<int> GetConfiguredPortAsync(CancellationToken cancellationToken)
    {
        var portStr = await _settings.GetAsync<string>(SettingsKeys.LlamaCppPort);
        return int.TryParse(portStr, out var port) && port is > 0 and <= 65535
            ? port
            : PreferredPort;
    }

    private async Task WaitForServerReadyAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var delay = TimeSpan.FromMilliseconds(500);
        var timeout = TimeSpan.FromSeconds(StartupTimeoutSeconds);

        while (sw.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var proc = _serverProcess;
            if (proc is null || proc.HasExited)
            {
                throw new InvalidOperationException(
                    $"llama-server exited unexpectedly during startup (exit code: {proc?.ExitCode}). " +
                    "Check that the binary is compatible and model files are valid.");
            }

            try
            {
                using var resp = await _httpClient!.GetAsync("/health", cancellationToken);
                if (resp.IsSuccessStatusCode)
                    return;

                _logger.LogDebug("llama-server not ready yet (HTTP {Status}, elapsed: {Elapsed:F1}s)",
                    (int)resp.StatusCode, sw.Elapsed.TotalSeconds);
            }
            catch (HttpRequestException)
            {
                // Server not yet accepting connections
            }

            await Task.Delay(delay, cancellationToken);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 1.5, 5000));
        }

        throw new TimeoutException(
            $"llama-server did not become ready within {StartupTimeoutSeconds} seconds. " +
            "The model may be too large for available memory.");
    }

    private async Task StopServerAsync()
    {
        var proc = _serverProcess;
        if (proc is null || proc.HasExited) return;

        _serverProcess = null;
        var pid = proc.Id;
        _logger.LogInformation("Stopping llama-server (PID {Pid})", pid);

        try
        {
            proc.Kill(entireProcessTree: true);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop llama-server gracefully");
        }

        proc.Dispose();
    }

    private async Task DrainStreamAsync(StreamReader reader, string label)
    {
        try
        {
            var buffer = new char[4096];
            while (true)
            {
                var count = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (count == 0) break;

                var text = new string(buffer, 0, count);
                foreach (var line in text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
                    _logger.LogDebug("[llama-server {Label}] {Line}", label, line.TrimEnd());
            }
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error draining llama-server {Label} stream", label);
        }
    }

    /// <summary>Encodes float PCM samples into a 16-bit mono WAV byte array.</summary>
    internal static byte[] EncodeWav(ReadOnlySpan<float> samples, int sampleRate)
    {
        const int bitsPerSample = 16;
        const int numChannels = 1;
        const int bytesPerSample = bitsPerSample / 8;
        var dataSize = samples.Length * bytesPerSample;

        using var ms = new MemoryStream(44 + dataSize);
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);

        // fmt chunk
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write((short)numChannels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * numChannels * bytesPerSample);
        bw.Write((short)(numChannels * bytesPerSample));
        bw.Write((short)bitsPerSample);

        // data chunk
        bw.Write("data"u8);
        bw.Write(dataSize);

        for (int i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i], -1.0f, 1.0f);
            bw.Write((short)(clamped * 32767));
        }

        return ms.ToArray();
    }
}
