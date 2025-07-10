using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VpnClient.Core.Interfaces;
using VpnClient.Infrastructure.Services;
using VpnClient.UI.ViewModels;

namespace VpnClient.UI;

class Program
{
    public static IServiceProvider Services { get; private set; } = default!;

    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IWintunService, WintunService>();
        builder.Services.AddSingleton<IVpnService, VpnService>();
        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddSingleton<MainWindowViewModel>();

        var host = builder.Build();
        Services = host.Services;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
