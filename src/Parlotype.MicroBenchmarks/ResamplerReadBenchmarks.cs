using BenchmarkDotNet.Attributes;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Parlotype.MicroBenchmarks;

/// <summary>
/// Follow-up to finding P1 (live `dotnet-counters` verification, 2026-07-14):
/// NAudio's resampler chain (BufferedWaveProvider → ToSampleProvider →
/// WdlResamplingSampleProvider → ToMono) allocates internally in proportion to
/// the count REQUESTED from <c>ISampleProvider.Read</c>, on every call — the
/// dominant allocator while recording. One benchmark op = one simulated 100 ms
/// WASAPI callback (38,400 bytes of 48 kHz stereo float32).
///
/// ReadCount values: 38,400 = pre-rework behaviour (`new float[BytesRecorded]`,
/// read `.Length`); 65,536 = the regressed pooled version (ArrayPool bucket
/// rounding inflated the request); 3,200 = the fix (2× the expected 1,600
/// resampled output samples). See memory/knowledge/naudio-resampler-read-cost.md.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ResamplerReadBenchmarks
{
    private const int BytesPerCallback = 38_400; // 100 ms @ 48 kHz stereo float32

    [Params(38_400, 65_536, 3_200)]
    public int ReadCount { get; set; }

    private byte[] _srcBytes = [];
    private float[] _readBuffer = [];
    private BufferedWaveProvider _bufferedProvider = null!;
    private ISampleProvider _resampler = null!;

    [GlobalSetup]
    public void Setup()
    {
        _srcBytes = new byte[BytesPerCallback];
        var rng = new Random(1);
        for (int i = 0; i < BytesPerCallback / 4; i++)
            BitConverter.TryWriteBytes(_srcBytes.AsSpan(i * 4), (float)(rng.NextDouble() - 0.5) * 0.5f);

        _bufferedProvider = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2))
        {
            DiscardOnBufferOverflow = true,
            ReadFully = false,
        };
        _resampler = new WdlResamplingSampleProvider(_bufferedProvider.ToSampleProvider(), 16_000);
        if (_resampler.WaveFormat.Channels > 1)
            _resampler = _resampler.ToMono();

        _readBuffer = new float[ReadCount];
    }

    [Benchmark]
    public int CaptureCallback()
    {
        _bufferedProvider.AddSamples(_srcBytes, 0, BytesPerCallback);
        return _resampler.Read(_readBuffer, 0, ReadCount);
    }
}
