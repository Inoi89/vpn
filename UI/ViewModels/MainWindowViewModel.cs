using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IVpnService _vpnService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IConfigService _configService;

    [ObservableProperty]
    private ImportedProfile? importedProfile;

    [ObservableProperty]
    private string importStateText = "Импортируйте .vpn или .conf, чтобы добавить сервер.";

    public ObservableCollection<LogEntry> LogEntries { get; }

    public MainWindowViewModel(
        IConfigService configService,
        IVpnService vpnService,
        ILogger<MainWindowViewModel> logger,
        ObservableCollection<LogEntry> logEntries)
    {
        _configService = configService;
        _vpnService = vpnService;
        _logger = logger;
        LogEntries = logEntries;
        ImportedProfile = _configService.CurrentProfile;

        if (ImportedProfile is not null)
        {
            ImportStateText = $"Активный профиль: {ImportedProfile.DisplayName}";
        }
    }

    public bool HasImportedProfile => ImportedProfile is not null;

    public bool CanToggleConnection => _vpnService.State switch
    {
        VpnState.Connecting => false,
        VpnState.Disconnecting => false,
        VpnState.Connected => true,
        _ => HasImportedProfile
    };

    public string ConnectionStatus => _vpnService.State switch
    {
        VpnState.Connected => "Подключено",
        VpnState.Connecting => "Подключение...",
        VpnState.Disconnecting => "Отключение...",
        _ => "Не подключено"
    };

    public string PrimaryButtonText => _vpnService.State == VpnState.Connected ? "Отключиться" : "Подключиться";

    public string ImportButtonText => HasImportedProfile ? "Заменить конфиг" : "Добавить сервер из файла";

    public string ProfileTitle => ImportedProfile?.DisplayName ?? "Сервер не добавлен";

    public string ProfileFileName => ImportedProfile?.FileName ?? "Файл не выбран";

    public string ProfileFormat => ImportedProfile?.Format ?? "Нет активного профиля";

    public string ProfileEndpoint => ImportedProfile?.Endpoint ?? "Endpoint не найден";

    public string ProfileAddress => ImportedProfile?.Address ?? "Адрес не найден";

    public string ProfileDns => ImportedProfile?.PrimaryDns ?? "DNS не найден";

    public string ProfileSourcePath => ImportedProfile?.SourcePath ?? "Файл еще не выбран";

    public string ImportedAt => ImportedProfile is null
        ? "Профиль не импортирован"
        : ImportedProfile.ImportedAtUtc.ToLocalTime().ToString("dd.MM.yyyy, HH:mm:ss");

    public string ConnectionHint => _vpnService.State switch
    {
        VpnState.Connected => "Туннель поднят. Можно работать через выбранный сервер.",
        VpnState.Connecting => "Поднимаем интерфейс и применяем конфигурацию.",
        VpnState.Disconnecting => "Отключаем интерфейс и очищаем локальное состояние.",
        _ when HasImportedProfile => "Профиль готов. Можно подключаться.",
        _ => "Сначала импортируйте конфиг Amnezia."
    };

    public string ProfileSummary => ImportedProfile is null
        ? "Нет импортированного сервера"
        : $"{ProfileTitle} · {ProfileFormat}";

    partial void OnImportedProfileChanged(ImportedProfile? value)
    {
        OnPropertyChanged(nameof(HasImportedProfile));
        OnPropertyChanged(nameof(CanToggleConnection));
        OnPropertyChanged(nameof(ImportButtonText));
        OnPropertyChanged(nameof(ProfileTitle));
        OnPropertyChanged(nameof(ProfileFileName));
        OnPropertyChanged(nameof(ProfileFormat));
        OnPropertyChanged(nameof(ProfileEndpoint));
        OnPropertyChanged(nameof(ProfileAddress));
        OnPropertyChanged(nameof(ProfileDns));
        OnPropertyChanged(nameof(ProfileSourcePath));
        OnPropertyChanged(nameof(ImportedAt));
        OnPropertyChanged(nameof(ConnectionHint));
        OnPropertyChanged(nameof(ProfileSummary));
    }

    public async Task ImportConfigAsync(string path)
    {
        try
        {
            var profile = await _configService.ImportConfigAsync(path);
            ImportedProfile = profile;
            ImportStateText = $"Импортирован профиль {profile.DisplayName}.";
            _logger.LogInformation("Imported profile from {Path}", path);
        }
        catch (Exception ex)
        {
            ImportStateText = $"Ошибка импорта: {ex.Message}";
            _logger.LogError(ex, "Failed to import config from {Path}", path);
        }
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (_vpnService.State == VpnState.Disconnected)
        {
            if (!HasImportedProfile)
            {
                ImportStateText = "Сначала импортируйте .vpn или .conf.";
                _logger.LogWarning("Connect requested without imported profile");
                return;
            }

            _logger.LogInformation("Connecting...");
            RefreshConnectionState();

            try
            {
                var config = await _configService.LoadConfigAsync();
                await _vpnService.ConnectAsync(config);
                _logger.LogInformation("Connected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection error: {Message}", ex.Message);
            }

            RefreshConnectionState();
            return;
        }

        if (_vpnService.State == VpnState.Connected)
        {
            _logger.LogInformation("Disconnecting...");
            RefreshConnectionState();

            try
            {
                await _vpnService.DisconnectAsync();
                _logger.LogInformation("Disconnected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Disconnect error: {Message}", ex.Message);
            }

            RefreshConnectionState();
        }
    }

    private void RefreshConnectionState()
    {
        OnPropertyChanged(nameof(CanToggleConnection));
        OnPropertyChanged(nameof(ConnectionStatus));
        OnPropertyChanged(nameof(ConnectionHint));
        OnPropertyChanged(nameof(PrimaryButtonText));
    }
}
