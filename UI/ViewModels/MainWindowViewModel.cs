using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VpnClient.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace VpnClient.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IVpnService _vpnService;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private string _logText = string.Empty;

    public MainWindowViewModel(IVpnService vpnService, ILogger<MainWindowViewModel> logger)
    {
        _vpnService = vpnService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (_vpnService.State == VpnState.Disconnected)
        {
            AppendLog("Connecting...");
            await _vpnService.ConnectAsync("TODO: config");
            AppendLog("Connected");
            OnPropertyChanged(nameof(ButtonText));
        }
        else if (_vpnService.State == VpnState.Connected)
        {
            AppendLog("Disconnecting...");
            await _vpnService.DisconnectAsync();
            AppendLog("Disconnected");
            OnPropertyChanged(nameof(ButtonText));
        }
    }

    public string ButtonText => _vpnService.State == VpnState.Connected ? "Отключиться" : "Подключиться к VPN";

    private void AppendLog(string message)
    {
        _logText += $"{DateTime.Now:T}: {message}\n";
        OnPropertyChanged(nameof(LogText));
    }
}
