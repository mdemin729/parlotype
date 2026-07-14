using BenchmarkDotNet.Attributes;
using Parlotype.Platform.Speech;

namespace Parlotype.MicroBenchmarks;

/// <summary>
/// Finding P5: WAV encoding runs once per utterance on the cloud and llama.cpp
/// paths. Compares the frozen legacy encoder (MemoryStream + BinaryWriter +
/// ToArray) against the production <see cref="WavEncoder"/>.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class WavEncoderBenchmarks
{
    [Params(1, 10, 30)]
    public int Seconds { get; set; }

    private float[] _samples = [];

    [GlobalSetup]
    public void Setup() => _samples = SyntheticAudio.Generate(SyntheticAudio.SampleRate * Seconds);

    [Benchmark(Baseline = true)]
    public byte[] Legacy() => LegacyWavEncoder.Encode(_samples, SyntheticAudio.SampleRate);

    [Benchmark]
    public byte[] Current() => WavEncoder.Encode(_samples, SyntheticAudio.SampleRate);
}
