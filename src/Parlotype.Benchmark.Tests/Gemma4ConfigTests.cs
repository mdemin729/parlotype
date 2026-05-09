using System.Text.Json;
using Parlotype.Benchmark.Configuration;
using Parlotype.Gemma4;

namespace Parlotype.Benchmark.Tests;

public sealed class Gemma4ConfigTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Deserialize_Gemma4Config_ParsesAllProperties()
    {
        var json = """
        {
          "name": "gemma4-test",
          "datasets": ["smoke-test"],
          "gemma4": {
            "modelId": "gemma-4-E4B-it",
            "modelPath": "/custom/path",
            "quantization": "8bit",
            "port": 9000,
            "pythonPath": "python3",
            "maxNewTokens": 300,
            "deviceMap": "cuda:0",
            "startupTimeoutSeconds": 240
          },
          "vad": { "enabled": false }
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.True(config.IsGemma4);
        Assert.NotNull(config.Gemma4);
        Assert.Equal("gemma-4-E4B-it", config.Gemma4.ModelId);
        Assert.Equal("/custom/path", config.Gemma4.ModelPath);
        Assert.Equal("8bit", config.Gemma4.Quantization);
        Assert.Equal(9000, config.Gemma4.Port);
        Assert.Equal("python3", config.Gemma4.PythonPath);
        Assert.Equal(300, config.Gemma4.MaxNewTokens);
        Assert.Equal("cuda:0", config.Gemma4.DeviceMap);
        Assert.Equal(240, config.Gemma4.StartupTimeoutSeconds);
    }

    [Fact]
    public void Deserialize_Gemma4Config_AppliesDefaults()
    {
        var json = """
        {
          "name": "gemma4-defaults",
          "datasets": ["ds"],
          "gemma4": {}
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.True(config.IsGemma4);
        Assert.NotNull(config.Gemma4);
        Assert.Equal("gemma-4-E2B-it", config.Gemma4.ModelId);
        Assert.Null(config.Gemma4.ModelPath);
        Assert.Equal("none", config.Gemma4.Quantization);
        Assert.Equal(8321, config.Gemma4.Port);
        Assert.Equal("python", config.Gemma4.PythonPath);
        Assert.Equal(200, config.Gemma4.MaxNewTokens);
        Assert.Equal("auto", config.Gemma4.DeviceMap);
        Assert.Equal(180, config.Gemma4.StartupTimeoutSeconds);
    }

    [Fact]
    public void Deserialize_WhisperOnly_IsGemma4IsFalse()
    {
        var json = """
        {
          "name": "whisper-test",
          "datasets": ["ds"],
          "whisper": { "model": "Base" }
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.False(config.IsGemma4);
        Assert.Null(config.Gemma4);
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

        Assert.False(config.IsGemma4);
        Assert.Null(config.Whisper);
        Assert.Null(config.Gemma4);
        // EffectiveWhisper provides defaults when no engine block present
        Assert.Equal("auto", config.EffectiveWhisper.Language);
    }

    [Fact]
    public void ModelDisplayName_Gemma4_IncludesModelIdAndQuantization()
    {
        var config = new BenchmarkConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Gemma4 = new Gemma4Config { ModelId = "gemma-4-E2B-it", Quantization = "4bit" },
        };

        Assert.Equal("Gemma4", config.EngineName);
        Assert.Equal("Gemma4/gemma-4-E2B-it (4bit)", config.ModelDisplayName);
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
    public void GetEffectiveModelPath_DefaultPath_UsesLocalAppData()
    {
        var gemma4Config = new Gemma4Config { ModelId = "gemma-4-E2B-it" };
        var path = gemma4Config.GetEffectiveModelPath();

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parlotype", "models", "gemma-4-E2B-it");

        Assert.Equal(expected, path);
    }

    [Fact]
    public void GetEffectiveModelPath_ExplicitPath_UsesProvided()
    {
        var gemma4Config = new Gemma4Config
        {
            ModelId = "gemma-4-E2B-it",
            ModelPath = @"D:\models\gemma4",
        };

        Assert.Equal(@"D:\models\gemma4", gemma4Config.GetEffectiveModelPath());
    }

    [Fact]
    public void Deserialize_Gemma4SmokeTestConfig_MatchesSampleConfig()
    {
        var json = File.ReadAllText(
            Path.Combine(FindDatasetsDir(), "gemma4-smoke-test-config.json"));

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json, JsonOptions)!;

        Assert.True(config.IsGemma4);
        Assert.Equal("gemma4-smoke-test", config.Name);
        Assert.Equal("gemma-4-E2B-it", config.Gemma4!.ModelId);
        Assert.Equal("none", config.Gemma4.Quantization);
        Assert.Equal(8321, config.Gemma4.Port);
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
