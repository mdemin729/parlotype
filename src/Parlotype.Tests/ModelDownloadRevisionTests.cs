using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Speech;
using Parlotype.Core.TextProcessing;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// Every model download resolves against one upstream commit, never a branch
/// (ADR-076). A branch lets upstream replace the bytes behind a catalog
/// SHA-256, which is how every new Parakeet download broke on 2026-11-07.
/// </summary>
public sealed partial class ModelDownloadRevisionTests : IDisposable
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    [GeneratedRegex("^https://huggingface\\.co/[^/]+/[^/]+/resolve/[0-9a-f]{40}/.+$")]
    private static partial Regex PinnedResolveUrl();

    private readonly string _dir = Directory.CreateTempSubdirectory("parlotype-revision-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    // ----- URL builder ---------------------------------------------------------

    [Fact]
    public void Resolve_BuildsUrlFromRepoRevisionAndPath()
    {
        var url = HuggingFaceUrl.Resolve("owner/repo", Commit, "dir/model.onnx");

        Assert.Equal($"https://huggingface.co/owner/repo/resolve/{Commit}/dir/model.onnx", url);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("v3")]
    [InlineData("")]
    [InlineData("0123456789abcdef")]                             // abbreviated SHA
    [InlineData("0123456789ABCDEF0123456789ABCDEF01234567")]     // uppercase
    [InlineData("0123456789abcdef0123456789abcdef0123456g")]     // not hex
    [InlineData("0123456789abcdef0123456789abcdef012345678")]    // 41 characters
    public void Resolve_RejectsAnythingButAFullCommitSha(string revision)
    {
        Assert.False(HuggingFaceUrl.IsCommitSha(revision));
        Assert.Throws<ArgumentException>(() => HuggingFaceUrl.Resolve("owner/repo", revision, "model.onnx"));
    }

    [Fact]
    public void IsCommitSha_AcceptsFullLowercaseSha_RejectsNull()
    {
        Assert.True(HuggingFaceUrl.IsCommitSha(Commit));
        Assert.False(HuggingFaceUrl.IsCommitSha(null));
    }

    // ----- catalogs ------------------------------------------------------------

    [Fact]
    public void ParakeetCatalog_EveryFileUrl_IsPinnedToTheModelRevision()
    {
        Assert.All(ParakeetModelInfo.All, model =>
            Assert.All(model.FileNames, fileName =>
                Assert.Equal(
                    $"https://huggingface.co/{model.HuggingFaceRepo}/resolve/{model.HuggingFaceRevision}/{fileName}",
                    AssertPinned(model.GetDownloadUrl(fileName)))));
    }

    /// <summary>
    /// The revision and the digests are one pair: upstream's <c>main</c> has
    /// carried different encoder, decoder and joiner bytes since 2026-11-07.
    /// Whoever moves the revision must refresh the digests from the tree API
    /// at the new commit, and this test is where they find that out.
    /// </summary>
    [Fact]
    public void ParakeetDefault_RevisionAndEncoderDigest_AreTheVerifiedPair()
    {
        var model = ParakeetModelInfo.TdtV3Int8;

        Assert.Equal(
            "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/2bda32ec70b097a55adaa07d9a7173915b43cc78/encoder.int8.onnx",
            model.GetDownloadUrl(model.EncoderFileName));
        Assert.Equal(
            "acfc2b4456377e15d04f0243af540b7fe7c992f8d898d751cf134c3a55fd2247",
            model.GetSha256(model.EncoderFileName));
    }

    [Fact]
    public void GemmaCatalog_BothFileUrls_ArePinnedToTheModelRevision()
    {
        Assert.All(Gemma4ModelInfo.All, model =>
            Assert.All(new[] { model.GgufFileName, model.MmprojFileName }, fileName =>
                Assert.Equal(
                    $"https://huggingface.co/{model.HuggingFaceRepo}/resolve/{model.HuggingFaceRevision}/{fileName}",
                    AssertPinned(model.GetDownloadUrl(fileName)))));
    }

    [Fact]
    public void SpeechCleanupCatalog_EveryModelUrl_IsPinnedToTheModelRevision()
    {
        Assert.All(SpeechCleanupModelInfo.All, model =>
            Assert.Equal(
                $"https://huggingface.co/{model.HuggingFaceRepo}/resolve/{model.HuggingFaceRevision}/{model.GgufFileName}",
                AssertPinned(model.DownloadUrl)));
    }

    [Fact]
    public void WhisperCatalog_EveryModelUrl_IsPinnedToTheCatalogRevision()
    {
        var prefix =
            $"https://huggingface.co/{WhisperModelInfo.HuggingFaceRepo}/resolve/{WhisperModelInfo.HuggingFaceRevision}/classic/ggml-";

        Assert.All(WhisperModelInfo.GetAll(), model =>
        {
            var url = AssertPinned(HttpModelDownloadService.GetDownloadUrl(model.Type));
            Assert.StartsWith(prefix, url);
            Assert.EndsWith(".bin", url);
        });
    }

    private static string AssertPinned(string url)
    {
        Assert.Matches(PinnedResolveUrl(), url);
        return url;
    }

    // ----- on the wire ---------------------------------------------------------

    private sealed class RecordingHandler(byte[] payload) : HttpMessageHandler
    {
        public ConcurrentQueue<(HttpMethod Method, string Url)> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue((request.Method, request.RequestUri!.AbsoluteUri));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            });
        }
    }

    [Fact]
    public async Task ParakeetDownload_RequestsEveryFileAtThePinnedRevision()
    {
        var payload = "parakeet bytes"u8.ToArray();
        var sha = Convert.ToHexStringLower(SHA256.HashData(payload));
        var catalog = ParakeetModelInfo.TdtV3Int8;

        // A rooted ModelId makes GetModelDirectory() resolve to the temp
        // directory instead of the user's model cache. The assert keeps the
        // test from writing anywhere else should that ever stop being true.
        var model = catalog with
        {
            ModelId = _dir,
            FileSha256 = catalog.FileNames.ToDictionary(f => f, _ => sha),
        };
        Assert.Equal(_dir, model.GetModelDirectory());

        var handler = new RecordingHandler(payload);
        var service = new ParakeetModelDownloadService(
            new HttpClient(handler), NullLogger<ParakeetModelDownloadService>.Instance);

        await service.DownloadModelAsync(model, progress: null, CancellationToken.None);

        var expectedPrefix =
            $"https://huggingface.co/{catalog.HuggingFaceRepo}/resolve/{catalog.HuggingFaceRevision}/";
        Assert.NotEmpty(handler.Requests);
        Assert.All(handler.Requests, r => Assert.StartsWith(expectedPrefix, r.Url));
        Assert.Equal(
            catalog.FileNames.Select(f => expectedPrefix + f).Order(),
            handler.Requests.Where(r => r.Method == HttpMethod.Get).Select(r => r.Url).Order());
        Assert.All(catalog.FileNames, f => Assert.True(File.Exists(Path.Combine(_dir, f))));
    }
}
