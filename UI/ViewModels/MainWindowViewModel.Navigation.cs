using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VpnClient.Core.Models;

namespace VpnClient.UI.ViewModels;

public enum ShellScreen
{
    Home,
    ServerSelection,
    Settings
}

public partial class MainWindowViewModel
{
    [ObservableProperty]
    private ShellScreen currentScreen = ShellScreen.Home;

    [ObservableProperty]
    private string serverSearchText = string.Empty;

    [ObservableProperty]
    private MockLocationOption? selectedMockLocation = new("DE", "Франкфурт, Германия", string.Empty, 4);

    [ObservableProperty]
    private bool autoConnectEnabled = true;

    [ObservableProperty]
    private bool killSwitchEnabled;

    [ObservableProperty]
    private bool notificationsEnabled = true;

    [ObservableProperty]
    private bool launchToTrayEnabled = true;

    public ObservableCollection<MockLocationOption> MockLocations { get; } =
    [
        new("DE", "Франкфурт, Германия", string.Empty, 4),
        new("NL", "Амстердам, Нидерланды", string.Empty, 4),
        new("US", "Нью-Йорк, США", string.Empty, 3),
        new("JP", "Токио, Япония", string.Empty, 3),
        new("CA", "Торонто, Канада", string.Empty, 4),
        new("AU", "Сидней, Австралия", string.Empty, 2),
        new("SG", "Сингапур", string.Empty, 3)
    ];

    public bool ShowMainShell => !ShowOnboardingScreen && !ShowAccountScreen;

    public bool IsHomeScreen => CurrentScreen == ShellScreen.Home;

    public bool IsServerSelectionScreen => CurrentScreen == ShellScreen.ServerSelection;

    public bool IsSettingsScreen => CurrentScreen == ShellScreen.Settings;

    public IReadOnlyList<MockLocationOption> VisibleMockLocations =>
        string.IsNullOrWhiteSpace(ServerSearchText)
            ? MockLocations
            : MockLocations
                .Where(location => location.Name.Contains(ServerSearchText, StringComparison.OrdinalIgnoreCase))
                .ToArray();

    public string CurrentLocationName => SelectedMockLocation?.Name ?? "Франкфурт, Германия";

    public string CurrentLocationSubtitle => SelectedMockLocation?.Subtitle ?? string.Empty;

    public string HomeStatusLabel => ConnectionState.Status switch
    {
        RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded => "Защищено",
        RuntimeConnectionStatus.Connecting => "Подключаемся",
        RuntimeConnectionStatus.Disconnecting => "Отключаемся",
        RuntimeConnectionStatus.Failed => "Ошибка подключения",
        RuntimeConnectionStatus.Unsupported => "Недоступно",
        _ => "Не защищено"
    };

    public bool ShowHomeSessionLabel => ConnectionState.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded;

    public string HomeSessionLabel => ShowHomeSessionLabel ? SessionDurationText : string.Empty;

    public bool ShowHomeUpdateAlert => ShowUpdateAction;

    public bool ShowHomeFooterText => !ShowHomeUpdateAlert;

    public string HomeFooterText => ConnectionState.Status switch
    {
        RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded => "Нажми, чтобы отключить защиту",
        RuntimeConnectionStatus.Connecting => "Подключаем защиту...",
        RuntimeConnectionStatus.Disconnecting => "Отключаем защиту...",
        _ => "Нажми для защиты своего соединения"
    };

    public string HomeUpdateAlertText => "Требуется обновление!";

    public string SettingsVersionText => $"Версия {CurrentVersionText}";

    public string SettingsUpdateActionText => ShowUpdateAction ? UpdateActionText : "Проверить обновления";

    [RelayCommand]
    private void OpenServerSelection()
    {
        CurrentScreen = ShellScreen.ServerSelection;
        NotifyViewStateChanged();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        CurrentScreen = ShellScreen.Settings;
        NotifyViewStateChanged();
    }

    [RelayCommand]
    private void ReturnHome()
    {
        CurrentScreen = ShellScreen.Home;
        NotifyViewStateChanged();
    }

    [RelayCommand]
    private void SelectMockLocation(MockLocationOption? location)
    {
        if (location is null)
        {
            return;
        }

        SelectedMockLocation = location;
        CurrentScreen = ShellScreen.Home;
        NotifyViewStateChanged();
    }

    partial void OnServerSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(VisibleMockLocations));
    }

    partial void OnSelectedMockLocationChanged(MockLocationOption? value)
    {
        OnPropertyChanged(nameof(CurrentLocationName));
        OnPropertyChanged(nameof(CurrentLocationSubtitle));
    }

    partial void OnCurrentScreenChanged(ShellScreen value)
    {
        OnPropertyChanged(nameof(IsHomeScreen));
        OnPropertyChanged(nameof(IsServerSelectionScreen));
        OnPropertyChanged(nameof(IsSettingsScreen));
    }
}
