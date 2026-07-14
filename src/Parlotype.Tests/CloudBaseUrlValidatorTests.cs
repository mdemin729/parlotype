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
        Assert.True(CloudBaseUrlValidator.TryValidate(baseUrl, out var error));
        Assert.Null(error);
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
        Assert.False(CloudBaseUrlValidator.TryValidate(baseUrl, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryValidate_RejectsRemoteHttp_WithActionableReason()
    {
        Assert.False(CloudBaseUrlValidator.TryValidate("http://api.example.com/v1", out var error));
        Assert.Contains("localhost", error);
    }
}
