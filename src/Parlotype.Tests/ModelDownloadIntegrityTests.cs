using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// SHA-256 model-download verification (security audit 2026-07-11, S2):
/// the streaming downloader must reject tampered bytes before anything
/// reaches the destination path, and every catalog entry must actually
/// carry a digest so verification is never silently skipped.
/// </summary>
public sealed partial class ModelDownloadIntegrityTests : IDisposable
{
    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex LowercaseSha256();

    private readonly string _dir = Directory.CreateTempSubdirectory("parlotype-integrity-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private sealed class StubHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            });
    }

    private StreamingFileDownloader CreateDownloader(byte[] payload) =>
        new(new HttpClient(new StubHandler(payload)), NullLogger<StreamingFileDownloader>.Instance);

    [Fact]
    public async Task DownloadAsync_MatchingSha256_MovesFileIntoPlace()
    {
        var payload = "model bytes"u8.ToArray();
        var expected = Convert.ToHexStringLower(SHA256.HashData(payload));
        var dest = Path.Combine(_dir, "model.bin");

        await CreateDownloader(payload).DownloadAsync(
            "https://example.test/model.bin", dest, progress: null,
            CancellationToken.None, expectedSha256: expected);

        Assert.Equal(payload, await File.ReadAllBytesAsync(dest));
        Assert.False(File.Exists(dest + ".tmp"));
    }

    [Fact]
    public async Task DownloadAsync_Sha256Mismatch_ThrowsAndLeavesNoFiles()
    {
        var payload = "tampered bytes"u8.ToArray();
        var dest = Path.Combine(_dir, "model.bin");

        var ex = await Assert.ThrowsAsync<ModelIntegrityException>(() =>
            CreateDownloader(payload).DownloadAsync(
                "https://example.test/model.bin", dest, progress: null,
                CancellationToken.None, expectedSha256: new string('a', 64)));

        Assert.Equal("model.bin", ex.FileName);
        Assert.Equal(new string('a', 64), ex.ExpectedSha256);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(payload)), ex.ActualSha256);
        Assert.False(File.Exists(dest), "destination must never appear on mismatch");
        Assert.False(File.Exists(dest + ".tmp"), "temp file must be cleaned up on mismatch");
    }

    [Fact]
    public async Task DownloadAsync_Sha256ComparisonIsCaseInsensitive()
    {
        var payload = "case bytes"u8.ToArray();
        var expectedUpper = Convert.ToHexString(SHA256.HashData(payload)); // uppercase
        var dest = Path.Combine(_dir, "model.bin");

        await CreateDownloader(payload).DownloadAsync(
            "https://example.test/model.bin", dest, progress: null,
            CancellationToken.None, expectedSha256: expectedUpper);

        Assert.True(File.Exists(dest));
    }

    [Fact]
    public async Task DownloadAsync_NoExpectedSha_StillDownloads()
    {
        var payload = "unverified bytes"u8.ToArray();
        var dest = Path.Combine(_dir, "model.bin");

        await CreateDownloader(payload).DownloadAsync(
            "https://example.test/model.bin", dest, progress: null, CancellationToken.None);

        Assert.Equal(payload, await File.ReadAllBytesAsync(dest));
    }

    // ----- catalog completeness ------------------------------------------------

    [Fact]
    public void WhisperCatalog_EveryModel_HasLowercaseSha256()
    {
        Assert.All(WhisperModelInfo.GetAll(), m =>
            Assert.Matches(LowercaseSha256(), m.Sha256));
    }

    [Fact]
    public void ParakeetCatalog_EveryFile_HasLowercaseSha256()
    {
        Assert.All(ParakeetModelInfo.All, model =>
            Assert.All(model.FileNames, fileName =>
            {
                var sha = model.GetSha256(fileName);
                Assert.NotNull(sha);
                Assert.Matches(LowercaseSha256(), sha);
            }));
    }

    [Fact]
    public void GemmaCatalog_EveryModel_HasLowercaseSha256ForBothFiles()
    {
        Assert.All(Gemma4ModelInfo.All, model =>
        {
            Assert.Matches(LowercaseSha256(), model.GetSha256(model.GgufFileName)!);
            Assert.Matches(LowercaseSha256(), model.GetSha256(model.MmprojFileName)!);
        });
    }
}
