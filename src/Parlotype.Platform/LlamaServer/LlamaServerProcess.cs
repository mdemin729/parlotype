using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Parlotype.Platform.LlamaServer;

/// <summary>
/// One spawned <c>llama-server</c> child process: start, wait for
/// <c>/health</c>, stop. Knows nothing about what the server is used for.
/// </summary>
internal sealed class LlamaServerProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly ILogger _logger;

    private LlamaServerProcess(Process process, ILogger logger)
    {
        _process = process;
        _logger = logger;
    }

    /// <summary>Cleared by <see cref="MuteOutput"/>; read by the drain tasks.</summary>
    private volatile bool _forwardOutput = true;

    public int Id => _process.Id;

    public bool HasExited => _process.HasExited;

    /// <summary>
    /// Stops copying the server's stdout/stderr into the app log; the pipes keep
    /// being drained. Call once the server is healthy when requests will carry
    /// user text: the startup lines are worth having for diagnostics, but what
    /// the server prints per request is its own business and not ours to
    /// promise about, and dictated text must never reach a log file (ADR-046).
    /// </summary>
    public void MuteOutput() => _forwardOutput = false;

    /// <summary>
    /// Starts <paramref name="executablePath"/> with <paramref name="arguments"/>.
    /// Arguments go through <see cref="ProcessStartInfo.ArgumentList"/>, never a
    /// hand-quoted command line, so a path with quotes or shell metacharacters
    /// cannot break it (security audit 2026-07-11, S6).
    /// </summary>
    public static LlamaServerProcess Start(
        string executablePath, IEnumerable<string> arguments, ILogger logger)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start llama-server process.");

        var server = new LlamaServerProcess(process, logger);

        // Drain both pipes or the child blocks once a pipe buffer fills.
        _ = server.DrainAsync(process.StandardOutput, "stdout");
        _ = server.DrainAsync(process.StandardError, "stderr");
        return server;
    }

    /// <summary>
    /// Polls <c>/health</c> on <paramref name="http"/> until it answers 2xx.
    /// Throws <see cref="InvalidOperationException"/> if the process exits first
    /// and <see cref="TimeoutException"/> after <paramref name="timeout"/>.
    /// </summary>
    public async Task WaitUntilHealthyAsync(
        HttpClient http, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var delay = TimeSpan.FromMilliseconds(150);

        while (sw.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    $"llama-server exited unexpectedly during startup (exit code: {_process.ExitCode}). " +
                    "Check that the binary is compatible and the model file is valid.");
            }

            try
            {
                using var response = await http.GetAsync("/health", cancellationToken);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // Not accepting connections yet.
            }

            await Task.Delay(delay, cancellationToken);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 1.5, 2000));
        }

        throw new TimeoutException(
            $"llama-server did not become ready within {timeout.TotalSeconds:F0} seconds.");
    }

    /// <summary>Kills the process tree and waits briefly for it to go. Never throws.</summary>
    public async Task StopAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _logger.LogInformation("Stopping llama-server (PID {Pid})", _process.Id);
                _process.Kill(entireProcessTree: true);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _process.WaitForExitAsync(cts.Token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop llama-server gracefully");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _process.Dispose();
    }

    private async Task DrainAsync(StreamReader reader, string label)
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                if (_forwardOutput && line.Length > 0)
                    _logger.LogDebug("[llama-server {Label}] {Line}", label, line);
            }
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error draining llama-server {Label} stream", label);
        }
    }
}
