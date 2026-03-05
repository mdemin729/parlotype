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
        Assert.Equal(0.0f, configs[0].Whisper.Temperature);
        Assert.Equal(0.5f, configs[1].Whisper.Temperature);
    }
}
