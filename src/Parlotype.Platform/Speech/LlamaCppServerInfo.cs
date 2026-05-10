using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Parlotype.Platform.Speech;

/// <summary>Status of the llama-server connection.</summary>
public enum LlamaCppServerStatus
{
    /// <summary>Not yet probed.</summary>
    Unknown,

    /// <summary>Server is healthy and confirmed as llama-server.</summary>
    Connected,

    /// <summary>Nothing is listening on the port.</summary>
    Disconnected,

    /// <summary>Port is in use by a process that is not llama-server.</summary>
    PortConflict,

    /// <summary>Server is listening but still loading the model.</summary>
    Loading,

    /// <summary>Probe failed with an unexpected error.</summary>
    Error
}

/// <summary>Information fetched from a running llama-server's /props and /health endpoints.</summary>
public sealed record LlamaCppServerInfo
{
    public LlamaCppServerStatus Status { get; init; } = LlamaCppServerStatus.Unknown;
    public string? ModelAlias { get; init; }
    public string? ModelPath { get; init; }
    public bool AudioSupported { get; init; }
    public string? BuildInfo { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Probes a llama-server at the given host:port and returns its info.</summary>
    public static async Task<LlamaCppServerInfo> ProbeAsync(
        string host, int port, CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        return await ProbeAsync(http, host, port, cancellationToken);
    }

    /// <summary>Probes a llama-server using the provided HttpClient.</summary>
    public static async Task<LlamaCppServerInfo> ProbeAsync(
        HttpClient http, string host, int port, CancellationToken cancellationToken = default)
    {
        var baseUrl = $"http://{host}:{port}";

        try
        {
            // Step 1: Check /health
            using var healthResp = await http.GetAsync($"{baseUrl}/health", cancellationToken);
            if (!healthResp.IsSuccessStatusCode)
            {
                return new LlamaCppServerInfo
                {
                    Status = healthResp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                        ? LlamaCppServerStatus.Loading
                        : LlamaCppServerStatus.Error,
                    ErrorMessage = $"Health check returned HTTP {(int)healthResp.StatusCode}"
                };
            }

            // Step 2: Check /props to verify it's llama-server
            using var propsResp = await http.GetAsync($"{baseUrl}/props", cancellationToken);
            if (!propsResp.IsSuccessStatusCode)
            {
                return new LlamaCppServerInfo
                {
                    Status = LlamaCppServerStatus.PortConflict,
                    ErrorMessage = $"Port {port} is in use by another application (not llama-server)"
                };
            }

            // Step 3: Parse /props response
            using var propsStream = await propsResp.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(propsStream, cancellationToken: cancellationToken);
            var root = doc.RootElement;

            var modelAlias = root.TryGetProperty("model_alias", out var ma) ? ma.GetString() : null;
            var modelPath = root.TryGetProperty("model_path", out var mp) ? mp.GetString() : null;
            var buildInfo = root.TryGetProperty("build_info", out var bi) ? bi.GetString() : null;

            var audioSupported = false;
            if (root.TryGetProperty("modalities", out var modalities) &&
                modalities.TryGetProperty("audio", out var audio))
            {
                audioSupported = audio.GetBoolean();
            }

            return new LlamaCppServerInfo
            {
                Status = LlamaCppServerStatus.Connected,
                ModelAlias = modelAlias,
                ModelPath = modelPath,
                AudioSupported = audioSupported,
                BuildInfo = buildInfo
            };
        }
        catch (HttpRequestException)
        {
            return new LlamaCppServerInfo { Status = LlamaCppServerStatus.Disconnected };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LlamaCppServerInfo { Status = LlamaCppServerStatus.Disconnected };
        }
        catch (Exception ex)
        {
            return new LlamaCppServerInfo
            {
                Status = LlamaCppServerStatus.Error,
                ErrorMessage = ex.Message
            };
        }
    }
}
