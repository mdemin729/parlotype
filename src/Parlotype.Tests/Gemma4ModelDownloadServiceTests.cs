using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public class Gemma4ModelDownloadServiceTests
{
    [Fact]
    public void GetGgufPath_IsUnderCacheDirectory_WithModelFileName()
    {
        var cacheDir = Gemma4ModelInfo.GetModelCacheDirectory();

        foreach (var model in Gemma4ModelInfo.All)
        {
            var ggufPath = Gemma4ModelDownloadService.GetGgufPath(model);
            Assert.StartsWith(cacheDir, ggufPath);
            Assert.EndsWith(model.GgufFileName, ggufPath);
        }
    }

    [Fact]
    public void GetMmprojPath_IsUnderCacheDirectory_WithMmprojFileName()
    {
        var cacheDir = Gemma4ModelInfo.GetModelCacheDirectory();

        foreach (var model in Gemma4ModelInfo.All)
        {
            var mmprojPath = Gemma4ModelDownloadService.GetMmprojPath(model);
            Assert.StartsWith(cacheDir, mmprojPath);
            Assert.EndsWith(model.MmprojFileName, mmprojPath);
        }
    }

    [Fact]
    public void GgufPaths_AreDistinctPerEntry()
    {
        var paths = Gemma4ModelInfo.All
            .Select(Gemma4ModelDownloadService.GetGgufPath)
            .ToList();

        Assert.Equal(paths.Count, paths.Distinct().Count());
    }
}
