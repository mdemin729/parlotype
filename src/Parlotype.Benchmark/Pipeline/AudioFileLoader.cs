using System.Diagnostics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Parlotype.Benchmark.Pipeline;

/// <summary>Loads audio files and resamples to 16 kHz mono float samples.
/// Supports WAV natively and FLAC via FFmpeg conversion.</summary>
public static class AudioFileLoader
{
    private const int TargetSampleRate = 16_000;

    /// <summary>
    /// Loads an audio file and returns 16 kHz mono float samples with the audio duration.
    /// WAV files are loaded directly. FLAC files are converted to WAV via FFmpeg first.
    /// </summary>
    /// <param name="filePath">Path to the audio file (.wav or .flac).</param>
    /// <param name="cacheDir">Optional directory for caching converted WAV files. Defaults to a <c>.cache</c> folder next to the source file.</param>
    /// <returns>Tuple of float samples ([-1, 1] normalized) and duration in seconds.</returns>
    public static (float[] Samples, double DurationSeconds) Load(string filePath, string? cacheDir = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Audio file not found: {filePath}", filePath);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        var wavPath = extension switch
        {
            ".wav" => filePath,
            ".flac" => ConvertToWav(filePath, cacheDir),
            _ => throw new NotSupportedException($"Unsupported audio format: {extension}. Supported formats: .wav, .flac"),
        };

        return LoadWavInternal(wavPath);
    }

    /// <summary>Loads a WAV file directly (backward-compatible convenience method).</summary>
    public static (float[] Samples, double DurationSeconds) LoadWav(string filePath) => Load(filePath);

    /// <summary>
    /// Converts a FLAC file to 16 kHz mono 16-bit WAV using FFmpeg.
    /// Returns the cached WAV path, skipping conversion if it already exists.
    /// </summary>
    private static string ConvertToWav(string flacPath, string? cacheDir)
    {
        cacheDir ??= Path.Combine(Path.GetDirectoryName(flacPath)!, ".cache");
        Directory.CreateDirectory(cacheDir);

        var wavFileName = Path.GetFileNameWithoutExtension(flacPath) + ".wav";
        var wavPath = Path.Combine(cacheDir, wavFileName);

        // Skip if already cached and newer than source
        if (File.Exists(wavPath) && File.GetLastWriteTimeUtc(wavPath) >= File.GetLastWriteTimeUtc(flacPath))
            return wavPath;

        EnsureFfmpegAvailable();

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                ArgumentList =
                {
                    "-y",                   // overwrite output
                    "-i", flacPath,         // input FLAC
                    "-acodec", "pcm_s16le", // 16-bit PCM
                    "-ar", "16000",         // 16 kHz
                    "-ac", "1",             // mono
                    wavPath,                // output WAV
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg conversion failed (exit code {process.ExitCode}): {stderr}");

        return wavPath;
    }

    /// <summary>Checks that FFmpeg is available on the system PATH.</summary>
    private static void EnsureFfmpegAvailable()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    ArgumentList = { "-version" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "FFmpeg is required for FLAC support but was not found on PATH. " +
                "Install FFmpeg: https://ffmpeg.org/download.html", ex);
        }
    }

    private static (float[] Samples, double DurationSeconds) LoadWavInternal(string filePath)
    {
        using var reader = new WaveFileReader(filePath);
        var sampleProvider = reader.ToSampleProvider();

        // Convert to mono if multichannel
        if (sampleProvider.WaveFormat.Channels > 1)
            sampleProvider = sampleProvider.ToMono();

        // Resample to 16 kHz if needed
        ISampleProvider finalProvider = sampleProvider.WaveFormat.SampleRate != TargetSampleRate
            ? new WdlResamplingSampleProvider(sampleProvider, TargetSampleRate)
            : sampleProvider;

        var samples = ReadAllSamples(finalProvider);
        var durationSeconds = (double)samples.Length / TargetSampleRate;

        return (samples, durationSeconds);
    }

    private static float[] ReadAllSamples(ISampleProvider provider)
    {
        var samples = new List<float>();
        var buffer = new float[4096];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            samples.AddRange(buffer.AsSpan(0, read).ToArray());
        }
        return samples.ToArray();
    }
}
