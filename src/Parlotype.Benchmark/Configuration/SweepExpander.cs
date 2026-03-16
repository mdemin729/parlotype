using System.Text.Json;
using Parlotype.Core.Speech;

namespace Parlotype.Benchmark.Configuration;

/// <summary>Expands a sweep configuration into individual benchmark configs via Cartesian product.</summary>
public static class SweepExpander
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Expands sweep axes into a list of fully-specified benchmark configurations.</summary>
    public static List<BenchmarkConfig> Expand(SweepConfig sweep)
    {
        if (sweep.Sweep.Count == 0)
            throw new InvalidOperationException("Sweep configuration has no parameter axes defined.");

        var axes = sweep.Sweep.ToList();
        var combinations = CartesianProduct(axes.Select(a => a.Value.Length).ToArray());

        var configs = new List<BenchmarkConfig>();

        foreach (var combination in combinations)
        {
            var whisper = new WhisperConfig();
            var vad = sweep.Vad;
            var nameParts = new List<string> { sweep.Name };

            for (int i = 0; i < axes.Count; i++)
            {
                var (path, values) = axes[i];
                var value = values[combination[i]];

                (whisper, vad, var namePart) = ApplyValue(whisper, vad, path, value);
                if (namePart is not null)
                    nameParts.Add(namePart);
            }

            configs.Add(new BenchmarkConfig
            {
                Name = string.Join("-", nameParts),
                Description = sweep.Description,
                Datasets = sweep.Datasets,
                Repetitions = sweep.Repetitions,
                Whisper = whisper,
                Vad = vad,
            });
        }

        return configs;
    }

    private static (WhisperConfig Whisper, VadConfig Vad, string? NamePart) ApplyValue(
        WhisperConfig whisper, VadConfig vad, string path, JsonElement value)
    {
        switch (path.ToLowerInvariant())
        {
            case "whisper.model":
                var model = Enum.Parse<WhisperModelType>(value.GetString()!, ignoreCase: true);
                return (new WhisperConfig
                {
                    Model = model,
                    Language = whisper.Language,
                    BeamSize = whisper.BeamSize,
                    Temperature = whisper.Temperature,
                    InitialPrompt = whisper.InitialPrompt,
                    Threads = whisper.Threads,
                    RuntimePreference = whisper.RuntimePreference,
                }, vad, model.ToString());

            case "whisper.beamsize":
                var beamSize = value.GetInt32();
                return (new WhisperConfig
                {
                    Model = whisper.Model,
                    Language = whisper.Language,
                    BeamSize = beamSize,
                    Temperature = whisper.Temperature,
                    InitialPrompt = whisper.InitialPrompt,
                    Threads = whisper.Threads,
                    RuntimePreference = whisper.RuntimePreference,
                }, vad, $"beam{beamSize}");

            case "whisper.temperature":
                var temp = value.GetSingle();
                return (new WhisperConfig
                {
                    Model = whisper.Model,
                    Language = whisper.Language,
                    BeamSize = whisper.BeamSize,
                    Temperature = temp,
                    InitialPrompt = whisper.InitialPrompt,
                    Threads = whisper.Threads,
                    RuntimePreference = whisper.RuntimePreference,
                }, vad, $"temp{temp:F1}");

            case "whisper.language":
                var lang = value.GetString()!;
                return (new WhisperConfig
                {
                    Model = whisper.Model,
                    Language = lang,
                    BeamSize = whisper.BeamSize,
                    Temperature = whisper.Temperature,
                    InitialPrompt = whisper.InitialPrompt,
                    Threads = whisper.Threads,
                    RuntimePreference = whisper.RuntimePreference,
                }, vad, lang == "auto" ? null : lang);

            case "whisper.threads":
                var threads = value.GetInt32();
                return (new WhisperConfig
                {
                    Model = whisper.Model,
                    Language = whisper.Language,
                    BeamSize = whisper.BeamSize,
                    Temperature = whisper.Temperature,
                    InitialPrompt = whisper.InitialPrompt,
                    Threads = threads,
                    RuntimePreference = whisper.RuntimePreference,
                }, vad, $"t{threads}");

            case "whisper.initialprompt":
                var prompt = value.GetString();
                return (new WhisperConfig
                {
                    Model = whisper.Model,
                    Language = whisper.Language,
                    BeamSize = whisper.BeamSize,
                    Temperature = whisper.Temperature,
                    InitialPrompt = prompt,
                    Threads = whisper.Threads,
                    RuntimePreference = whisper.RuntimePreference,
                }, vad, null);

            case "whisper.runtimepreference":
                var runtimePref = Enum.Parse<RuntimePreference>(value.GetString()!, ignoreCase: true);
                return (new WhisperConfig
                {
                    Model = whisper.Model,
                    Language = whisper.Language,
                    BeamSize = whisper.BeamSize,
                    Temperature = whisper.Temperature,
                    InitialPrompt = whisper.InitialPrompt,
                    Threads = whisper.Threads,
                    RuntimePreference = runtimePref,
                }, vad, runtimePref == RuntimePreference.Cpu ? "cpu" : "gpu");

            case "vad.enabled":
                var vadEnabled = value.GetBoolean();
                return (whisper, vad with { Enabled = vadEnabled },
                    vadEnabled ? "vad" : "novad");

            case "vad.threshold":
                var threshold = value.GetSingle();
                return (whisper, vad with { Threshold = threshold }, $"thr{threshold:F2}");

            case "vad.speechpadms":
                var padMs = value.GetInt32();
                return (whisper, vad with { SpeechPadMs = padMs }, $"pad{padMs}");

            case "vad.minsilencedurationms":
                var silMs = value.GetInt32();
                return (whisper, vad with { MinSilenceDurationMs = silMs }, $"sil{silMs}");

            case "vad.minspeechdurationms":
                var speechMs = value.GetInt32();
                return (whisper, vad with { MinSpeechDurationMs = speechMs }, $"spch{speechMs}");

            case "vad.intersegmentsilencems":
                var gapMs = value.GetInt32();
                return (whisper, vad with { InterSegmentSilenceMs = gapMs }, $"gap{gapMs}");

            default:
                throw new InvalidOperationException(
                    $"Unknown sweep parameter path: '{path}'. " +
                    $"Valid paths: whisper.model, whisper.beamSize, whisper.temperature, " +
                    $"whisper.language, whisper.threads, whisper.initialPrompt, whisper.runtimePreference, " +
                    $"vad.enabled, vad.threshold, vad.speechPadMs, vad.minSilenceDurationMs, " +
                    $"vad.minSpeechDurationMs, vad.interSegmentSilenceMs");
        }
    }

    /// <summary>Generates all combinations of indices for the given axis lengths.</summary>
    private static List<int[]> CartesianProduct(int[] axisLengths)
    {
        var results = new List<int[]>();
        var current = new int[axisLengths.Length];

        void Generate(int depth)
        {
            if (depth == axisLengths.Length)
            {
                results.Add((int[])current.Clone());
                return;
            }

            for (int i = 0; i < axisLengths[depth]; i++)
            {
                current[depth] = i;
                Generate(depth + 1);
            }
        }

        Generate(0);
        return results;
    }
}
