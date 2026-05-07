using Parlotype.Core.Audio;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockAudioLevelProvider : IAudioLevelProvider
{
    public float CurrentLevel { get; set; }

    public event EventHandler<AudioLevelEventArgs>? LevelChanged;

    public void RaiseLevelChanged(float level)
    {
        CurrentLevel = level;
        LevelChanged?.Invoke(this, new AudioLevelEventArgs { Level = level });
    }
}
