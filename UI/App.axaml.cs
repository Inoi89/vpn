using System.ComponentModel;
using System.IO;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using VpnClient.UI.ViewModels;

namespace VpnClient.UI;

[SupportedOSPlatform("windows")]
public partial class App : Avalonia.Application
{
    private static readonly Uri ShieldIconUri = new("avares://VpnClient.UI/Assets/shield.ico");

    private MainWindow? _mainWindow;
    private MainWindowViewModel? _viewModel;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _toggleVpnItem;
    private NativeMenuItem? _showWindowItem;
    private NativeMenuItem? _updateItem;
    private NativeMenuItem? _exitItem;
    private byte[]? _iconBytes;
    private bool _allowWindowClose;
    private bool _shutdownCleanupStarted;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _iconBytes = TryLoadIconBytes();
            _mainWindow = Program.Services.GetRequiredService<MainWindow>();
            _viewModel = _mainWindow.DataContext as MainWindowViewModel ?? Program.Services.GetRequiredService<MainWindowViewModel>();

            if (_mainWindow.DataContext is null)
            {
                _mainWindow.DataContext = _viewModel;
            }

            _mainWindow.Icon = CreateWindowIcon();
            desktop.MainWindow = _mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _mainWindow.Closing += OnMainWindowClosing;
            desktop.Exit += OnDesktopExit;

            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }

            if (Program.SingleInstance is not null)
            {
                Program.SingleInstance.ActivationRequested += OnActivationRequested;
            }

            TryCreateTrayIcon();
            UpdateTrayMenuState();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void TryCreateTrayIcon()
    {
        if (_mainWindow is null)
        {
            return;
        }

        try
        {
            _toggleVpnItem = new NativeMenuItem("Подключить VPN");
            _toggleVpnItem.Click += OnToggleVpnFromTrayClick;

            _showWindowItem = new NativeMenuItem("Развернуть");
            _showWindowItem.Click += (_, _) => ShowMainWindow();

            _updateItem = new NativeMenuItem("Проверить обновление");
            _updateItem.Click += OnUpdateFromTrayClick;

            _exitItem = new NativeMenuItem("Закрыть");
            _exitItem.Click += OnExitFromTrayClick;

            var menu = new NativeMenu
            {
                _toggleVpnItem,
                _showWindowItem,
                _updateItem,
                new NativeMenuItemSeparator(),
                _exitItem
            };

            _trayIcon = new TrayIcon
            {
                Icon = CreateWindowIcon() ?? new WindowIcon(Environment.ProcessPath!),
                ToolTipText = "Your VPN Client",
                Menu = menu,
                IsVisible = true
            };
            _trayIcon.Clicked += (_, _) => ShowMainWindow();

            TrayIcon.SetIcons(this, new TrayIcons
            {
                _trayIcon
            });
        }
        catch
        {
            _trayIcon = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateTrayMenuState();
    }

    private void OnActivationRequested()
    {
        Dispatcher.UIThread.Post(ShowMainWindow);
    }

    private async void OnToggleVpnFromTrayClick(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.ExecuteTrayToggleAsync();
        UpdateTrayMenuState();
    }

    private async void OnUpdateFromTrayClick(object? sender, EventArgs e)
    {
        if (_viewModel is null || !_viewModel.CanTrayUpdate)
        {
            return;
        }

        await _viewModel.ExecuteUpdateActionAsync();
        UpdateTrayMenuState();
    }

    private async void OnExitFromTrayClick(object? sender, EventArgs e)
    {
        await ExitApplicationAsync();
    }

    private void UpdateTrayMenuState()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_toggleVpnItem is not null)
        {
            _toggleVpnItem.Header = _viewModel.TrayToggleText;
            _toggleVpnItem.IsEnabled = _viewModel.CanTrayToggle;
        }

        if (_showWindowItem is not null)
        {
            _showWindowItem.IsEnabled = true;
        }

        if (_updateItem is not null)
        {
            _updateItem.Header = _viewModel.TrayUpdateText;
            _updateItem.IsEnabled = _viewModel.CanTrayUpdate;
            _updateItem.IsVisible = _viewModel.UpdatesEnabled;
        }

        if (_trayIcon is not null)
        {
            _trayIcon.ToolTipText = _viewModel.TrayToolTipText;
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private async Task ExitApplicationAsync()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        await DisconnectActiveTunnelAsync();

        _allowWindowClose = true;
        desktop.Shutdown();
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowWindowClose || _trayIcon is null || _mainWindow is null)
        {
            return;
        }

        e.Cancel = true;
        _mainWindow.Hide();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        DisconnectActiveTunnelAsync().GetAwaiter().GetResult();

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (Program.SingleInstance is not null)
        {
            Program.SingleInstance.ActivationRequested -= OnActivationRequested;
        }

        _trayIcon?.Dispose();
    }

    private async Task DisconnectActiveTunnelAsync()
    {
        if (_shutdownCleanupStarted)
        {
            return;
        }

        _shutdownCleanupStarted = true;

        try
        {
            var viewModel = _viewModel ?? Program.Services.GetRequiredService<MainWindowViewModel>();
            await viewModel.DisconnectOnApplicationExitAsync();
        }
        catch
        {
            // Best-effort shutdown cleanup.
        }
    }

    private static byte[]? TryLoadIconBytes()
    {
        try
        {
            using var stream = AssetLoader.Open(ShieldIconUri);
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private WindowIcon? CreateWindowIcon()
    {
        if (_iconBytes is null || _iconBytes.Length == 0)
        {
            return null;
        }

        return new WindowIcon(new MemoryStream(_iconBytes, writable: false));
    }
}
