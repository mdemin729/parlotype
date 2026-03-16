using System.Text.Json;
using Parlotype.Benchmark.Configuration;
using Parlotype.Core.Speech;

namespace Parlotype.Benchmark.Tests;

public sealed class GpuConfigTests
{
    [Fact]
    public void WhisperConfig_RuntimePreference_DefaultsToAuto()
    {
        var json = """
        {
          "name": "test",
          "datasets": ["ds"],
          "whisper": {
            "model": "Base"
          }
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(RuntimePreference.Auto, config.Whisper.RuntimePreference);
    }

    [Fact]
    public void WhisperConfig_RuntimePreference_DeserializesAuto()
    {
        var json = """
        {
          "name": "test",
          "datasets": ["ds"],
          "whisper": {
            "model": "Base",
            "runtimePreference": "Auto"
          }
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(RuntimePreference.Auto, config.Whisper.RuntimePreference);
    }

    [Fact]
    public void WhisperConfig_RuntimePreference_DeserializesCpu()
    {
        var json = """
        {
          "name": "test",
          "datasets": ["ds"],
          "whisper": {
            "model": "Base",
            "runtimePreference": "Cpu"
          }
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(RuntimePreference.Cpu, config.Whisper.RuntimePreference);
    }

    [Fact]
    public void BenchmarkConfig_GpuOverride_CpuForcesPreference()
    {
        var json = """
        {
          "name": "test",
          "datasets": ["ds"],
          "whisper": {
            "model": "Base",
            "runtimePreference": "Auto"
          }
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(RuntimePreference.Auto, config.Whisper.RuntimePreference);

        // Mimic what Program.cs does when --gpu false is passed:
        // create a new BenchmarkConfig with RuntimePreference overridden to Cpu.
        var overridden = new BenchmarkConfig
        {
            Name = config.Name,
            Datasets = config.Datasets,
            Repetitions = config.Repetitions,
            Whisper = new WhisperConfig
            {
                Model = config.Whisper.Model,
                Language = config.Whisper.Language,
                BeamSize = config.Whisper.BeamSize,
                Temperature = config.Whisper.Temperature,
                RuntimePreference = RuntimePreference.Cpu
            },
            Vad = config.Vad
        };

        Assert.Equal(RuntimePreference.Cpu, overridden.Whisper.RuntimePreference);
    }
}
