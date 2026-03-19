using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VpnClient.Application.Profiles;
using VpnClient.Application.Updates;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;
using VpnClient.Core.Models.Diagnostics;
using VpnClient.Core.Models.Updates;

namespace VpnClient.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ImportProfileUseCase _importProfileUseCase;
    private readonly ListProfilesUseCase _listProfilesUseCase;
    private readonly RenameProfileUseCase _renameProfileUseCase;
    private readonly DeleteProfileUseCase _deleteProfileUseCase;
    private readonly SetActiveProfileUseCase _setActiveProfileUseCase;
    private readonly IAppUpdateService _appUpdateService;
    private readonly CheckForAppUpdatesUseCase _checkForAppUpdatesUseCase;
    private readonly PrepareAppUpdateUseCase _prepareAppUpdateUseCase;
    private readonly LaunchPreparedAppUpdateUseCase _launchPreparedAppUpdateUseCase;
    private readonly IVpnRuntimeAdapter _runtimeAdapter;
    private readonly IVpnDiagnosticsService _diagnosticsService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly DispatcherTimer _refreshTimer;

    private bool _initialized;
    private bool _applyingSnapshot;
    private bool _refreshInFlight;
    private double _downloadRateBytesPerSecond;
    private double _uploadRateBytesPerSecond;
    private long _lastReceivedBytes;
    private long _lastSentBytes;
    private DateTimeOffset? _lastTrafficSampleUtc;
    private DateTimeOffset? _sessionStartedAtUtc;

    public MainWindowViewModel(
        ImportProfileUseCase importProfileUseCase,
        ListProfilesUseCase listProfilesUseCase,
        RenameProfileUseCase renameProfileUseCase,
        DeleteProfileUseCase deleteProfileUseCase,
        SetActiveProfileUseCase setActiveProfileUseCase,
        IAppUpdateService appUpdateService,
        CheckForAppUpdatesUseCase checkForAppUpdatesUseCase,
        PrepareAppUpdateUseCase prepareAppUpdateUseCase,
        LaunchPreparedAppUpdateUseCase launchPreparedAppUpdateUseCase,
        IVpnRuntimeAdapter runtimeAdapter,
        IVpnDiagnosticsService diagnosticsService,
        ILogger<MainWindowViewModel> logger)
    {
        _importProfileUseCase = importProfileUseCase;
        _listProfilesUseCase = listProfilesUseCase;
        _renameProfileUseCase = renameProfileUseCase;
        _deleteProfileUseCase = deleteProfileUseCase;
        _setActiveProfileUseCase = setActiveProfileUseCase;
        _appUpdateService = appUpdateService;
        _checkForAppUpdatesUseCase = checkForAppUpdatesUseCase;
        _prepareAppUpdateUseCase = prepareAppUpdateUseCase;
        _launchPreparedAppUpdateUseCase = launchPreparedAppUpdateUseCase;
        _runtimeAdapter = runtimeAdapter;
        _diagnosticsService = diagnosticsService;
        _logger = logger;

        Profiles = [];

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshDiagnosticsAsync();
    }

    public ObservableCollection<ImportedServerProfile> Profiles { get; }

    [ObservableProperty]
    private ImportedServerProfile? selectedProfile;

    [ObservableProperty]
    private ConnectionState connectionState = ConnectionState.Disconnected("VpnClient");

    [ObservableProperty]
    private string renameDraft = string.Empty;

    [ObservableProperty]
    private string lastOperationMessage = "Добавьте конфиг и подключитесь к нужному серверу.";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private AppUpdateState updateState = AppUpdateState.Disabled("0.0.0");

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => Profiles.Count == 0;

    public bool HasSelectedProfile => SelectedProfile is not null;

    public bool ShowImportShortcut => HasProfiles;

    public bool UpdatesEnabled => UpdateState.IsEnabled;

    public bool ShowUpdateCard => UpdateState.Status is not AppUpdateStatus.Disabled || !string.IsNullOrWhiteSpace(UpdateState.LastError);

    public bool ShowUpdateAction =>
        UpdateState.Status is AppUpdateStatus.UpdateAvailable
        or AppUpdateStatus.ReadyToInstall
        or AppUpdateStatus.Downloading
        or AppUpdateStatus.Installing
        or AppUpdateStatus.Failed;

    public bool ShowWarningCard => !string.IsNullOrWhiteSpace(WarningSummaryText);

    public bool CanRunUpdateAction =>
        !IsBusy
        && UpdateState.Status is not AppUpdateStatus.Disabled
        && UpdateState.Status is not AppUpdateStatus.Checking
        && UpdateState.Status is not AppUpdateStatus.Downloading
        && UpdateState.Status is not AppUpdateStatus.Installing;

    public string CurrentVersionText => UpdateState.CurrentVersion;

    public string UpdateCardTitle => UpdateState.Status switch
    {
        AppUpdateStatus.Checking => "Проверяем обновления",
        AppUpdateStatus.UpdateAvailable => "Доступна новая версия",
        AppUpdateStatus.Downloading => "Загружаем обновление",
        AppUpdateStatus.ReadyToInstall => "Обновление готово",
        AppUpdateStatus.Installing => "Устанавливаем обновление",
        AppUpdateStatus.Failed => "Обновление недоступно",
        AppUpdateStatus.UpToDate => "Версия актуальна",
        _ => "Обновление клиента"
    };

    public string UpdateCardDescription
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(UpdateState.LastError))
            {
                return UpdateState.LastError!;
            }

            if (UpdateState.AvailableRelease is null)
            {
                return "Приложение само проверяет свежие версии и может обновиться поверх текущей установки.";
            }

            return UpdateState.Status switch
            {
                AppUpdateStatus.UpdateAvailable => $"Найдена версия {UpdateState.AvailableRelease.Version}. Можно скачать и установить поверх текущей.",
                AppUpdateStatus.Downloading => $"Загружаем версию {UpdateState.AvailableRelease.Version}. После проверки начнется установка.",
                AppUpdateStatus.ReadyToInstall => $"Версия {UpdateState.AvailableRelease.Version} уже загружена и готова к установке.",
                AppUpdateStatus.Installing => $"Устанавливаем версию {UpdateState.AvailableRelease.Version}. Приложение перезапустится автоматически.",
                AppUpdateStatus.UpToDate => $"Сейчас установлена актуальная версия {UpdateState.CurrentVersion}.",
                _ => $"Последний найденный релиз: {UpdateState.AvailableRelease.Version}."
            };
        }
    }

    public string UpdateActionText => UpdateState.Status switch
    {
        AppUpdateStatus.UpdateAvailable => $"Скачать {UpdateState.AvailableRelease?.Version}",
        AppUpdateStatus.ReadyToInstall => $"Установить {UpdateState.AvailableRelease?.Version}",
        AppUpdateStatus.Checking => "Проверяем...",
        AppUpdateStatus.Downloading => "Загружаем...",
        AppUpdateStatus.Installing => "Устанавливаем...",
        _ => "Проверить обновление"
    };

    public string EmptyStateTitle => "Добавьте первый сервер";

    public string EmptyStateText => "Добавьте конфиг, чтобы начать.";

    public string ConnectionBadgeText => ConnectionState.Status switch
    {
        RuntimeConnectionStatus.Connected => "В сети",
        RuntimeConnectionStatus.Connecting => "Подключение",
        RuntimeConnectionStatus.Disconnecting => "Отключение",
        RuntimeConnectionStatus.Degraded => "Есть связь",
        RuntimeConnectionStatus.Failed => "Ошибка",
        RuntimeConnectionStatus.Unsupported => "Недоступно",
        _ => "Не подключено"
    };

    public string ConnectionLabelText => ConnectionState.Status switch
    {
        RuntimeConnectionStatus.Connected => "Подключено",
        RuntimeConnectionStatus.Connecting => "Подключение",
        RuntimeConnectionStatus.Disconnecting => "Отключение",
        RuntimeConnectionStatus.Degraded => "Соединение нестабильно",
        RuntimeConnectionStatus.Failed => "Ошибка подключения",
        RuntimeConnectionStatus.Unsupported => "Недоступно",
        _ => HasProfiles ? "Нажмите для подключения" : "Добавьте конфиг"
    };

    public string RingAccentBrush => ConnectionState.Status switch
    {
        RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded => "#1FE3D5",
        RuntimeConnectionStatus.Connecting or RuntimeConnectionStatus.Disconnecting => "#67B3FF",
        RuntimeConnectionStatus.Failed or RuntimeConnectionStatus.Unsupported => "#F87171",
        _ => "#8B6CF6"
    };

    public string StatusTitle
    {
        get
        {
            if (SelectedProfile is null)
            {
                return "Ваш VPN";
            }

            if (ConnectionState.ProfileId == SelectedProfile.Id)
            {
                return ConnectionState.Status switch
                {
                    RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded => $"Подключено к {SelectedProfile.DisplayName}",
                    RuntimeConnectionStatus.Connecting => $"Подключаем {SelectedProfile.DisplayName}",
                    RuntimeConnectionStatus.Disconnecting => $"Отключаем {SelectedProfile.DisplayName}",
                    _ => SelectedProfile.DisplayName
                };
            }

            if (ConnectionState.ProfileId is not null
                && ConnectionState.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded
                && !string.IsNullOrWhiteSpace(ConnectionState.ProfileName))
            {
                return $"Активен {ConnectionState.ProfileName}";
            }

            return SelectedProfile.DisplayName;
        }
    }

    public string StatusDescription
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ConnectionState.LastError))
            {
                return ConnectionState.LastError!;
            }

            if (SelectedProfile is null)
            {
                return "Выберите сервер слева или добавьте новый конфиг из файла.";
            }

            return ConnectionState.Status switch
            {
                RuntimeConnectionStatus.Connected => "Соединение активно. Интернет идет через защищенный туннель.",
                RuntimeConnectionStatus.Connecting => "Поднимаем туннель и ждем первый полезный трафик.",
                RuntimeConnectionStatus.Disconnecting => "Аккуратно завершаем текущую сессию.",
                RuntimeConnectionStatus.Degraded => ShowWarningCard
                    ? WarningSummaryText
                    : "Связь есть, но приложение видит нестабильные сетевые сигналы.",
                RuntimeConnectionStatus.Failed => "Не удалось завершить подключение. Проверьте сервер и попробуйте снова.",
                RuntimeConnectionStatus.Unsupported => "На этом компьютере недоступен системный модуль подключения.",
                _ => "Профиль готов. Подключение запускается одним нажатием."
            };
        }
    }

    public string PrimaryActionText
    {
        get
        {
            if (!HasProfiles)
            {
                return "Добавить конфиг";
            }

            if (IsBusy)
            {
                return "Подождите";
            }

            if (ConnectionState.Status == RuntimeConnectionStatus.Disconnecting)
            {
                return "Отключаем";
            }

            if (ConnectionState.Status == RuntimeConnectionStatus.Connecting && ConnectionState.ProfileId == SelectedProfile?.Id)
            {
                return "Подключаем";
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
                return "Переключить";
            }

            return "Подключить";
        }
    }

    public async Task ExecutePrimaryActionAsync()
    {
        await ToggleConnectionAsync();
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

    public string SidebarSummaryText => HasProfiles
        ? $"{Profiles.Count} {PluralizeProfiles(Profiles.Count)}"
        : "Нет сохраненных серверов";

    public string SelectedProfileCaption => SelectedProfile is null
        ? "Подберите сервер и подключайтесь одним нажатием."
        : $"Добавлен {SelectedProfile.ImportedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}";

    public string EndpointText => SelectedProfile?.Endpoint ?? ConnectionState.Endpoint ?? "Не указан";

    public string EndpointHostText => ExtractHost(EndpointText) ?? "Сервер не выбран";

    public string AddressText => SelectedProfile?.Address ?? ConnectionState.Address ?? "Не указан";

    public string DnsText => FormatList(SelectedProfile?.DnsServers ?? ConnectionState.DnsServers, "Авто");

    public string AllowedIpsText => FormatList(SelectedProfile?.AllowedIps ?? ConnectionState.AllowedIps, "Авто");

    public string MtuText => SelectedProfile?.Mtu ?? ConnectionState.Mtu?.ToString() ?? "Авто";

    public string HandshakeText => ConnectionState.LatestHandshakeAtUtc is null
        ? "Пока нет"
        : $"{FormatRelative(ConnectionState.LatestHandshakeAtUtc.Value)} · {ConnectionState.LatestHandshakeAtUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm:ss}";

    public string LastSeenText => ConnectionState.UpdatedAtUtc == default
        ? "Пока нет данных"
        : FormatRelative(ConnectionState.UpdatedAtUtc);

    public string DownloadRateText => FormatSpeed(_downloadRateBytesPerSecond);

    public string UploadRateText => FormatSpeed(_uploadRateBytesPerSecond);

    public string DownloadTotalText => FormatBytes(ConnectionState.ReceivedBytes);

    public string UploadTotalText => FormatBytes(ConnectionState.SentBytes);

    public double DownloadActivityValue => NormalizeRate(_downloadRateBytesPerSecond);

    public double UploadActivityValue => NormalizeRate(_uploadRateBytesPerSecond);

    public string SessionDurationText => _sessionStartedAtUtc is null
        ? "00:00:00"
        : FormatDuration(DateTimeOffset.UtcNow - _sessionStartedAtUtc.Value);

    public string SessionCaptionText => ConnectionState.Status switch
    {
        RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded => "Сеанс активен",
        RuntimeConnectionStatus.Connecting => "Запускаем туннель",
        RuntimeConnectionStatus.Disconnecting => "Завершаем сеанс",
        _ => "Готово к запуску"
    };

    public string LastActionTitle => IsBusy ? "Выполняем действие" : "Последнее действие";

    public string WarningSummaryText => ConnectionState.LastError
        ?? ConnectionState.Warnings.FirstOrDefault()
        ?? string.Empty;

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
        UpdateState = _appUpdateService.CurrentState;

        var snapshot = await _listProfilesUseCase.ExecuteAsync();
        ApplySnapshot(snapshot, snapshot.ActiveProfileId);

        try
        {
            var restoredState = await _runtimeAdapter.TryRestoreAsync(snapshot.Profiles);
            if (restoredState.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Connecting or RuntimeConnectionStatus.Degraded)
            {
                var restoreTarget = restoredState.ProfileName
                                    ?? restoredState.Endpoint
                                    ?? "existing tunnel";

                _diagnosticsService.RecordConnectionLog(
                    $"Восстановлено активное подключение для '{restoreTarget}'.",
                    DiagnosticsLogLevel.Information,
                    "runtime",
                    restoredState.AdapterName);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Runtime restore failed during startup.");
            LastOperationMessage = "Приложение запущено. Не удалось восстановить прошлое подключение автоматически.";
        }

        await RefreshDiagnosticsAsync();
        _refreshTimer.Start();

        if (UpdatesEnabled)
        {
            _ = CheckForUpdatesInBackgroundAsync();
        }
    }

    public async Task ImportConfigAsync(string path)
    {
        try
        {
            IsBusy = true;
            NotifyViewStateChanged();

            var result = await _importProfileUseCase.ExecuteAsync(path);
            _diagnosticsService.RecordConnectionLog(
                $"Добавлен профиль '{result.Profile.DisplayName}'.",
                DiagnosticsLogLevel.Information,
                "import",
                "UI");

            LastOperationMessage = $"Профиль '{result.Profile.DisplayName}' готов к подключению.";
            ApplySnapshot(result.Snapshot, result.Profile.Id);
            await RefreshDiagnosticsAsync();
        }
        catch (Exception exception)
        {
            _diagnosticsService.RecordImportValidationError(exception, path);
            _diagnosticsService.RecordConnectionLog(
                $"Не удалось импортировать конфиг: {exception.Message}",
                DiagnosticsLogLevel.Error,
                "import",
                "UI");

            LastOperationMessage = $"Импорт не выполнен: {exception.Message}";
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
    private async Task RunUpdateActionAsync()
    {
        if (!CanRunUpdateAction)
        {
            return;
        }

        try
        {
            IsBusy = true;
            NotifyViewStateChanged();

            if (UpdateState.Status is AppUpdateStatus.UpdateAvailable or AppUpdateStatus.ReadyToInstall)
            {
                UpdateState = await _prepareAppUpdateUseCase.ExecuteAsync();
                if (UpdateState.Status == AppUpdateStatus.ReadyToInstall)
                {
                    UpdateState = await _launchPreparedAppUpdateUseCase.ExecuteAsync();
                    if (UpdateState.Status == AppUpdateStatus.Installing)
                    {
                        LastOperationMessage = $"Запущена установка версии {UpdateState.AvailableRelease?.Version}.";
                        ShutdownApplication();
                        return;
                    }
                }
            }
            else
            {
                UpdateState = await _checkForAppUpdatesUseCase.ExecuteAsync();
            }

            LastOperationMessage = UpdateState.Status switch
            {
                AppUpdateStatus.UpToDate => $"Версия {UpdateState.CurrentVersion} уже актуальна.",
                AppUpdateStatus.UpdateAvailable => $"Найдена версия {UpdateState.AvailableRelease?.Version}. Можно скачать и установить.",
                AppUpdateStatus.ReadyToInstall => $"Версия {UpdateState.AvailableRelease?.Version} готова к установке.",
                AppUpdateStatus.Failed => $"Не удалось обновить приложение: {UpdateState.LastError}",
                _ => UpdateCardDescription
            };
        }
        catch (Exception exception)
        {
            LastOperationMessage = $"Не удалось запустить обновление: {exception.Message}";
            _logger.LogError(exception, "Update action failed.");
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

            var shouldDisconnectCurrent = ConnectionState.Status is RuntimeConnectionStatus.Connected
                or RuntimeConnectionStatus.Degraded
                or RuntimeConnectionStatus.Connecting;

            if (shouldDisconnectCurrent && ConnectionState.ProfileId == SelectedProfile.Id)
            {
                _diagnosticsService.RecordConnectionLog(
                    $"Отключаем '{SelectedProfile.DisplayName}'.",
                    DiagnosticsLogLevel.Information,
                    "connect",
                    "UI");

                await _runtimeAdapter.DisconnectAsync();
                LastOperationMessage = $"Соединение с '{SelectedProfile.DisplayName}' завершено.";
                await RefreshDiagnosticsAsync();
                return;
            }

            if (shouldDisconnectCurrent && ConnectionState.ProfileId != SelectedProfile.Id)
            {
                _diagnosticsService.RecordConnectionLog(
                    $"Переключаемся с '{ConnectionState.ProfileName}' на '{SelectedProfile.DisplayName}'.",
                    DiagnosticsLogLevel.Information,
                    "connect",
                    "UI");

                await _runtimeAdapter.DisconnectAsync();
            }
            else
            {
                _diagnosticsService.RecordConnectionLog(
                    $"Запускаем подключение к '{SelectedProfile.DisplayName}'.",
                    DiagnosticsLogLevel.Information,
                    "connect",
                    "UI");
            }

            var state = await _runtimeAdapter.ConnectAsync(SelectedProfile);
            LastOperationMessage = state.Status switch
            {
                RuntimeConnectionStatus.Failed => $"Не удалось подключиться: {state.LastError}",
                RuntimeConnectionStatus.Unsupported => state.LastError ?? "На этой системе недоступен системный модуль подключения.",
                _ => $"Профиль '{SelectedProfile.DisplayName}' передан в систему."
            };

            await RefreshDiagnosticsAsync();
        }
        catch (Exception exception)
        {
            _diagnosticsService.RecordConnectionLog(
                $"Ошибка подключения: {exception.Message}",
                DiagnosticsLogLevel.Error,
                "connect",
                "UI");

            LastOperationMessage = $"Подключение не выполнено: {exception.Message}";
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
            NotifyViewStateChanged();

            var newName = RenameDraft.Trim();
            var snapshot = await _renameProfileUseCase.ExecuteAsync(SelectedProfile.Id, newName);
            _diagnosticsService.RecordConnectionLog(
                $"Профиль переименован в '{newName}'.",
                DiagnosticsLogLevel.Information,
                "profile",
                "UI");

            LastOperationMessage = "Название профиля обновлено.";
            ApplySnapshot(snapshot, SelectedProfile.Id);
        }
        catch (Exception exception)
        {
            _diagnosticsService.RecordConnectionLog(
                $"Ошибка переименования: {exception.Message}",
                DiagnosticsLogLevel.Error,
                "profile",
                "UI");

            LastOperationMessage = $"Не удалось изменить название: {exception.Message}";
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
            NotifyViewStateChanged();

            if (ConnectionState.ProfileId == SelectedProfile.Id
                && ConnectionState.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded or RuntimeConnectionStatus.Connecting)
            {
                await _runtimeAdapter.DisconnectAsync();
            }

            var deletedProfileId = SelectedProfile.Id;
            var deletedProfileName = SelectedProfile.DisplayName;
            var snapshot = await _deleteProfileUseCase.ExecuteAsync(deletedProfileId);

            _diagnosticsService.RecordConnectionLog(
                $"Профиль '{deletedProfileName}' удален.",
                DiagnosticsLogLevel.Warning,
                "profile",
                "UI");

            LastOperationMessage = $"Профиль '{deletedProfileName}' удален.";
            ApplySnapshot(snapshot, snapshot.ActiveProfileId);
            await RefreshDiagnosticsAsync();
        }
        catch (Exception exception)
        {
            _diagnosticsService.RecordConnectionLog(
                $"Ошибка удаления профиля: {exception.Message}",
                DiagnosticsLogLevel.Error,
                "profile",
                "UI");

            LastOperationMessage = $"Не удалось удалить профиль: {exception.Message}";
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
            _diagnosticsService.RecordConnectionLog(
                $"Ошибка выбора профиля: {exception.Message}",
                DiagnosticsLogLevel.Error,
                "profile",
                "UI");

            LastOperationMessage = $"Не удалось выбрать профиль: {exception.Message}";
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
            UpdateState = _appUpdateService.CurrentState;
            UpdateTrafficIndicators(previousState, snapshot.ConnectionState);

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

    private async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            UpdateState = await _checkForAppUpdatesUseCase.ExecuteAsync();
            NotifyViewStateChanged();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Background update check failed.");
        }
    }

    private void UpdateTrafficIndicators(ConnectionState previousState, ConnectionState nextState)
    {
        var now = nextState.UpdatedAtUtc == default
            ? DateTimeOffset.UtcNow
            : nextState.UpdatedAtUtc;

        var tunnelLive = nextState.Status is RuntimeConnectionStatus.Connected
            or RuntimeConnectionStatus.Degraded
            or RuntimeConnectionStatus.Connecting
            or RuntimeConnectionStatus.Disconnecting;

        var profileChanged = previousState.ProfileId != nextState.ProfileId;

        if (!tunnelLive)
        {
            _downloadRateBytesPerSecond = 0;
            _uploadRateBytesPerSecond = 0;
            _lastTrafficSampleUtc = null;
            _lastReceivedBytes = nextState.ReceivedBytes;
            _lastSentBytes = nextState.SentBytes;
            _sessionStartedAtUtc = null;
            return;
        }

        if (_sessionStartedAtUtc is null
            || profileChanged
            || previousState.Status is RuntimeConnectionStatus.Disconnected or RuntimeConnectionStatus.Failed or RuntimeConnectionStatus.Unsupported)
        {
            _sessionStartedAtUtc = nextState.LatestHandshakeAtUtc ?? now;
        }

        if (_lastTrafficSampleUtc is null
            || profileChanged
            || nextState.ReceivedBytes < _lastReceivedBytes
            || nextState.SentBytes < _lastSentBytes)
        {
            _downloadRateBytesPerSecond = 0;
            _uploadRateBytesPerSecond = 0;
            _lastTrafficSampleUtc = now;
            _lastReceivedBytes = nextState.ReceivedBytes;
            _lastSentBytes = nextState.SentBytes;
            return;
        }

        var seconds = Math.Max(0.25, (now - _lastTrafficSampleUtc.Value).TotalSeconds);
        _downloadRateBytesPerSecond = Math.Max(0, (nextState.ReceivedBytes - _lastReceivedBytes) / seconds);
        _uploadRateBytesPerSecond = Math.Max(0, (nextState.SentBytes - _lastSentBytes) / seconds);
        _lastTrafficSampleUtc = now;
        _lastReceivedBytes = nextState.ReceivedBytes;
        _lastSentBytes = nextState.SentBytes;
    }

    private void LogRuntimeTransition(ConnectionState previous, ConnectionState next)
    {
        if (previous.Status != next.Status)
        {
            _diagnosticsService.RecordConnectionLog(
                $"Состояние туннеля изменилось: {previous.Status} -> {next.Status}.",
                next.Status is RuntimeConnectionStatus.Failed or RuntimeConnectionStatus.Unsupported
                    ? DiagnosticsLogLevel.Error
                    : DiagnosticsLogLevel.Information,
                "runtime",
                next.AdapterName);
        }

        if (previous.LatestHandshakeAtUtc is null && next.LatestHandshakeAtUtc is not null)
        {
            _diagnosticsService.RecordConnectionLog(
                $"Получен первый handshake для '{next.ProfileName ?? "profile"}'.",
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
        OnPropertyChanged(nameof(ShowImportShortcut));
        OnPropertyChanged(nameof(UpdatesEnabled));
        OnPropertyChanged(nameof(ShowUpdateCard));
        OnPropertyChanged(nameof(ShowUpdateAction));
        OnPropertyChanged(nameof(ShowWarningCard));
        OnPropertyChanged(nameof(CanRunUpdateAction));
        OnPropertyChanged(nameof(CurrentVersionText));
        OnPropertyChanged(nameof(UpdateCardTitle));
        OnPropertyChanged(nameof(UpdateCardDescription));
        OnPropertyChanged(nameof(UpdateActionText));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(ConnectionBadgeText));
        OnPropertyChanged(nameof(ConnectionLabelText));
        OnPropertyChanged(nameof(RingAccentBrush));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusDescription));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(CanToggleConnection));
        OnPropertyChanged(nameof(CanRenameSelectedProfile));
        OnPropertyChanged(nameof(CanDeleteSelectedProfile));
        OnPropertyChanged(nameof(SidebarSummaryText));
        OnPropertyChanged(nameof(SelectedProfileCaption));
        OnPropertyChanged(nameof(EndpointText));
        OnPropertyChanged(nameof(EndpointHostText));
        OnPropertyChanged(nameof(AddressText));
        OnPropertyChanged(nameof(DnsText));
        OnPropertyChanged(nameof(AllowedIpsText));
        OnPropertyChanged(nameof(MtuText));
        OnPropertyChanged(nameof(HandshakeText));
        OnPropertyChanged(nameof(LastSeenText));
        OnPropertyChanged(nameof(DownloadRateText));
        OnPropertyChanged(nameof(UploadRateText));
        OnPropertyChanged(nameof(DownloadTotalText));
        OnPropertyChanged(nameof(UploadTotalText));
        OnPropertyChanged(nameof(DownloadActivityValue));
        OnPropertyChanged(nameof(UploadActivityValue));
        OnPropertyChanged(nameof(SessionDurationText));
        OnPropertyChanged(nameof(SessionCaptionText));
        OnPropertyChanged(nameof(LastActionTitle));
        OnPropertyChanged(nameof(WarningSummaryText));
        RunUpdateActionCommand.NotifyCanExecuteChanged();
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

    private static string FormatList(IReadOnlyList<string> items, string fallback)
    {
        return items.Count == 0 ? fallback : string.Join(", ", items);
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

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond <= 0)
        {
            return "0 KB/s";
        }

        string[] suffixes = ["B/s", "KB/s", "MB/s", "GB/s"];
        var value = bytesPerSecond;
        var suffixIndex = 0;

        while (value >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            value /= 1024;
            suffixIndex++;
        }

        return $"{value:0.##} {suffixes[suffixIndex]}";
    }

    private static double NormalizeRate(double bytesPerSecond)
    {
        if (bytesPerSecond <= 0)
        {
            return 0;
        }

        var megabytesPerSecond = bytesPerSecond / (1024d * 1024d);
        return Math.Clamp(megabytesPerSecond / 12d * 100d, 0d, 100d);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static string FormatRelative(DateTimeOffset timestampUtc)
    {
        var delta = DateTimeOffset.UtcNow - timestampUtc;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        if (delta.TotalSeconds < 10)
        {
            return "Только что";
        }

        if (delta.TotalMinutes < 1)
        {
            return $"{Math.Max(1, (int)Math.Floor(delta.TotalSeconds))} сек назад";
        }

        if (delta.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)Math.Floor(delta.TotalMinutes))} мин назад";
        }

        if (delta.TotalDays < 1)
        {
            return $"{Math.Max(1, (int)Math.Floor(delta.TotalHours))} ч назад";
        }

        return $"{Math.Max(1, (int)Math.Floor(delta.TotalDays))} дн назад";
    }

    private static string? ExtractHost(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        if (endpoint.StartsWith('['))
        {
            var bracketIndex = endpoint.IndexOf(']');
            return bracketIndex > 1 ? endpoint[1..bracketIndex] : endpoint;
        }

        var separatorIndex = endpoint.LastIndexOf(':');
        return separatorIndex > 0 ? endpoint[..separatorIndex] : endpoint;
    }

    private static string PluralizeProfiles(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;

        if (mod10 == 1 && mod100 != 11)
        {
            return "профиль";
        }

        if (mod10 is >= 2 and <= 4 && (mod100 < 12 || mod100 > 14))
        {
            return "профиля";
        }

        return "профилей";
    }

    private static void ShutdownApplication()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            desktopLifetime.Shutdown();
        }
    }
}
