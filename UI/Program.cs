using Avalonia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VpnClient.Application.Imports;
using VpnClient.Application.Profiles;
using VpnClient.Application.Updates;
using VpnClient.Core.Interfaces;
using VpnClient.Infrastructure.Logging;
using VpnClient.Infrastructure.Auth;
using VpnClient.UI.Platform;
using VpnClient.UI.ViewModels;

namespace VpnClient.UI;

class Program
{
    public static IServiceProvider Services { get; private set; } = default!;
    public static SingleInstanceCoordinator? SingleInstance { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        SingleInstanceCoordinator? singleInstance = null;

        if (OperatingSystem.IsWindows() && !DesktopBootstrap.TryAcquireSingleInstance(out singleInstance))
        {
            return;
        }

        SingleInstance = singleInstance;

        try
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new FileLoggerProvider());
            builder.Services.AddLogging();
            DesktopBootstrap.ConfigureServices(builder.Services, builder.Configuration);

            builder.Services.AddSingleton<MainWindow>();
            builder.Services.AddSingleton<MainWindowViewModel>();

            var host = builder.Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Desktop client launch started.");

            var productPlatformOptions = host.Services.GetRequiredService<ProductPlatformOptions>();
            logger.LogInformation(
                "Product platform endpoints configured. ApiBaseUrl={ApiBaseUrl}; CabinetUrl={CabinetUrl}",
                productPlatformOptions.ApiBaseUrl,
                productPlatformOptions.CabinetUrl);
            logger.LogInformation(
                "Application paths configured. BaseDirectory={BaseDirectory}; CurrentDirectory={CurrentDirectory}",
                AppContext.BaseDirectory,
                Environment.CurrentDirectory);

            Services = host.Services;

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            if (OperatingSystem.IsWindows())
            {
                SingleInstance?.Dispose();
            }

            SingleInstance = null;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
