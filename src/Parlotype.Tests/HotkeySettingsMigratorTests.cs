using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;
using Xunit;

namespace Parlotype.Tests;

public class HotkeySettingsMigratorTests
{
    private sealed class FakeSettingsService : ISettingsService
    {
        public Dictionary<string, object?> Store { get; } = new();
        public List<string> WrittenKeys { get; } = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.TryGetValue(key, out var v) ? (T?)v : default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            Store[key] = value;
            WrittenKeys.Add(key);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Fresh_Install_Gets_The_Default_Set()
    {
        var settings = new FakeSettingsService();

        var bindings = await HotkeySettingsMigrator.LoadOrMigrateAsync(settings);

        Assert.Equal(DictationHotkeyDefaults.All, bindings);
    }

    [Fact]
    public async Task Fresh_Install_Persists_The_Default_Set()
    {
        var settings = new FakeSettingsService();

        await HotkeySettingsMigrator.LoadOrMigrateAsync(settings);

        var stored = await settings.GetAsync<List<string>>(SettingsKeys.HotkeyBindings);
        Assert.Equal(HotkeyBindingCodec.EncodeAll(DictationHotkeyDefaults.All), stored);
    }

    [Fact]
    public async Task Existing_Set_Is_Loaded_Verbatim()
    {
        var settings = new FakeSettingsService();
        var expected = new List<DictationHotkey> { DictationHotkeyDefaults.ChordFallback };
        await settings.SetAsync(SettingsKeys.HotkeyBindings, HotkeyBindingCodec.EncodeAll(expected));
        settings.WrittenKeys.Clear();

        var bindings = await HotkeySettingsMigrator.LoadOrMigrateAsync(settings);

        Assert.Equal(expected, bindings);
        Assert.Empty(settings.WrittenKeys); // nothing rewritten
    }

    [Fact]
    public async Task Legacy_Custom_Chord_Is_Preserved_As_The_Only_Binding()
    {
        // The user deliberately chose this, so handing them the new gesture
        // defaults on top would bind global keys they never asked for.
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.HotkeyModifiers, "Ctrl, Alt");
        await settings.SetAsync(SettingsKeys.HotkeyKey, "F9");
        await settings.SetAsync(SettingsKeys.ActivationMode, "Toggle");

        var bindings = await HotkeySettingsMigrator.LoadOrMigrateAsync(settings);

        var only = Assert.Single(bindings);
        Assert.Equal(HotkeyGestureKind.Chord, only.Gesture.Kind);
        Assert.Equal(new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "F9"), only.Gesture.Chord);
        Assert.Equal(ActivationMode.Toggle, only.Mode);
    }

    [Fact]
    public async Task Legacy_Custom_Chord_Defaults_To_PushToTalk_When_Mode_Is_Missing()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.HotkeyModifiers, "Ctrl");
        await settings.SetAsync(SettingsKeys.HotkeyKey, "F9");

        var bindings = await HotkeySettingsMigrator.LoadOrMigrateAsync(settings);

        Assert.Equal(ActivationMode.PushToTalk, Assert.Single(bindings).Mode);
    }

    [Fact]
    public async Task Legacy_Untouched_Default_Chord_Is_Replaced_By_The_New_Set()
    {
        // Ctrl+Shift+Space was what shipped, not what the user picked — and it
        // collides with parameter hints in Visual Studio and VS Code.
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.HotkeyModifiers, "Ctrl, Shift");
        await settings.SetAsync(SettingsKeys.HotkeyKey, "Space");
        await settings.SetAsync(SettingsKeys.ActivationMode, "PushToTalk");

        var bindings = await HotkeySettingsMigrator.LoadOrMigrateAsync(settings);

        Assert.Equal(DictationHotkeyDefaults.All, bindings);
    }

    [Fact]
    public async Task Migration_Does_Not_Rewrite_The_Legacy_Keys()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.HotkeyModifiers, "Ctrl, Alt");
        await settings.SetAsync(SettingsKeys.HotkeyKey, "F9");
        settings.WrittenKeys.Clear();

        await HotkeySettingsMigrator.LoadOrMigrateAsync(settings);

        Assert.Equal([SettingsKeys.HotkeyBindings], settings.WrittenKeys);
    }

    [Fact]
    public async Task Migration_Is_Idempotent()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.HotkeyModifiers, "Ctrl, Alt");
        await settings.SetAsync(SettingsKeys.HotkeyKey, "F9");

        var first = await HotkeySettingsMigrator.LoadOrMigrateAsync(settings);
        var second = await HotkeySettingsMigrator.LoadOrMigrateAsync(settings);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Unparseable_Stored_Set_Falls_Back_To_Migration()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.HotkeyBindings, new List<string> { "nonsense" });

        var bindings = await HotkeySettingsMigrator.LoadOrMigrateAsync(settings);

        Assert.Equal(DictationHotkeyDefaults.All, bindings);
    }

    [Fact]
    public async Task Invalid_Legacy_Chord_Falls_Back_To_Defaults()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.HotkeyModifiers, "None");
        await settings.SetAsync(SettingsKeys.HotkeyKey, "Space");

        var bindings = await HotkeySettingsMigrator.LoadOrMigrateAsync(settings);

        Assert.Equal(DictationHotkeyDefaults.All, bindings);
    }
}
