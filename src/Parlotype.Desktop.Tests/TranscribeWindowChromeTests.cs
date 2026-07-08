using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Parlotype.Core.Settings;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Frameless-chrome behaviour of the compact Transcribe widget (ADR-040):
/// no system decorations, grip-strip layout, Esc / ✕ hide to tray, and
/// position persistence with off-screen fallback.
/// </summary>
public class TranscribeWindowChromeTests
{
    private static TranscribeWindow CreateWindow() => new()
    {
        DataContext = new TranscribeViewModel(new MockWindowManager()),
    };

    [AvaloniaFact]
    public void Window_HasNoSystemDecorations_AndFixedCompactSize()
    {
        var window = CreateWindow();

        Assert.Equal(WindowDecorations.None, window.WindowDecorations);
        Assert.False(window.CanResize);
        Assert.True(window.Topmost);
        Assert.Equal(172, window.Width);
        // No relationship wired ⇒ no language strip ⇒ compact height.
        Assert.Equal(88, window.Height);

        window.Close();
    }

    [AvaloniaFact]
    public async Task Window_ResizesWithLanguageStrip_PerEngine()
    {
        // Seed the persisted engine: the VM constructor kicks the relationship's
        // InitializeAsync, which applies the engine read from settings.
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, Core.Speech.SpeechEngine.Whisper.ToString());
        var relationship = new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService());

        var vm = new TranscribeViewModel(new MockWindowManager(), relationship: relationship);
        await vm.RelationshipInitialization;
        var window = new TranscribeWindow { DataContext = vm };

        // Whisper offers language choices ⇒ strip shown ⇒ full height.
        Assert.Equal(118, window.Height);

        // Parakeet has no language choices ⇒ strip hidden ⇒ window compacts.
        relationship.SetEngine(Core.Speech.SpeechEngine.Parakeet);
        Assert.Equal(88, window.Height);

        window.Close();
    }

    [AvaloniaFact]
    public void GripZone_And_CloseButton_ArePresent()
    {
        var window = CreateWindow();
        window.Show();

        Assert.NotNull(window.FindControl<Border>("GripZone"));
        Assert.NotNull(window.FindControl<Button>("CloseButton"));

        window.Close();
    }

    [AvaloniaFact]
    public void Escape_HidesWindow()
    {
        var window = CreateWindow();
        window.Show();
        Assert.True(window.IsVisible);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.False(window.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task RestorePosition_AppliesSavedOnScreenPosition()
    {
        var windowState = new MockWindowStateService();
        await windowState.SetAsync(WindowStateKeys.TranscribeWindowPosition, new WindowPosition(300, 200));

        var window = CreateWindow();
        await window.RestorePositionAsync(windowState);
        window.Show();

        Assert.Equal(WindowStartupLocation.Manual, window.WindowStartupLocation);
        Assert.Equal(new PixelPoint(300, 200), window.Position);

        window.Close();
    }

    [AvaloniaFact]
    public async Task RestorePosition_WithoutSavedValues_KeepsCenterScreenDefault()
    {
        var window = CreateWindow();
        await window.RestorePositionAsync(new MockWindowStateService());

        Assert.Equal(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);

        window.Close();
    }

    [AvaloniaFact]
    public async Task RestorePosition_WithOffScreenSavedPosition_KeepsCenterScreenDefault()
    {
        // Headless reports one virtual screen at (0,0,1920,1280) — anything far
        // outside it exercises the same "monitor unplugged" fallback path.
        var windowState = new MockWindowStateService();
        await windowState.SetAsync(WindowStateKeys.TranscribeWindowPosition, new WindowPosition(50_000, 50_000));

        var window = CreateWindow();
        await window.RestorePositionAsync(windowState);
        window.Show();

        Assert.Equal(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);

        window.Close();
    }

    [AvaloniaFact]
    public async Task SavePosition_PersistsCurrentPosition()
    {
        var windowState = new MockWindowStateService();
        var window = CreateWindow();
        await window.RestorePositionAsync(windowState);
        window.Show();

        window.Position = new PixelPoint(123, 45);
        await window.SavePositionAsync();

        Assert.Equal(new WindowPosition(123, 45),
            await windowState.GetAsync<WindowPosition?>(WindowStateKeys.TranscribeWindowPosition));

        window.Close();
    }

    [AvaloniaFact]
    public async Task SavePosition_WithoutRestore_IsNoOp()
    {
        var window = CreateWindow();
        window.Show();

        // Never wired to a settings store (legacy/test host) — must not throw.
        await window.SavePositionAsync();

        window.Close();
    }
}
