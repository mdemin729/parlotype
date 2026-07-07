using Parlotype.Core.Speech;
using Xunit;

namespace Parlotype.Tests;

public class ParakeetModelInfoTests
{
    [Fact]
    public void Catalog_ContainsDefault()
    {
        Assert.Contains(ParakeetModelInfo.Default, ParakeetModelInfo.All);
        Assert.Equal(ParakeetModelInfo.TdtV3Int8, ParakeetModelInfo.Default);
    }

    [Fact]
    public void GetById_ResolvesKnownId()
    {
        var model = ParakeetModelInfo.GetById("parakeet-tdt-0.6b-v3-int8");

        Assert.NotNull(model);
        Assert.Equal(ParakeetModelInfo.TdtV3Int8, model);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-model")]
    public void GetById_ReturnsNullForUnknownIds(string? id)
    {
        Assert.Null(ParakeetModelInfo.GetById(id));
    }

    [Fact]
    public void FileNames_ContainsAllFourFiles_LargestFirst()
    {
        var model = ParakeetModelInfo.TdtV3Int8;

        Assert.Equal(
            [model.EncoderFileName, model.DecoderFileName, model.JoinerFileName, model.TokensFileName],
            model.FileNames);
        Assert.Equal(4, model.FileNames.Count);
    }

    [Fact]
    public void GetModelDirectory_NestsPerModelUnderSharedCache()
    {
        var dir = ParakeetModelInfo.TdtV3Int8.GetModelDirectory();

        // Generic upstream file names (encoder.int8.onnx …) collide across
        // models, so each model must get its own subdirectory.
        Assert.EndsWith(
            Path.Combine("parlotype", "models", ParakeetModelInfo.TdtV3Int8.ModelId),
            dir);
    }

    [Fact]
    public void ModelIds_AreUnique()
    {
        Assert.Equal(
            ParakeetModelInfo.All.Count,
            ParakeetModelInfo.All.Select(m => m.ModelId).Distinct().Count());
    }
}
