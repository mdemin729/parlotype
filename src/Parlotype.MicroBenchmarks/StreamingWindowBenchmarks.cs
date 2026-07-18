using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace Parlotype.MicroBenchmarks;

/// <summary>
/// Finding P3: streaming mode extracts fixed 3 s windows from the sample
/// buffer. Compares <c>GetRange().ToArray()</c> (intermediate List + second
/// copy) against a single span-slice copy. Both variants include the
/// subsequent <c>RemoveRange</c> shift, as in production.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class StreamingWindowBenchmarks
{
    private const int WindowSamples = 16_000 * 3;
    private const int BufferSamples = 16_000 * 9; // 3 windows per invocation

    private float[] _source = [];
    private List<float> _buffer = [];

    [GlobalSetup]
    public void Setup() => _source = SyntheticAudio.Generate(BufferSamples);

    [IterationSetup]
    public void FillBuffer() => _buffer = [.. _source];

    [Benchmark(Baseline = true)]
    public int GetRangeToArray()
    {
        int extracted = 0;
        while (_buffer.Count >= WindowSamples)
        {
            var window = _buffer.GetRange(0, WindowSamples).ToArray();
            _buffer.RemoveRange(0, WindowSamples);
            extracted += window.Length;
        }

        return extracted;
    }

    [Benchmark]
    public int SpanSliceCopy()
    {
        int extracted = 0;
        while (_buffer.Count >= WindowSamples)
        {
            var window = CollectionsMarshal.AsSpan(_buffer)[..WindowSamples].ToArray();
            _buffer.RemoveRange(0, WindowSamples);
            extracted += window.Length;
        }

        return extracted;
    }
}
