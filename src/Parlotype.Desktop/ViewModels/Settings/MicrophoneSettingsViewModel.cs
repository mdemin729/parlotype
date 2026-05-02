using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;

namespace Parlotype.Desktop.ViewModels.Settings;

public partial class MicrophoneSettingsViewModel : SettingsSectionViewModelBase, IDisposable
{
    private readonly IMicrophoneEnumerator _enumerator;
    private readonly ISettingsService _settings;
    private readonly ILogger<MicrophoneSettingsViewModel> _logger;
    private bool _initialized;

    public override string Title => "Microphone";

    public ObservableCollection<MicrophoneDisplayItem> AvailableMicrophones { get; } = [];

    [ObservableProperty]
    private MicrophoneInfo? _selectedMicrophone;

    public MicrophoneSettingsViewModel(
        IMicrophoneEnumerator enumerator,
        ISettingsService settings,
        ILogger<MicrophoneSettingsViewModel>? logger = null)
    {
        _enumerator = enumerator;
        _settings = settings;
        _logger = logger ?? NullLogger<MicrophoneSettingsViewModel>.Instance;

        _enumerator.DevicesChanged += OnDevicesChanged;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;

        var savedId = await _settings.GetAsync<string>(SettingsKeys.SelectedMicrophoneId);
        PopulateList();

        var match = AvailableMicrophones.FirstOrDefault(m => m.Info.Id == savedId);
        if (match is not null)
            ApplySelection(match);
        else if (AvailableMicrophones.Count > 0)
            ApplySelection(AvailableMicrophones[0]);
    }

    private void PopulateList()
    {
        AvailableMicrophones.Clear();
        foreach (var mic in _enumerator.GetAvailableMicrophones())
            AvailableMicrophones.Add(new MicrophoneDisplayItem(mic, SelectMicrophoneCommand));
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var previousId = SelectedMicrophone?.Id;
            PopulateList();
            var match = AvailableMicrophones.FirstOrDefault(m => m.Info.Id == previousId)
                        ?? (AvailableMicrophones.Count > 0 ? AvailableMicrophones[0] : null);
            if (match is not null)
            {
                ApplySelection(match);
                _ = _settings.SetAsync(SettingsKeys.SelectedMicrophoneId, match.Info.Id);
            }
            else
            {
                SelectedMicrophone = null;
            }
        });
    }

    private void ApplySelection(MicrophoneDisplayItem item)
    {
        foreach (var m in AvailableMicrophones)
            m.IsSelected = false;
        item.IsSelected = true;
        SelectedMicrophone = item.Info;
    }

    [RelayCommand]
    private void SelectMicrophone(MicrophoneDisplayItem item)
    {
        _logger.LogInformation("Microphone selected: {Name}", item.Name);
        ApplySelection(item);
        _ = _settings.SetAsync(SettingsKeys.SelectedMicrophoneId, item.Info.Id);
    }

    public void Dispose()
    {
        _enumerator.DevicesChanged -= OnDevicesChanged;
        GC.SuppressFinalize(this);
    }
}
