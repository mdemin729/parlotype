using System.Text.Json;
using Parlotype.Benchmark.Configuration;

namespace Parlotype.Benchmark.Tests;

public sealed class LlamaCppConfigTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Deserialize_LlamaCppConfig_ParsesAllProperties()
    {
        var json = """
        {
          "name": "llamacpp-test",
          "datasets": ["smoke-test"],
          "llamaCpp": {
            "modelId": "gemma-4-E4B-it-Q8_0",
            "port": 9000,
            "serverFolder": "D:\\llama-server"
          },
          "vad": { "enabled": false }
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.True(config.IsLlamaCpp);
        Assert.NotNull(config.LlamaCpp);
        Assert.Equal("gemma-4-E4B-it-Q8_0", config.LlamaCpp.ModelId);
        Assert.Equal(9000, config.LlamaCpp.Port);
        Assert.Equal("D:\\llama-server", config.LlamaCpp.ServerFolder);
    }

    [Fact]
    public void Deserialize_LlamaCppConfig_AppliesDefaults()
    {
        var json = """
        {
          "name": "llamacpp-defaults",
          "datasets": ["ds"],
          "llamaCpp": {}
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.True(config.IsLlamaCpp);
        Assert.NotNull(config.LlamaCpp);
        Assert.Equal("gemma-4-E4B-it-Q4_K_M", config.LlamaCpp.ModelId);
        Assert.Equal(8321, config.LlamaCpp.Port);
        Assert.Null(config.LlamaCpp.ServerFolder);
    }

    [Fact]
    public void Deserialize_WhisperOnly_IsLlamaCppIsFalse()
    {
        var json = """
        {
          "name": "whisper-test",
          "datasets": ["ds"],
          "whisper": { "model": "Base" }
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.False(config.IsLlamaCpp);
        Assert.Null(config.LlamaCpp);
        Assert.NotNull(config.Whisper);
    }

    [Fact]
    public void Deserialize_NoEngine_FallsBackToWhisperDefaults()
    {
        var json = """
        {
          "name": "no-engine",
          "datasets": ["ds"]
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.False(config.IsLlamaCpp);
        Assert.Null(config.LlamaCpp);
        Assert.Null(config.Whisper);
        Assert.Equal("auto", config.EffectiveWhisper.Language);
    }

    [Fact]
    public void EngineName_LlamaCpp_ReturnsGemma4()
    {
        var config = new BenchmarkConfig
        {
            Name = "test",
            Datasets = ["ds"],
            LlamaCpp = new LlamaCppConfig { ModelId = "gemma-4-E4B-it-Q4_K_M" },
        };

        Assert.Equal("Gemma4", config.EngineName);
    }

    [Fact]
    public void ModelDisplayName_LlamaCpp_ReturnsModelId()
    {
        var config = new BenchmarkConfig
        {
            Name = "test",
            Datasets = ["ds"],
            LlamaCpp = new LlamaCppConfig { ModelId = "gemma-4-E4B-it-Q4_K_M" },
        };

        Assert.Equal("gemma-4-E4B-it-Q4_K_M", config.ModelDisplayName);
    }

    [Fact]
    public void ModelDisplayName_Whisper_ReturnsModelType()
    {
        var config = new BenchmarkConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Whisper = new WhisperConfig { Model = Core.Speech.WhisperModelType.Small },
        };

        Assert.Equal("Whisper", config.EngineName);
        Assert.Equal("Small", config.ModelDisplayName);
    }

    [Fact]
    public void Deserialize_GemmaSmokeSmokeTestConfig_MatchesSampleFile()
    {
        var json = File.ReadAllText(
            Path.Combine(FindDatasetsDir(), "gemma4-smoke-test-config.json"));

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.True(config.IsLlamaCpp);
        Assert.Equal("gemma4-smoke-test", config.Name);
        Assert.Equal("gemma-4-E4B-it-Q4_K_M", config.LlamaCpp!.ModelId);
        Assert.Equal(8321, config.LlamaCpp.Port);
        Assert.False(config.Vad.Enabled);
    }

    private static string FindDatasetsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var datasets = Path.Combine(dir.FullName, "datasets");
            if (Directory.Exists(datasets))
                return datasets;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Cannot find datasets directory");
    }
}
