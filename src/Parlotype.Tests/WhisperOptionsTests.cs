using Parlotype.Core.Speech;
using Xunit;

namespace Parlotype.Tests;

public class WhisperOptionsTests
{
    [Fact]
    public void WhisperOptions_RuntimePreference_DefaultsToAuto()
    {
        var options = new WhisperOptions();
        Assert.Equal(RuntimePreference.Auto, options.RuntimePreference);
    }
}
