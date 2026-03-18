using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VpnClient.Application.Profiles;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;
using VpnClient.Core.Models.Diagnostics;

namespace VpnClient.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ImportProfileUseCase _importProfileUseCase;
    private readonly ListProfilesUseCase _listProfilesUseCase;
    private readonly RenameProfileUseCase _renameProfileUseCase;
    private readonly DeleteProfileUseCase _deleteProfileUseCase;
    private readonly SetActiveProfileUseCase _setActiveProfileUseCase;
    private readonly IVpnRuntimeAdapter _runtimeAdapter;
    private readonly IVpnDiagnosticsService _diagnosticsService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly DispatcherTimer _refreshTimer;

    private bool _initialized;
    private bool _applyingSnapshot;
    private bool _refreshInFlight;

    public MainWindowViewModel(
        ImportProfileUseCase importProfileUseCase,
        ListProfilesUseCase listProfilesUseCase,
        RenameProfileUseCase renameProfileUseCase,
        DeleteProfileUseCase deleteProfileUseCase,
        SetActiveProfileUseCase setActiveProfileUseCase,
        IVpnRuntimeAdapter runtimeAdapter,
        IVpnDiagnosticsService diagnosticsService,
        ILogger<MainWindowViewModel> logger)
    {
        _importProfileUseCase = importProfileUseCase;
        _listProfilesUseCase = listProfilesUseCase;
        _renameProfileUseCase = renameProfileUseCase;
        _deleteProfileUseCase = deleteProfileUseCase;
        _setActiveProfileUseCase = setActiveProfileUseCase;
        _runtimeAdapter = runtimeAdapter;
        _diagnosticsService = diagnosticsService;
        _logger = logger;

        Profiles = [];
        ConnectionLogs = [];
        ImportValidationErrors = [];

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshDiagnosticsAsync();
    }

    public ObservableCollection<ImportedServerProfile> Profiles { get; }

    public ObservableCollection<ConnectionLogEntry> ConnectionLogs { get; }

    public ObservableCollection<ImportValidationError> ImportValidationErrors { get; }

    [ObservableProperty]
    private ImportedServerProfile? selectedProfile;

    [ObservableProperty]
    private ConnectionState connectionState = ConnectionState.Disconnected("VpnClient");

    [ObservableProperty]
    private string renameDraft = string.Empty;

    [ObservableProperty]
    private string lastOperationMessage = "Импортируйте .vpn или .conf, чтобы добавить сервер.";

    [ObservableProperty]
    private bool isBusy;

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => Profiles.Count == 0;

    public bool HasSelectedProfile => SelectedProfile is not null;

    public bool HasImportErrors => ImportValidationErrors.Count > 0;

    public string EmptyStateTitle => "Добавьте конфигурацию";

    public string EmptyStateText => "Клиент хранит локальные профили и подключается к уже существующим Amnezia/WireGuard серверам. Никакой выдачи ключей внутри приложения.";

    public string PrimaryActionText
    {
        get
        {
            if (IsBusy)
            {
                return "Подождите...";
            }

            if (ConnectionState.Status == RuntimeConnectionStatus.Disconnecting)
            {
                return "Отключение...";
            }

            if (ConnectionState.Status == RuntimeConnectionStatus.Connecting && ConnectionState.ProfileId == SelectedProfile?.Id)
            {
                return "Подключение...";
            }

            if (ConnectionState.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded
                && ConnectionState.ProfileId == SelectedProfile?.Id)
            {
                return "Отключить";
            }

            if (ConnectionState.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded
                && ConnectionState.ProfileId != SelectedProfile?.Id
                && SelectedProfile is not null)
            {
                return "Переподключить";
            }

            return "Подключить";
        }
    }

    public bool CanToggleConnection =>
        !IsBusy
        && SelectedProfile is not null
        && ConnectionState.Status is not RuntimeConnectionStatus.Disconnecting;

    public bool CanRenameSelectedProfile =>
        !IsBusy
        && SelectedProfile is not null
        && !string.IsNullOrWhiteSpace(RenameDraft)
        && !string.Equals(SelectedProfile.DisplayName, RenameDraft.Trim(), StringComparison.Ordinal);

    public bool CanDeleteSelectedProfile => !IsBusy && SelectedProfile is not null;

    public string CurrentRuntimeLabel => ConnectionState.AdapterName switch
    {
        "BundledAmneziaWG" => "Bundled AmneziaWG runtime",
        "AmneziaDaemon" => "External Amnezia daemon runtime",
        _ when ConnectionState.IsWindowsFirst => "Legacy Windows fallback runtime",
        _ => "VPN runtime"
    };

    public string CurrentFormatLabel => SelectedProfile?.SourceFormat switch
    {
        TunnelConfigFormat.AmneziaVpn => ".vpn / Amnezia package",
        TunnelConfigFormat.AmneziaAwgNative => ".conf / AmneziaWG",
        TunnelConfigFormat.WireGuardConf => ".conf / WireGuard",
        _ => "Нет профиля"
    };

    public string StatusTitle => ConnectionState.Status switch
    {
        RuntimeConnectionStatus.Connected => "Подключено",
        RuntimeConnectionStatus.Connecting => "Подключение",
        RuntimeConnectionStatus.Disconnecting => "Отключение",
        RuntimeConnectionStatus.Degraded => "Подключено с предупреждениями",
        RuntimeConnectionStatus.Failed => "Ошибка подключения",
        RuntimeConnectionStatus.Unsupported => "Режим недоступен",
        _ => "Отключено"
    };

    public string StatusDescription
    {
        get
        {
            if (ConnectionState.LastError is not null)
            {
                return ConnectionState.LastError;
            }

            if (SelectedProfile is null)
            {
                return "Выберите профиль или импортируйте новый конфиг.";
            }

            return ConnectionState.Status switch
            {
                RuntimeConnectionStatus.Connected => "Туннель поднят. Можно работать через выбранный профиль.",
                RuntimeConnectionStatus.Connecting => "Клиент применяет runtime-путь Amnezia и ждёт handshake/трафик.",
                RuntimeConnectionStatus.Degraded => "Туннель активирован, но клиент видит warnings. Проверьте DNS, handshake и backend runtime.",
                RuntimeConnectionStatus.Failed => "Подключение не завершилось. Смотрите diagnostics и import validation.",
                RuntimeConnectionStatus.Unsupported => "На этой машине нет нужного runtime backend для подключения.",
                _ => "Профиль импортирован и готов к подключению."
            };
        }
    }

    public string EndpointText => SelectedProfile?.Endpoint ?? "Не найден";

    public string AddressText => SelectedProfile?.Address ?? "Не найден";

    public string DnsText => SelectedProfile is null || SelectedProfile.DnsServers.Count == 0
        ? "Не задан"
        : string.Join(", ", SelectedProfile.DnsServers);

    public string AllowedIpsText => SelectedProfile is null || SelectedProfile.AllowedIps.Count == 0
        ? "Не заданы"
        : string.Join(", ", SelectedProfile.AllowedIps);

    public string MtuText => SelectedProfile?.Mtu ?? "По умолчанию";

    public string HandshakeText => ConnectionState.LatestHandshakeAtUtc is null
        ? "Handshake ещё не зафиксирован"
        : ConnectionState.LatestHandshakeAtUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");

    public string DownloadText => FormatBytes(ConnectionState.ReceivedBytes);

    public string UploadText => FormatBytes(ConnectionState.SentBytes);

    public string ImportErrorSummary => HasImportErrors
        ? $"{ImportValidationErrors.Count} ошибок импорта сохранено локально"
        : "Ошибок импорта нет";

    public IReadOnlyList<string> WarningItems => ConnectionState.Warnings;

    partial void OnSelectedProfileChanged(ImportedServerProfile? value)
    {
        RenameDraft = value?.DisplayName ?? string.Empty;
        NotifyViewStateChanged();

        if (_applyingSnapshot || value is null)
        {
            return;
        }

        _ = ActivateSelectionAsync(value.Id);
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        var snapshot = await _listProfilesUseCase.ExecuteAsync();
        ApplySnapshot(snapshot, snapshot.ActiveProfileId);

        var restoredState = await _runtimeAdapter.TryRestoreAsync(snapshot.Profiles);
        if (restoredState.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Connecting or RuntimeConnectionStatus.Degraded)
        {
            var restoreTarget = restoredState.ProfileName
                                ?? restoredState.Endpoint
                                ?? "existing tunnel";
            _diagnosticsService.RecordConnectionLog(
                $"Restored runtime state for '{restoreTarget}'.",
                DiagnosticsLogLevel.Information,
                "runtime",
                restoredState.AdapterName);
        }

        await RefreshDiagnosticsAsync();
        _refreshTimer.Start();
    }

    public async Task ImportConfigAsync(string path)
    {
        try
        {
            IsBusy = true;
            var result = await _importProfileUseCase.ExecuteAsync(path);
            _diagnosticsService.RecordConnectionLog($"Импортирован профиль '{result.Profile.DisplayName}'.", DiagnosticsLogLevel.Information, "import", "UI");
            LastOperationMessage = $"Профиль '{result.Profile.DisplayName}' добавлен.";
            ApplySnapshot(result.Snapshot, result.Profile.Id);
            await RefreshDiagnosticsAsync();
        }
        catch (Exception exception)
        {
            _diagnosticsService.RecordImportValidationError(exception, path);
            _diagnosticsService.RecordConnectionLog($"Ошибка импорта: {exception.Message}", DiagnosticsLogLevel.Error, "import", "UI");
            LastOperationMessage = $"Ошибка импорта: {exception.Message}";
            _logger.LogError(exception, "Failed to import config from {Path}", path);
            await RefreshDiagnosticsAsync();
        }
        finally
        {
            IsBusy = false;
            NotifyViewStateChanged();
        }
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (SelectedProfile is null || IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            NotifyViewStateChanged();

            var shouldDisconnectCurrent = ConnectionState.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded or RuntimeConnectionStatus.Connecting;

            if (shouldDisconnectCurrent && ConnectionState.ProfileId == SelectedProfile.Id)
            {
                _diagnosticsService.RecordConnectionLog($"Отключение от '{SelectedProfile.DisplayName}'.", DiagnosticsLogLevel.Information, "connect", "UI");
                await _runtimeAdapter.DisconnectAsync();
                LastOperationMessage = $"Профиль '{SelectedProfile.DisplayName}' отключён.";
                await RefreshDiagnosticsAsync();
                return;
            }

            if (shouldDisconnectCurrent && ConnectionState.ProfileId != SelectedProfile.Id)
            {
                _diagnosticsService.RecordConnectionLog($"Переключение с '{ConnectionState.ProfileName}' на '{SelectedProfile.DisplayName}'.", DiagnosticsLogLevel.Information, "connect", "UI");
                await _runtimeAdapter.DisconnectAsync();
            }
            else
            {
                _diagnosticsService.RecordConnectionLog($"Подключение к '{SelectedProfile.DisplayName}'.", DiagnosticsLogLevel.Information, "connect", "UI");
            }

            var state = await _runtimeAdapter.ConnectAsync(SelectedProfile);
            LastOperationMessage = state.Status switch
            {
                RuntimeConnectionStatus.Failed => $"Подключение не удалось: {state.LastError}",
                RuntimeConnectionStatus.Unsupported => state.LastError ?? "Нужный runtime backend недоступен.",
                _ => $"Профиль '{SelectedProfile.DisplayName}' отправлен в runtime."
            };

            await RefreshDiagnosticsAsync();
        }
        catch (Exception exception)
        {
            _diagnosticsService.RecordConnectionLog($"Ошибка подключения: {exception.Message}", DiagnosticsLogLevel.Error, "connect", "UI");
            LastOperationMessage = $"Ошибка подключения: {exception.Message}";
            _logger.LogError(exception, "Connection toggle failed for {Profile}", SelectedProfile.DisplayName);
            await RefreshDiagnosticsAsync();
        }
        finally
        {
            IsBusy = false;
            NotifyViewStateChanged();
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await RefreshDiagnosticsAsync();
    }

    [RelayCommand]
    private async Task RenameSelectedProfileAsync()
    {
        if (SelectedProfile is null || !CanRenameSelectedProfile)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var snapshot = await _renameProfileUseCase.ExecuteAsync(SelectedProfile.Id, RenameDraft.Trim());
            _diagnosticsService.RecordConnectionLog($"Профиль переименован в '{RenameDraft.Trim()}'.", DiagnosticsLogLevel.Information, "profile", "UI");
            LastOperationMessage = "Имя профиля обновлено.";
            ApplySnapshot(snapshot, SelectedProfile.Id);
        }
        catch (Exception exception)
        {
            _diagnosticsService.RecordConnectionLog($"Ошибка переименования: {exception.Message}", DiagnosticsLogLevel.Error, "profile", "UI");
            LastOperationMessage = $"Ошибка переименования: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyViewStateChanged();
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedProfileAsync()
    {
        if (SelectedProfile is null || !CanDeleteSelectedProfile)
        {
            return;
        }

        try
        {
            IsBusy = true;

            if (ConnectionState.ProfileId == SelectedProfile.Id
                && ConnectionState.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded or RuntimeConnectionStatus.Connecting)
            {
                await _runtimeAdapter.DisconnectAsync();
            }

            var deletedProfileId = SelectedProfile.Id;
            var deletedProfileName = SelectedProfile.DisplayName;
            var snapshot = await _deleteProfileUseCase.ExecuteAsync(deletedProfileId);
            _diagnosticsService.RecordConnectionLog($"Профиль '{deletedProfileName}' удалён.", DiagnosticsLogLevel.Warning, "profile", "UI");
            LastOperationMessage = $"Профиль '{deletedProfileName}' удалён.";
            ApplySnapshot(snapshot, snapshot.ActiveProfileId);
            await RefreshDiagnosticsAsync();
        }
        catch (Exception exception)
        {
            _diagnosticsService.RecordConnectionLog($"Ошибка удаления профиля: {exception.Message}", DiagnosticsLogLevel.Error, "profile", "UI");
            LastOperationMessage = $"Ошибка удаления: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyViewStateChanged();
        }
    }

    private async Task ActivateSelectionAsync(Guid profileId)
    {
        try
        {
            var snapshot = await _setActiveProfileUseCase.ExecuteAsync(profileId);
            ApplySnapshot(snapshot, profileId);
            await RefreshDiagnosticsAsync();
        }
        catch (Exception exception)
        {
            _diagnosticsService.RecordConnectionLog($"Ошибка выбора профиля: {exception.Message}", DiagnosticsLogLevel.Error, "profile", "UI");
            LastOperationMessage = $"Ошибка выбора профиля: {exception.Message}";
        }
    }

    private async Task RefreshDiagnosticsAsync()
    {
        if (_refreshInFlight)
        {
            return;
        }

        try
        {
            _refreshInFlight = true;
            var previousState = ConnectionState;
            var snapshot = await _diagnosticsService.CaptureSnapshotAsync();

            ConnectionState = snapshot.ConnectionState;
            ReplaceCollection(ConnectionLogs, snapshot.ConnectionLogs.OrderByDescending(entry => entry.TimestampUtc));
            ReplaceCollection(ImportValidationErrors, snapshot.ImportValidationErrors.OrderByDescending(entry => entry.TimestampUtc));

            if (snapshot.CurrentProfile is not null && SelectedProfile?.Id != snapshot.CurrentProfile.Id)
            {
                var matching = Profiles.FirstOrDefault(profile => profile.Id == snapshot.CurrentProfile.Id);
                if (matching is not null)
                {
                    _applyingSnapshot = true;
                    SelectedProfile = matching;
                    _applyingSnapshot = false;
                }
            }

            LogRuntimeTransition(previousState, snapshot.ConnectionState);
            NotifyViewStateChanged();
        }
        finally
        {
            _refreshInFlight = false;
        }
    }

    private void LogRuntimeTransition(ConnectionState previous, ConnectionState next)
    {
        if (previous.Status != next.Status)
        {
            _diagnosticsService.RecordConnectionLog(
                $"Статус туннеля: {previous.Status} -> {next.Status}.",
                next.Status is RuntimeConnectionStatus.Failed or RuntimeConnectionStatus.Unsupported
                    ? DiagnosticsLogLevel.Error
                    : DiagnosticsLogLevel.Information,
                "runtime",
                next.AdapterName);
        }

        if (previous.LatestHandshakeAtUtc is null && next.LatestHandshakeAtUtc is not null)
        {
            _diagnosticsService.RecordConnectionLog(
                $"Получен handshake для '{next.ProfileName}'.",
                DiagnosticsLogLevel.Information,
                "runtime",
                next.AdapterName);
        }
    }

    private void ApplySnapshot(ProfileCollectionSnapshot snapshot, Guid? preferredSelectionId)
    {
        _applyingSnapshot = true;
        try
        {
            ReplaceCollection(Profiles, snapshot.Profiles);

            var targetId = preferredSelectionId
                ?? snapshot.ActiveProfileId
                ?? SelectedProfile?.Id
                ?? snapshot.Profiles.FirstOrDefault()?.Id;

            SelectedProfile = targetId is null
                ? null
                : Profiles.FirstOrDefault(profile => profile.Id == targetId.Value);
        }
        finally
        {
            _applyingSnapshot = false;
            NotifyViewStateChanged();
        }
    }

    private void NotifyViewStateChanged()
    {
        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(HasNoProfiles));
        OnPropertyChanged(nameof(HasSelectedProfile));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(CanToggleConnection));
        OnPropertyChanged(nameof(CanRenameSelectedProfile));
        OnPropertyChanged(nameof(CanDeleteSelectedProfile));
        OnPropertyChanged(nameof(CurrentRuntimeLabel));
        OnPropertyChanged(nameof(CurrentFormatLabel));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusDescription));
        OnPropertyChanged(nameof(EndpointText));
        OnPropertyChanged(nameof(AddressText));
        OnPropertyChanged(nameof(DnsText));
        OnPropertyChanged(nameof(AllowedIpsText));
        OnPropertyChanged(nameof(MtuText));
        OnPropertyChanged(nameof(HandshakeText));
        OnPropertyChanged(nameof(DownloadText));
        OnPropertyChanged(nameof(UploadText));
        OnPropertyChanged(nameof(ImportErrorSummary));
        OnPropertyChanged(nameof(WarningItems));
        ToggleConnectionCommand.NotifyCanExecuteChanged();
        RenameSelectedProfileCommand.NotifyCanExecuteChanged();
        DeleteSelectedProfileCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var suffixIndex = 0;

        while (value >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            value /= 1024;
            suffixIndex++;
        }

        return $"{value:0.##} {suffixes[suffixIndex]}";
    }
}
