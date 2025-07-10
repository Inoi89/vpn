using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VpnClient.Core.Interfaces;
using Microsoft.Extensions.Logging;
using VpnClient.Core.Models;

namespace VpnClient.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IVpnService _vpnService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IConfigService _configService;

    public ObservableCollection<LogEntry> LogEntries { get; }

    public MainWindowViewModel(IConfigService configService, IVpnService vpnService,
        ILogger<MainWindowViewModel> logger, ObservableCollection<LogEntry> logEntries)
    {
        _configService = configService;
        _vpnService = vpnService;
        _logger = logger;
        LogEntries = logEntries;
    }

    public string ConnectionStatus => _vpnService.State switch
    {
        VpnState.Connected => "VPN подключен",
        VpnState.Connecting => "Подключение...",
        VpnState.Disconnecting => "Отключение...",
        _ => "VPN отключен"
    };

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (_vpnService.State == VpnState.Disconnected)
        {
            _logger.LogInformation("Connecting...");
            try
            {
                var config = await _configService.LoadConfigAsync();
                await _vpnService.ConnectAsync(config);
                _logger.LogInformation("Connected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error: {Message}", ex.Message);
            }
            OnPropertyChanged(nameof(ButtonText));
            OnPropertyChanged(nameof(ConnectionStatus));
        }
        else if (_vpnService.State == VpnState.Connected)
        {
            _logger.LogInformation("Disconnecting...");
            try
            {
                await _vpnService.DisconnectAsync();
                _logger.LogInformation("Disconnected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error: {Message}", ex.Message);
            }
            OnPropertyChanged(nameof(ButtonText));
            OnPropertyChanged(nameof(ConnectionStatus));
        }
    }

    public string ButtonText => _vpnService.State == VpnState.Connected ? "Отключиться" : "Подключиться к VPN";
}
