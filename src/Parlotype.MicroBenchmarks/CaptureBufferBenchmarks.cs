using System.Buffers;
using BenchmarkDotNet.Attributes;

namespace Parlotype.MicroBenchmarks;

/// <summary>
/// Finding P1: the WASAPI capture callback allocated <c>new float[e.BytesRecorded]</c>
/// per callback — 38 400 floats (153.6 KB, Large Object Heap) for a typical
/// 100 ms chunk of 48 kHz stereo float32 audio. Compares that allocation with
/// an <see cref="ArrayPool{T}"/> rent/return cycle at realistic callback sizes.
/// One invocation simulates 10 s of capture (100 callbacks).
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CaptureBufferBenchmarks
{
    private const int Callbacks = 100;

    /// <summary>Bytes per callback: 48 kHz stereo float32 at 50 ms and 100 ms cadence.</summary>
    [Params(19_200, 38_400)]
    public int BytesRecorded { get; set; }

    [Benchmark(Baseline = true)]
    public float Allocate()
    {
        float sink = 0;
        for (int i = 0; i < Callbacks; i++)
        {
            var buffer = new float[BytesRecorded];
            buffer[0] = i;
            sink += buffer[0];
        }

        return sink;
    }

    [Benchmark]
    public float PoolRentReturn()
    {
        float sink = 0;
        for (int i = 0; i < Callbacks; i++)
        {
            var buffer = ArrayPool<float>.Shared.Rent(BytesRecorded);
            buffer[0] = i;
            sink += buffer[0];
            ArrayPool<float>.Shared.Return(buffer);
        }

        return sink;
    }
}
