using System.Text.Json;
using Parlotype.Benchmark.Configuration;

namespace Parlotype.Benchmark.Tests;

public class SweepExpanderTests
{
    private static JsonElement ToElement(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static JsonElement[] ToElements(params object[] values)
        => values.Select(ToElement).ToArray();

    [Fact]
    public void Expand_SingleAxis_ProducesCorrectCount()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["whisper.model"] = ToElements("Base", "Small"),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(2, configs.Count);
    }

    [Fact]
    public void Expand_TwoAxes_ProducesCartesianProduct()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["whisper.model"] = ToElements("Base", "Small"),
                ["whisper.beamSize"] = ToElements(1, 5),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(4, configs.Count);
    }

    [Fact]
    public void Expand_ThreeAxes_ProducesCartesianProduct()
    {
        var sweep = new SweepConfig
        {
            Name = "sweep",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["whisper.model"] = ToElements("Base", "Small"),
                ["whisper.beamSize"] = ToElements(1, 5),
                ["vad.enabled"] = ToElements(true, false),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(8, configs.Count); // 2 × 2 × 2
    }

    [Fact]
    public void Expand_ConfigNaming_ContainsParameterValues()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["whisper.model"] = ToElements("Base"),
                ["whisper.beamSize"] = ToElements(5),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Single(configs);
        Assert.Contains("Base", configs[0].Name);
        Assert.Contains("beam5", configs[0].Name);
    }

    [Fact]
    public void Expand_PropagatesDatasets()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds1", "ds2"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["whisper.model"] = ToElements("Base"),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(["ds1", "ds2"], configs[0].Datasets);
    }

    [Fact]
    public void Expand_PropagatesRepetitions()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Repetitions = 3,
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["whisper.model"] = ToElements("Base"),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(3, configs[0].Repetitions);
    }

    [Fact]
    public void Expand_InvalidPath_Throws()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["invalid.path"] = ToElements("value"),
            },
        };

        Assert.Throws<InvalidOperationException>(() => SweepExpander.Expand(sweep));
    }

    [Fact]
    public void Expand_EmptySweep_Throws()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>(),
        };

        Assert.Throws<InvalidOperationException>(() => SweepExpander.Expand(sweep));
    }

    [Fact]
    public void Expand_VadAxis_ProducesVadAndNovadNames()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["vad.enabled"] = ToElements(true, false),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(2, configs.Count);
        Assert.Contains(configs, c => c.Name.Contains("vad"));
        Assert.Contains(configs, c => c.Name.Contains("novad"));
    }

    [Fact]
    public void Expand_TemperatureAxis_FormattedCorrectly()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["whisper.temperature"] = ToElements(0.0f, 0.5f),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(2, configs.Count);
        Assert.Equal(0.0f, configs[0].Whisper!.Temperature);
        Assert.Equal(0.5f, configs[1].Whisper!.Temperature);
    }

    [Fact]
    public void Expand_VadThresholdAxis_ProducesCorrectValues()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["vad.threshold"] = ToElements(0.3f, 0.5f, 0.7f),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(3, configs.Count);
        Assert.Equal(0.3f, configs[0].Vad.Threshold, 0.01f);
        Assert.Equal(0.5f, configs[1].Vad.Threshold, 0.01f);
        Assert.Equal(0.7f, configs[2].Vad.Threshold, 0.01f);
    }

    [Fact]
    public void Expand_VadInterSegmentSilenceAxis_ProducesCorrectValues()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["vad.interSegmentSilenceMs"] = ToElements(0, 100, 160, 300),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(4, configs.Count);
        Assert.Equal(0, configs[0].Vad.InterSegmentSilenceMs);
        Assert.Equal(100, configs[1].Vad.InterSegmentSilenceMs);
        Assert.Equal(160, configs[2].Vad.InterSegmentSilenceMs);
        Assert.Equal(300, configs[3].Vad.InterSegmentSilenceMs);
    }

    [Fact]
    public void Expand_VadSpeechPadAxis_ProducesCorrectValues()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["vad.speechPadMs"] = ToElements(100, 200, 300),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(3, configs.Count);
        Assert.Equal(100, configs[0].Vad.SpeechPadMs);
        Assert.Equal(200, configs[1].Vad.SpeechPadMs);
        Assert.Equal(300, configs[2].Vad.SpeechPadMs);
        Assert.Contains("pad100", configs[0].Name);
        Assert.Contains("pad300", configs[2].Name);
    }

    [Fact]
    public void Expand_VadMinSilenceDurationAxis_ProducesCorrectValues()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["vad.minSilenceDurationMs"] = ToElements(300, 500, 800),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(3, configs.Count);
        Assert.Equal(300, configs[0].Vad.MinSilenceDurationMs);
        Assert.Equal(500, configs[1].Vad.MinSilenceDurationMs);
        Assert.Equal(800, configs[2].Vad.MinSilenceDurationMs);
    }
    [Fact]
    public void Expand_ParakeetSweep_CarriesEngineOntoEveryConfig()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Parakeet = new ParakeetConfig { ModelId = "parakeet-tdt-0.6b-v3-int8" },
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["vad.silenceThresholdMs"] = ToElements(500, 3000),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(2, configs.Count);
        Assert.All(configs, c =>
        {
            Assert.True(c.IsParakeet);
            Assert.False(c.IsWhisper);
            Assert.Equal("parakeet-tdt-0.6b-v3-int8", c.Parakeet!.ModelId);
        });
    }

    [Fact]
    public void Expand_WithoutEngineBlock_StaysWhisper()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["whisper.model"] = ToElements("Base"),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.True(configs[0].IsWhisper);
        Assert.Null(configs[0].Parakeet);
    }

    [Fact]
    public void Expand_NullSilenceThreshold_SelectsOneShotMode()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["vad.silenceThresholdMs"] = ToElements(3000, null!),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(3000, configs[0].Vad.SilenceThresholdMs);
        Assert.Null(configs[1].Vad.SilenceThresholdMs);
        Assert.Contains("flushnone", configs[1].Name);
    }

    [Fact]
    public void Expand_MaxBufferSeconds_AppliesToConfigAndName()
    {
        var sweep = new SweepConfig
        {
            Name = "test",
            Datasets = ["ds"],
            Sweep = new Dictionary<string, JsonElement[]>
            {
                ["vad.maxBufferSeconds"] = ToElements(30, 90),
            },
        };

        var configs = SweepExpander.Expand(sweep);

        Assert.Equal(30, configs[0].Vad.MaxBufferSeconds);
        Assert.Equal(90, configs[1].Vad.MaxBufferSeconds);
        Assert.Contains("cap90s", configs[1].Name);
    }

}
