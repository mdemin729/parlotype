using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parlotype.Core.LlamaServer;
using Parlotype.Core.Settings;
using Parlotype.Core.TextProcessing;
using Parlotype.Platform.LlamaServer;

namespace Parlotype.Platform.TextProcessing;

/// <summary>
/// Speech cleanup backed by a <c>llama-server</c> child process that hosts the
/// cleanup model (ADR-073). The process is started on first use, on its own
/// loopback port, and kept for the life of the app.
/// </summary>
/// <remarks>
/// <para>This is a second <c>llama-server</c> consumer beside
/// <see cref="Speech.LlamaCppSpeechRecognizer"/>, and it runs its own process
/// rather than sharing one: a server hosts a single model, and the two consumers
/// need different ones. They do share the installed binary, which is why this
/// type also implements <see cref="ILlamaCppServerLifecycle"/>: the installer
/// has to stop every process holding the executable before it replaces it.</para>
/// <para>The port is picked fresh for each launch instead of being a setting.
/// Nothing outside this class talks to the server, so there is no address for a
/// user to know, no conflict to resolve and nothing to adopt.</para>
/// </remarks>
public sealed class LlamaServerSpeechCleanupService
    : ISpeechCleanupService, ILlamaCppServerLifecycle, IAsyncDisposable
{
    private const string Host = "127.0.0.1";

    /// <summary>A 462 MB model loads in a second or two; this covers a cold disk.</summary>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Per-request ceiling. A dictation cleans in well under a second on a
    /// laptop CPU; past this the user is better served by the raw text now.
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    private readonly ISettingsService _settings;
    private readonly ILlamaServerRegistry _registry;
    private readonly SpeechCleanupModelDownloadService _models;
    private readonly ILogger<LlamaServerSpeechCleanupService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private LlamaServerProcess? _server;
    private HttpClient? _http;
    private bool _disposed;

    public LlamaServerSpeechCleanupService(
        ISettingsService settings,
        ILlamaServerRegistry registry,
        SpeechCleanupModelDownloadService models,
        ILogger<LlamaServerSpeechCleanupService> logger)
    {
        _settings = settings;
        _registry = registry;
        _models = models;
        _logger = logger;
    }

    private static SpeechCleanupModelInfo Model => SpeechCleanupModelInfo.Default;

    public async Task<SpeechCleanupReadiness> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        if (!_models.IsModelCached(Model))
            return SpeechCleanupReadiness.ModelMissing;

        var executable = await LlamaServerPathResolver.ResolveExecutableAsync(
            _registry, _settings, cancellationToken);
        return executable is not null && File.Exists(executable)
            ? SpeechCleanupReadiness.Ready
            : SpeechCleanupReadiness.ServerMissing;
    }

    public async Task PrepareAsync(CancellationToken cancellationToken = default) =>
        await EnsureStartedAsync(cancellationToken);

    public async Task<string> CleanAsync(
        string transcript,
        SpeechCleanupOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(options);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var http = await EnsureStartedAsync(cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using var response = await http.PostAsJsonAsync(
                "/v1/chat/completions", SpeechCleanupRequest.Build(options, transcript), timeout.Token);

            // The status code only. An error body can quote the request back,
            // and the request is the user's dictation (ADR-046).
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Speech cleanup request failed (HTTP {(int)response.StatusCode}).");

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            return SpeechCleanupRequest.ParseContent(doc.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Speech cleanup did not answer within {RequestTimeout.TotalSeconds:F0} seconds.");
        }
    }

    public async Task UnloadAsync()
    {
        if (_disposed)
            return;

        await _lock.WaitAsync();
        try
        {
            await StopCoreAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public Task StopForReplacementAsync(CancellationToken cancellationToken = default) => UnloadAsync();

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await StopCoreAsync();
        _lock.Dispose();
    }

    /// <summary>
    /// Returns the client for a running server, starting one if there is none or
    /// the previous one died.
    /// </summary>
    private async Task<HttpClient> EnsureStartedAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_server is { HasExited: false } && _http is not null)
                return _http;

            // Either never started or it crashed; clear what is left of it.
            await StopCoreAsync();

            var modelPath = _models.GetModelPath(Model);
            if (!File.Exists(modelPath))
                throw new InvalidOperationException(
                    $"Speech cleanup model not found at '{modelPath}'. Download it in Settings → Speech cleanup.");

            var executable = await LlamaServerPathResolver.ResolveExecutableAsync(
                _registry, _settings, cancellationToken);
            if (executable is null || !File.Exists(executable))
                throw new InvalidOperationException(
                    "No llama-server is installed for speech cleanup. Set it up in Settings → Speech cleanup.");

            var port = FindFreeLoopbackPort();
            _logger.LogInformation(
                "Starting llama-server for speech cleanup (model={Model}, port={Port})",
                Model.GgufFileName, port);

            var server = LlamaServerProcess.Start(executable, BuildArguments(modelPath, port), _logger);
            var http = new HttpClient { BaseAddress = new Uri($"http://{Host}:{port}") };
            try
            {
                await server.WaitUntilHealthyAsync(http, StartupTimeout, cancellationToken);
            }
            catch
            {
                http.Dispose();
                await server.DisposeAsync();
                throw;
            }

            // From here on requests carry dictated text.
            server.MuteOutput();

            _server = server;
            _http = http;
            _logger.LogInformation("Speech cleanup server is ready (PID {Pid})", server.Id);
            return http;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// The command line for the cleanup server.
    /// </summary>
    /// <remarks>
    /// <para><c>--jinja</c> with <c>enable_thinking: false</c>, not
    /// <c>--reasoning-budget 0</c>: both silence the think block, but the model
    /// card reports that the second degrades the output (fillers survive).</para>
    /// <para><c>-np 1</c> because <c>llama-server</c> divides the context between
    /// its slots, and the default is several: leaving it would quietly cut the
    /// window a dictation gets to a fraction of <c>-c</c>.</para>
    /// <para>No <c>-ngl</c>: the default offloads to a GPU when the build has
    /// one and runs on the CPU otherwise, which is what we want from either.</para>
    /// </remarks>
    internal static IReadOnlyList<string> BuildArguments(string modelPath, int port) =>
    [
        "-m", modelPath,
        "--host", Host,
        "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "-c", Model.ContextTokens.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "-np", "1",
        "--jinja",
        "--chat-template-kwargs", "{\"enable_thinking\":false}",
        "--temp", "0",
        "--no-webui",
    ];

    /// <summary>
    /// Asks the OS for an unused loopback port. Another process could take it
    /// between this call and the server binding it; the server would then exit
    /// during startup and the next attempt picks a different port.
    /// </summary>
    private static int FindFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task StopCoreAsync()
    {
        var server = _server;
        var http = _http;
        _server = null;
        _http = null;

        http?.Dispose();
        if (server is not null)
            await server.DisposeAsync();
    }
}
