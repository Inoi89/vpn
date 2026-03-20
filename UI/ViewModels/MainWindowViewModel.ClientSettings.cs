using VpnClient.Core.Models;

namespace VpnClient.UI.ViewModels;

public partial class MainWindowViewModel
{
    private bool _applyingClientSettings;
    private bool _clientSettingsLoaded;

    private ClientSettings BuildClientSettings()
    {
        return new ClientSettings(
            AutoConnectEnabled,
            KillSwitchEnabled,
            NotificationsEnabled,
            LaunchToTrayEnabled);
    }

    private async Task LoadClientSettingsAsync()
    {
        var settings = await _clientSettingsService.LoadAsync();

        _applyingClientSettings = true;
        try
        {
            AutoConnectEnabled = settings.AutoConnectEnabled;
            KillSwitchEnabled = settings.KillSwitchEnabled;
            NotificationsEnabled = settings.NotificationsEnabled;
            LaunchToTrayEnabled = settings.LaunchToTrayEnabled;
        }
        finally
        {
            _applyingClientSettings = false;
            _clientSettingsLoaded = true;
        }
    }

    private async Task PersistClientSettingsAsync()
    {
        if (_applyingClientSettings || !_clientSettingsLoaded)
        {
            return;
        }

        await _clientSettingsService.SaveAsync(BuildClientSettings());
    }

    private async Task HandleRuntimeStateChangedAsync(ConnectionState state)
    {
        await EnsureKillSwitchStateAsync(state, releaseWhenInactive: false);
    }

    private async Task EnsureKillSwitchStateAsync(ConnectionState state, bool releaseWhenInactive)
    {
        if (!KillSwitchEnabled)
        {
            await _killSwitchService.DisarmAsync();
            return;
        }

        if (state.Status is RuntimeConnectionStatus.Connected
            or RuntimeConnectionStatus.Connecting
            or RuntimeConnectionStatus.Degraded)
        {
            var endpoint = state.Endpoint ?? SelectedProfile?.Endpoint;
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                await _killSwitchService.ArmAsync(endpoint);
            }

            return;
        }

        if (releaseWhenInactive)
        {
            await _killSwitchService.DisarmAsync();
        }
    }

    private async Task<ConnectionState> DisconnectRuntimeAsync(bool releaseKillSwitch)
    {
        var state = await _runtimeAdapter.DisconnectAsync();
        await EnsureKillSwitchStateAsync(state, releaseKillSwitch);
        return state;
    }

    partial void OnAutoConnectEnabledChanged(bool value)
    {
        _ = PersistClientSettingsAsync();
    }

    partial void OnKillSwitchEnabledChanged(bool value)
    {
        _ = PersistClientSettingsAsync();
        _ = EnsureKillSwitchStateAsync(ConnectionState, releaseWhenInactive: !value);
    }

    partial void OnNotificationsEnabledChanged(bool value)
    {
        _ = PersistClientSettingsAsync();
    }

    partial void OnLaunchToTrayEnabledChanged(bool value)
    {
        _ = PersistClientSettingsAsync();
    }
}
