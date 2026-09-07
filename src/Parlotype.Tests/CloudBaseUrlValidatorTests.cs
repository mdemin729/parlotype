using Parlotype.Core.Speech;
using Xunit;

namespace Parlotype.Tests;

public sealed class CloudBaseUrlValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://api.openai.com/v1")]
    [InlineData("https://api.groq.com/openai/v1")]
    [InlineData("http://localhost:1234/v1")]
    [InlineData("http://127.0.0.1:8080/v1")]
    [InlineData("http://[::1]:1234/v1")]
    [InlineData("  https://api.x.ai/v1  ")]
    public void TryValidate_Accepts(string? baseUrl)
    {
        Assert.True(CloudBaseUrlValidator.TryValidate(baseUrl, out var failure));
        Assert.Null(failure);
    }

    [Theory]
    [InlineData("http://api.openai.com/v1")]      // plaintext to a remote host
    [InlineData("http://192.168.1.10:1234/v1")]    // LAN is not loopback
    [InlineData("ftp://example.com/v1")]           // wrong scheme
    [InlineData("file:///C:/temp")]                // wrong scheme
    [InlineData("not a url")]
    [InlineData("api.openai.com/v1")]              // relative / schemeless
    public void TryValidate_Rejects(string baseUrl)
    {
        Assert.False(CloudBaseUrlValidator.TryValidate(baseUrl, out var failure));
        Assert.NotNull(failure);
        Assert.False(string.IsNullOrWhiteSpace(failure.Value.Description));
    }

    [Fact]
    public void TryValidate_RejectsUnsupportedScheme_CarriesTheScheme()
    {
        // The scheme is data on the failure, not a substring of a sentence, so
        // the UI can put it wherever its language wants it (ADR-064 amendment).
        Assert.False(CloudBaseUrlValidator.TryValidate("ftp://example.com/v1", out var failure));
        Assert.Equal(CloudBaseUrlError.UnsupportedScheme, failure!.Value.Error);
        Assert.Equal("ftp", failure.Value.Scheme);
    }

    [Fact]
    public void TryValidate_RejectsRemoteHttp_WithActionableReason()
    {
        Assert.False(CloudBaseUrlValidator.TryValidate("http://api.example.com/v1", out var failure));
        Assert.Equal(CloudBaseUrlError.PlainHttpNotLoopback, failure!.Value.Error);
        Assert.Contains("localhost", failure.Value.Description);
    }
}
