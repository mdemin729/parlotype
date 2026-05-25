using Parlotype.Core.Speech;
using Xunit;

namespace Parlotype.Tests;

public class WhisperModelInfoTests
{
    [Theory]
    [InlineData(WhisperModelType.Tiny, true)]
    [InlineData(WhisperModelType.TinyEn, false)]
    [InlineData(WhisperModelType.Base, true)]
    [InlineData(WhisperModelType.BaseEn, false)]
    [InlineData(WhisperModelType.Small, true)]
    [InlineData(WhisperModelType.SmallEn, false)]
    [InlineData(WhisperModelType.Medium, true)]
    [InlineData(WhisperModelType.MediumEn, false)]
    [InlineData(WhisperModelType.LargeV1, true)]
    [InlineData(WhisperModelType.LargeV2, true)]
    [InlineData(WhisperModelType.LargeV3, true)]
    [InlineData(WhisperModelType.LargeV3Turbo, false)]
    public void SupportsTranslation_MatchesModelCapability(WhisperModelType type, bool expected)
    {
        Assert.Equal(expected, WhisperModelInfo.Get(type).SupportsTranslation);
    }

    [Fact]
    public void GetAll_CoversEveryModelType()
    {
        var covered = WhisperModelInfo.GetAll().Select(m => m.Type).ToHashSet();
        Assert.True(Enum.GetValues<WhisperModelType>().All(covered.Contains));
    }
}
