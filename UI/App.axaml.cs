using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using VpnClient.UI.ViewModels;

namespace VpnClient.UI;

public partial class App : Avalonia.Application
{
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;
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
            _mainWindow = Program.Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = _mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _mainWindow.Closing += OnMainWindowClosing;
            desktop.Exit += OnDesktopExit;
            TryCreateTrayIcon();
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
            var openItem = new NativeMenuItem("Открыть");
            openItem.Click += (_, _) => ShowMainWindow();

            var exitItem = new NativeMenuItem("Закрыть");
            exitItem.Click += async (_, _) => await ExitApplicationAsync();

            var menu = new NativeMenu
            {
                openItem,
                new NativeMenuItemSeparator(),
                exitItem
            };

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(Environment.ProcessPath!),
                ToolTipText = "Your VPN Client",
                Menu = menu,
                IsVisible = true
            };

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
            var viewModel = Program.Services.GetRequiredService<MainWindowViewModel>();
            await viewModel.DisconnectOnApplicationExitAsync();
        }
        catch
        {
            // Best-effort shutdown cleanup.
        }
    }
}
