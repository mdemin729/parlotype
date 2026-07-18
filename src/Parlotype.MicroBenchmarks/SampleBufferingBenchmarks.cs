using BenchmarkDotNet.Attributes;

namespace Parlotype.MicroBenchmarks;

/// <summary>
/// Finding P2: <c>AudioPipelineService</c> accumulates capture chunks into a
/// <c>List&lt;float&gt;</c>. Compares the original per-sample <c>Add</c> loop
/// against span-based <c>AddRange</c>, with and without pre-sizing to the 30 s
/// batch cap (480 000 samples). Simulates 30 s of 100 ms capture chunks.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class SampleBufferingBenchmarks
{
    private const int ChunkSamples = 1_600;              // 100 ms at 16 kHz
    private const int ChunkCount = 300;                  // 30 s total
    private const int MaxBatchBufferSamples = 16_000 * 30;

    private float[] _chunk = [];

    [GlobalSetup]
    public void Setup() => _chunk = SyntheticAudio.Generate(ChunkSamples);

    [Benchmark(Baseline = true)]
    public int PerSampleAdd()
    {
        var buffer = new List<float>();
        for (int c = 0; c < ChunkCount; c++)
        {
            foreach (var s in _chunk.AsSpan())
                buffer.Add(s);
        }

        return buffer.Count;
    }

    [Benchmark]
    public int SpanAddRange()
    {
        var buffer = new List<float>();
        for (int c = 0; c < ChunkCount; c++)
            buffer.AddRange(_chunk.AsSpan());

        return buffer.Count;
    }

    [Benchmark]
    public int PreSizedSpanAddRange()
    {
        var buffer = new List<float>();
        buffer.EnsureCapacity(MaxBatchBufferSamples);
        for (int c = 0; c < ChunkCount; c++)
            buffer.AddRange(_chunk.AsSpan());

        return buffer.Count;
    }
}
