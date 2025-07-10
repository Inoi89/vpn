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

    public ObservableCollection<LogEntry> LogEntries { get; }

    private const string AmneziaConfig = """
[Interface]
Address = 10.8.1.11/32
DNS = 1.1.1.1, 1.0.0.1
PrivateKey = LHaPBG/W+dIOypKSs8IBMQbJZOJFOF1OzWltNxoY9+c=
Jc = 8
Jmin = 50
Jmax = 1000
S1 = 73
S2 = 24
H1 = 79450512
H2 = 1691023906
H3 = 310914319
H4 = 403689475

[Peer]
PublicKey = NtaQ9Uui6q2Tnr2px3398/b7vzrF2xhWuNax1AO2gg4=
PresharedKey = WUH31aV7brbl40vy/b984sQo9tWe/fzLq7zOJzE1D9A=
AllowedIPs = 0.0.0.0/0, ::/0
Endpoint = 5.61.37.29:31296
PersistentKeepalive = 25
""";

    public MainWindowViewModel(IVpnService vpnService, ILogger<MainWindowViewModel> logger,
        ObservableCollection<LogEntry> logEntries)
    {
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
                await _vpnService.ConnectAsync(AmneziaConfig);
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
