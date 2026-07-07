using System.Text.Json;
using Parlotype.Benchmark.Configuration;

namespace Parlotype.Benchmark.Tests;

public sealed class ParakeetConfigTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Deserialize_ParakeetConfig_ParsesModelId()
    {
        var json = """
        {
          "name": "parakeet-test",
          "datasets": ["smoke-test"],
          "parakeet": {
            "modelId": "parakeet-tdt-0.6b-v3-int8"
          },
          "vad": { "enabled": false }
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.True(config.IsParakeet);
        Assert.False(config.IsLlamaCpp);
        Assert.False(config.IsWhisper);
        Assert.NotNull(config.Parakeet);
        Assert.Equal("parakeet-tdt-0.6b-v3-int8", config.Parakeet.ModelId);
    }

    [Fact]
    public void Deserialize_ParakeetConfig_AppliesDefaultModelId()
    {
        var json = """
        {
          "name": "parakeet-defaults",
          "datasets": ["ds"],
          "parakeet": {}
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.True(config.IsParakeet);
        Assert.Equal("parakeet-tdt-0.6b-v3-int8", config.Parakeet!.ModelId);
    }

    [Fact]
    public void Deserialize_WhisperOnly_IsParakeetIsFalse()
    {
        var json = """
        {
          "name": "whisper-test",
          "datasets": ["ds"],
          "whisper": { "model": "Base" }
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.False(config.IsParakeet);
        Assert.True(config.IsWhisper);
        Assert.Null(config.Parakeet);
    }

    [Fact]
    public void EngineName_Parakeet_ReturnsParakeet()
    {
        var config = new BenchmarkConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Parakeet = new ParakeetConfig(),
        };

        Assert.Equal("Parakeet", config.EngineName);
    }

    [Fact]
    public void ModelDisplayName_Parakeet_ReturnsModelId()
    {
        var config = new BenchmarkConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Parakeet = new ParakeetConfig { ModelId = "parakeet-tdt-0.6b-v3-int8" },
        };

        Assert.Equal("parakeet-tdt-0.6b-v3-int8", config.ModelDisplayName);
    }

    [Fact]
    public void DisplayHelpers_Parakeet_ShowAutoLanguageAndNoBeamSize()
    {
        var config = new BenchmarkConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Parakeet = new ParakeetConfig(),
        };

        Assert.Equal("auto (Parakeet)", config.LanguageDisplay);
        Assert.Equal("-", config.BeamSizeDisplay);
    }

    [Fact]
    public void DisplayHelpers_Whisper_ShowConfiguredLanguageAndBeamSize()
    {
        var config = new BenchmarkConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Whisper = new WhisperConfig { Language = "en", BeamSize = 5 },
        };

        Assert.Equal("en", config.LanguageDisplay);
        Assert.Equal("5", config.BeamSizeDisplay);
    }

    [Fact]
    public void Deserialize_ParakeetSmokeConfig_MatchesSampleFile()
    {
        var json = File.ReadAllText(
            Path.Combine(FindDatasetsDir(), "parakeet-smoke-config.json"));

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.True(config.IsParakeet);
        Assert.Equal("smoke-test-parakeet", config.Name);
        Assert.Equal("parakeet-tdt-0.6b-v3-int8", config.Parakeet!.ModelId);
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
