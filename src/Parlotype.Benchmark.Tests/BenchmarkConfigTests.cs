using System.Text.Json;
using Parlotype.Benchmark.Configuration;
using Parlotype.Core.Speech;

namespace Parlotype.Benchmark.Tests;

public sealed class BenchmarkConfigTests
{
    [Fact]
    public void Deserialize_FullConfig()
    {
        var json = """
        {
          "name": "test-run",
          "description": "A test configuration",
          "datasets": ["dataset-a", "dataset-b"],
          "repetitions": 3,
          "whisper": {
            "model": "Small",
            "language": "en",
            "beamSize": 5,
            "temperature": 0.2
          },
          "vad": {
            "enabled": false
          }
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal("test-run", config.Name);
        Assert.Equal("A test configuration", config.Description);
        Assert.Equal(["dataset-a", "dataset-b"], config.Datasets);
        Assert.Equal(3, config.Repetitions);
        Assert.Equal(WhisperModelType.Small, config.Whisper.Model);
        Assert.Equal("en", config.Whisper.Language);
        Assert.Equal(5, config.Whisper.BeamSize);
        Assert.Equal(0.2f, config.Whisper.Temperature);
        Assert.False(config.Vad.Enabled);
    }

    [Fact]
    public void Deserialize_MinimalConfig_AppliesDefaults()
    {
        var json = """
        {
          "name": "minimal",
          "datasets": ["ds"]
        }
        """;

        var config = JsonSerializer.Deserialize<BenchmarkConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal("minimal", config.Name);
        Assert.Null(config.Description);
        Assert.Single(config.Datasets);
        Assert.Equal(1, config.Repetitions);
        Assert.Equal(WhisperModelType.Base, config.Whisper.Model);
        Assert.Equal("auto", config.Whisper.Language);
        Assert.Equal(1, config.Whisper.BeamSize);
        Assert.Equal(0.0f, config.Whisper.Temperature);
        Assert.True(config.Vad.Enabled);
    }

    [Fact]
    public void Deserialize_DatasetManifest()
    {
        var json = """
        {
          "name": "test-dataset",
          "description": "Test description",
          "language": "en",
          "samples": [
            {
              "id": "sample-001",
              "file": "samples/sample-001.wav",
              "referenceText": "hello world",
              "language": "en",
              "durationSeconds": 2.5,
              "tags": ["clean", "short"]
            }
          ]
        }
        """;

        var manifest = JsonSerializer.Deserialize<DatasetManifest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal("test-dataset", manifest.Name);
        Assert.Single(manifest.Samples);
        Assert.Equal("sample-001", manifest.Samples[0].Id);
        Assert.Equal("hello world", manifest.Samples[0].ReferenceText);
        Assert.Equal(2.5, manifest.Samples[0].DurationSeconds);
        Assert.Equal(["clean", "short"], manifest.Samples[0].Tags);
    }
}
