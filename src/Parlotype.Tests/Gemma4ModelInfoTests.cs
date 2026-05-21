using Parlotype.Core.Speech;
using Xunit;

namespace Parlotype.Tests;

public class Gemma4ModelInfoTests
{
    [Fact]
    public void All_ContainsFiveEntriesInExpectedOrder()
    {
        Assert.Collection(Gemma4ModelInfo.All,
            m => Assert.Equal("gemma-4-E2B-it-Q8_0", m.ModelId),
            m => Assert.Equal("gemma-4-E2B-it-bf16", m.ModelId),
            m => Assert.Equal("gemma-4-E4B-it-Q4_K_M", m.ModelId),
            m => Assert.Equal("gemma-4-E4B-it-Q8_0", m.ModelId),
            m => Assert.Equal("gemma-4-E4B-it-bf16", m.ModelId));
    }

    [Fact]
    public void Default_IsE4BQ4KM()
    {
        Assert.Equal("gemma-4-E4B-it-Q4_K_M", Gemma4ModelInfo.Default.ModelId);
        Assert.Equal(Gemma4Variant.E4B, Gemma4ModelInfo.Default.Variant);
        Assert.Equal(Gemma4Quant.Q4_K_M, Gemma4ModelInfo.Default.Quant);
    }

    [Theory]
    [InlineData("gemma-4-E2B-it-Q8_0")]
    [InlineData("gemma-4-E4B-it-bf16")]
    public void GetById_ReturnsMatchingEntry(string modelId)
    {
        var model = Gemma4ModelInfo.GetById(modelId);

        Assert.NotNull(model);
        Assert.Equal(modelId, model!.ModelId);
        Assert.EndsWith(".gguf", model.GgufFileName);
        Assert.StartsWith(modelId, model.GgufFileName);
        Assert.EndsWith(".gguf", model.MmprojFileName);
        Assert.False(string.IsNullOrWhiteSpace(model.HuggingFaceRepo));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("does-not-exist")]
    public void GetById_ReturnsNull_ForUnknownId(string? modelId)
    {
        Assert.Null(Gemma4ModelInfo.GetById(modelId));
    }

    [Fact]
    public void AllEntries_HaveDistinctIdsAndGgufFileNames()
    {
        Assert.Equal(Gemma4ModelInfo.All.Count, Gemma4ModelInfo.All.Select(m => m.ModelId).Distinct().Count());
        Assert.Equal(Gemma4ModelInfo.All.Count, Gemma4ModelInfo.All.Select(m => m.GgufFileName).Distinct().Count());
    }

    [Fact]
    public void E2B_HasNoQ4KM()
    {
        // The ggml-org E2B repo publishes only Q8_0 and BF16.
        Assert.DoesNotContain(Gemma4ModelInfo.All,
            m => m.Variant == Gemma4Variant.E2B && m.Quant == Gemma4Quant.Q4_K_M);
    }
}
