using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;

namespace Parlotype.Gemma4;

/// <summary>
/// Speech recognizer that delegates to a Python sidecar running Gemma 4.
/// The sidecar is auto-managed: spawned on <see cref="InitializeAsync"/> and
/// killed on <see cref="DisposeAsync"/>.
/// </summary>
public sealed class Gemma4SpeechRecognizer : ISpeechRecognizer
{
    private readonly Gemma4Config _config;
    private readonly ILogger<Gemma4SpeechRecognizer> _logger;
    private readonly HttpClient _httpClient = new();
    private readonly string _authToken = Guid.NewGuid().ToString("N");

    private Process? _sidecarProcess;
    private string? _tempDirectory;
    private bool _disposed;

    public Gemma4SpeechRecognizer(Gemma4Config config, ILogger<Gemma4SpeechRecognizer> logger)
    {
        _config = config;
        _logger = logger;
        _httpClient.BaseAddress = new Uri($"http://127.0.0.1:{_config.Port}");
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
    }

    public bool IsReady { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var modelPath = _config.GetEffectiveModelPath();
        if (!Directory.Exists(modelPath))
            throw new InvalidOperationException(
                $"Gemma 4 model not found at '{modelPath}'. " +
                $"Download it with: hf download google/{_config.ModelId} --local-dir \"{modelPath}\"");

        _tempDirectory = Path.Combine(Path.GetTempPath(), $"parlotype-gemma4-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        var sidecarPath = GetSidecarPath();
        if (!File.Exists(sidecarPath))
            throw new InvalidOperationException($"Sidecar script not found at '{sidecarPath}'.");

        _logger.LogInformation("Starting Gemma 4 sidecar (model={ModelId}, quantization={Quantization}, port={Port})",
            _config.ModelId, _config.Quantization, _config.Port);

        // Check if something is already listening on the port (stale sidecar from a previous run)
        try
        {
            using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var probeResponse = await probe.GetAsync($"http://127.0.0.1:{_config.Port}/health", cancellationToken);
            if (probeResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stale sidecar detected on port {Port} — sending shutdown", _config.Port);
                try
                {
                    await probe.PostAsync($"http://127.0.0.1:{_config.Port}/shutdown", null, cancellationToken);
                }
                catch { /* Shutdown kills the process before responding */ }

                // Wait until the port is actually free
                var portFree = false;
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(500, cancellationToken);
                    try
                    {
                        await probe.GetAsync($"http://127.0.0.1:{_config.Port}/health", cancellationToken);
                        // Still alive — keep waiting
                    }
                    catch (HttpRequestException)
                    {
                        portFree = true;
                        break;
                    }
                    catch (TaskCanceledException)
                    {
                        portFree = true;
                        break;
                    }
                }

                if (!portFree)
                    throw new InvalidOperationException(
                        $"Port {_config.Port} still in use after shutdown attempt. Kill the Python process manually.");

                _logger.LogInformation("Stale sidecar shut down, port {Port} is free", _config.Port);
            }
        }
        catch (HttpRequestException) { /* Port is free — expected */ }
        catch (TaskCanceledException) { /* Timeout — port is free */ }

        var psi = new ProcessStartInfo
        {
            FileName = _config.PythonPath,
            Arguments = $"\"{sidecarPath}\" " +
                        $"--model-path \"{modelPath}\" " +
                        $"--port {_config.Port} " +
                        $"--quantization {_config.Quantization} " +
                        $"--dtype {_config.Dtype} " +
                        $"--device-map {_config.DeviceMap} " +
                        $"--max-new-tokens {_config.MaxNewTokens} " +
                        $"--auth-token {_authToken} " +
                        $"--allowed-dir \"{_tempDirectory}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _sidecarProcess = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Python sidecar process.");

        // Drain stdout/stderr asynchronously to prevent pipe deadlocks
        _ = DrainStreamAsync(_sidecarProcess.StandardOutput, "stdout");
        _ = DrainStreamAsync(_sidecarProcess.StandardError, "stderr");

        // Poll /health until ready
        await WaitForSidecarReadyAsync(cancellationToken);

        IsReady = true;
        _logger.LogInformation("Gemma 4 sidecar is ready");
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsReady)
            throw new InvalidOperationException("Recognizer is not initialized. Call InitializeAsync first.");

        // Write samples to temp WAV file
        var wavPath = Path.Combine(_tempDirectory!, $"{Guid.NewGuid():N}.wav");
        WriteWavFile(wavPath, samples.Span, sampleRate: 16000);
        _logger.LogDebug("Transcribing {SampleCount} samples from {WavPath}", samples.Length, wavPath);

