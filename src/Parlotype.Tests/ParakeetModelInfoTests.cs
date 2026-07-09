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
    public void FileNames_Int8_ContainsFourFiles_LargestFirst()
    {
        var model = ParakeetModelInfo.TdtV3Int8;

        Assert.Equal(
            [model.EncoderFileName, model.DecoderFileName, model.JoinerFileName, model.TokensFileName],
            model.FileNames);
        Assert.Equal(4, model.FileNames.Count);
        Assert.Null(model.EncoderWeightsFileName);
    }

    [Fact]
    public void FileNames_Fp32_IncludesExternalEncoderWeights()
    {
        var model = ParakeetModelInfo.TdtV3Fp32;

        // The fp32 encoder is a small graph + ONNX external-data weights file;
        // both must download into the same directory or onnxruntime cannot
        // resolve the relative reference.
        Assert.Equal("encoder.weights", model.EncoderWeightsFileName);
        Assert.Equal(
            [model.EncoderWeightsFileName!, model.EncoderFileName, model.DecoderFileName, model.JoinerFileName, model.TokensFileName],
            model.FileNames);
        Assert.Equal(5, model.FileNames.Count);
    }

    [Fact]
    public void GetById_ResolvesFp32Variant()
    {
        Assert.Equal(ParakeetModelInfo.TdtV3Fp32, ParakeetModelInfo.GetById("parakeet-tdt-0.6b-v3-fp32"));
    }

    [Fact]
    public void Default_IsInt8_AndListedFirst()
    {
        Assert.Equal(ParakeetModelInfo.TdtV3Int8, ParakeetModelInfo.Default);
        Assert.Equal(ParakeetModelInfo.TdtV3Int8, ParakeetModelInfo.All[0]);
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
