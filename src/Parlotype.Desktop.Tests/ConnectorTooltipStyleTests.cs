using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// A tooltip's content is a logical child of the control it hangs off, so a
/// descendant selector like <c>Button.connector.on TextBlock</c> reaches into it
/// and repaints the tooltip's own text — white glyph copy on the tooltip's light
/// background, i.e. an invisible tooltip. The connector glyph therefore carries an
/// explicit <c>connectorGlyph</c> class and the colour styles target that, never a
/// bare descendant <c>TextBlock</c> (ADR-061).
///
/// <para>Each test hangs a probe <see cref="TextBlock"/> off the connector as its
/// tip and asserts the styles left its foreground alone. The tooltip is closed
/// again in a <c>finally</c>: an open tooltip leaves its show/close timer running
/// past the end of the test, which surfaces later as "Cannot get KeyValueStorage
/// on the idle test context" against whichever test happens to run next.</para>
/// </summary>
public class ConnectorTooltipStyleTests
{
    private static async Task<LanguageRelationshipViewModel> RelationshipAsync(bool paused)
    {
        var settings = new MockSettingsService();
        var ct = TestContext.Current.CancellationToken;
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Whisper.ToString(), ct);

        var relationship = new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService());
        await relationship.InitializeAsync(ct);
        relationship.ToggleTranslation();
        if (paused)
            relationship.SetWhisperModel(WhisperModelType.LargeV3Turbo);

        Assert.Equal(paused, relationship.IsTranslationPaused);
        Assert.Equal(paused ? ConnectorState.Paused : ConnectorState.On, relationship.Connector);
        return relationship;
    }

    private static void Settle() => Dispatcher.UIThread.RunJobs();

    /// <summary>
    /// Hangs a probe on <paramref name="target"/>, opens it, and asserts the view's
    /// styles did not repaint it. Always closes the tooltip and the window.
    /// </summary>
    private static void AssertTooltipKeepsItsOwnForeground(Window window, Control target)
    {
        var probe = new TextBlock { Text = "probe" };
        try
        {
            ToolTip.SetTip(target, probe);
            ToolTip.SetIsOpen(target, true);
            Settle();

            Assert.NotEqual(Brushes.White, probe.Foreground);
        }
        finally
        {
            ToolTip.SetIsOpen(target, false);
            ToolTip.SetTip(target, null);
            Settle();
            window.Close();
        }
    }

    private static Button Connector(Visual root, string connectorClass) =>
        root.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains(connectorClass));

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TranscribeStrip_Connector_DoesNotRepaintItsTooltip(bool paused)
    {
        var relationship = await RelationshipAsync(paused);
        var window = new TranscribeWindow
        {
            DataContext = new TranscribeViewModel(new MockWindowManager(), relationship: relationship),
            RequestedThemeVariant = ThemeVariant.Light,
        };
        window.Show();
        Settle();

        AssertTooltipKeepsItsOwnForeground(window, Connector(window, "stripConnector"));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LanguagePage_Connector_DoesNotRepaintItsTooltip(bool paused)
    {
        var relationship = await RelationshipAsync(paused);
        var window = new Window
        {
            Width = 600,
            Height = 400,
            Content = new LanguageSelectionSettingsView
            {
                DataContext = new LanguageSelectionSettingsViewModel(relationship),
            },
            RequestedThemeVariant = ThemeVariant.Light,
        };
        window.Show();
        Settle();

        AssertTooltipKeepsItsOwnForeground(window, Connector(window, "connector"));
    }

    /// <summary>
    /// The record button paints itself white-on-accent while recording and its
    /// tooltip lists the user's hotkeys, so the same leak would blank it out. It
    /// does not: that style sets <c>Foreground</c> on the button itself, which the
    /// tooltip's separate popup root does not inherit. Pinned so it stays that way.
    /// </summary>
    [AvaloniaFact]
    public async Task RecordButton_WhileRecording_DoesNotRepaintItsTooltip()
    {
        var relationship = await RelationshipAsync(paused: false);
        var window = new TranscribeWindow
        {
            DataContext = new TranscribeViewModel(new MockWindowManager(), relationship: relationship),
            RequestedThemeVariant = ThemeVariant.Light,
        };
        window.Show();
        Settle();

        var record = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "RecordButton");
        record.Classes.Add("recording");
        Settle();

        AssertTooltipKeepsItsOwnForeground(window, record);
    }
}