        try
        {
            var request = new TranscribeRequest { AudioPath = wavPath };
            var response = await _httpClient.PostAsJsonAsync("/transcribe", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Gemma 4 sidecar transcription failed (HTTP {(int)response.StatusCode}): {errorBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<TranscribeResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Empty response from Gemma 4 sidecar.");

            return new TranscriptionResult { Text = result.Text ?? "" };
        }
        finally
        {
            TryDeleteFile(wavPath);
        }
    }

    public async Task UnloadAsync()
    {
        await StopSidecarAsync();
        IsReady = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopSidecarAsync();

        if (_tempDirectory is not null)
        {
            try { Directory.Delete(_tempDirectory, recursive: true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to clean up temp directory: {Path}", _tempDirectory); }
        }

        _httpClient.Dispose();
    }

    private async Task StopSidecarAsync()
    {
        if (_sidecarProcess is null || _sidecarProcess.HasExited) return;

        // Attempt graceful shutdown
        try
        {
            await _httpClient.PostAsync("/shutdown", null);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _sidecarProcess.WaitForExitAsync(cts.Token);
        }
        catch
        {
            // Force kill if graceful shutdown fails
        }

        if (!_sidecarProcess.HasExited)
        {
            _logger.LogWarning("Forcefully killing Gemma 4 sidecar process (PID {Pid})", _sidecarProcess.Id);
            try { _sidecarProcess.Kill(entireProcessTree: true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to kill sidecar process"); }
        }

        _sidecarProcess.Dispose();
        _sidecarProcess = null;
    }

    private async Task WaitForSidecarReadyAsync(CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(_config.StartupTimeoutSeconds);
        var sw = Stopwatch.StartNew();
        var delay = TimeSpan.FromMilliseconds(500);

        while (sw.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_sidecarProcess is null || _sidecarProcess.HasExited)
            {
                throw new InvalidOperationException(
                    "Gemma 4 sidecar process exited unexpectedly during startup. " +
                    "Check that Python and all dependencies are installed. " +
                    "Run: pip install -r <project>/sidecar/requirements.txt");
            }

            try
            {
                var response = await _httpClient.GetFromJsonAsync<HealthResponse>("/health", cancellationToken);
                if (response?.Status == "ready")
                    return;

                if (response?.Status == "failed")
                    throw new InvalidOperationException("Gemma 4 sidecar reported failure during model loading.");

                _logger.LogDebug("Sidecar status: {Status} (elapsed: {Elapsed:F1}s)", response?.Status, sw.Elapsed.TotalSeconds);
            }
            catch (HttpRequestException)
            {
                // Server not yet accepting connections
            }

            await Task.Delay(delay, cancellationToken);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 1.5, 5000));
        }

        throw new TimeoutException(
            $"Gemma 4 sidecar did not become ready within {_config.StartupTimeoutSeconds} seconds. " +
            "Increase startupTimeoutSeconds in the config or check the Python process logs.");
    }

    private async Task DrainStreamAsync(System.IO.StreamReader reader, string label)
    {
        try
        {
            var buffer = new char[4096];
            while (true)
            {
                var count = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (count == 0) break;

                var text = new string(buffer, 0, count);
                // Split on newlines and carriage returns for progress bar output
                foreach (var line in text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
                    _logger.LogDebug("[Gemma4 {Label}] {Line}", label, line.TrimEnd());
            }
        }
        catch (ObjectDisposedException) { }
    }

    private static string GetSidecarPath()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        return Path.Combine(assemblyDir, "sidecar", "server.py");
    }

    private static void WriteWavFile(string path, ReadOnlySpan<float> samples, int sampleRate)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        int bitsPerSample = 16;
        int numChannels = 1;
        int bytesPerSample = bitsPerSample / 8;
        int dataSize = samples.Length * bytesPerSample;

        // RIFF header
        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);

        // fmt chunk
        bw.Write("fmt "u8);
        bw.Write(16); // chunk size
        bw.Write((short)1); // PCM
        bw.Write((short)numChannels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * numChannels * bytesPerSample); // byte rate
        bw.Write((short)(numChannels * bytesPerSample)); // block align
        bw.Write((short)bitsPerSample);

        // data chunk
        bw.Write("data"u8);
        bw.Write(dataSize);

        for (int i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i], -1.0f, 1.0f);
            var pcm = (short)(clamped * 32767);
            bw.Write(pcm);
        }
    }

    private void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) { _logger.LogDebug(ex, "Failed to delete temp file: {Path}", path); }
    }

    private sealed record TranscribeRequest
    {
        [JsonPropertyName("audio_path")]
        public required string AudioPath { get; init; }
    }

    private sealed record TranscribeResponse
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    private sealed record HealthResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("model_id")]
        public string? ModelId { get; init; }

        [JsonPropertyName("quantization")]
        public string? Quantization { get; init; }

        [JsonPropertyName("device")]
        public string? Device { get; init; }
    }
}
