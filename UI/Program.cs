using Avalonia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VpnClient.Application.Imports;
using VpnClient.Application.Profiles;
using VpnClient.Application.Updates;
using VpnClient.Core.Interfaces;
using VpnClient.Infrastructure.Diagnostics;
using VpnClient.Infrastructure.Import;
using VpnClient.Infrastructure.Persistence;
using VpnClient.Infrastructure.Runtime;
using VpnClient.Infrastructure.Services;
using VpnClient.Infrastructure.Updates;
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

        builder.Services.AddSingleton<IImportService, AmneziaImportService>();
        builder.Services.AddSingleton<IProfileRepository, JsonProfileRepository>();
        builder.Services.AddSingleton<ImportTunnelConfigUseCase>();
        builder.Services.AddSingleton<ImportProfileUseCase>();
        builder.Services.AddSingleton<AddProfileUseCase>();
        builder.Services.AddSingleton<ListProfilesUseCase>();
        builder.Services.AddSingleton<RenameProfileUseCase>();
        builder.Services.AddSingleton<DeleteProfileUseCase>();
        builder.Services.AddSingleton<SetActiveProfileUseCase>();
        builder.Services.AddSingleton<CheckForAppUpdatesUseCase>();
        builder.Services.AddSingleton<PrepareAppUpdateUseCase>();
        builder.Services.AddSingleton<LaunchPreparedAppUpdateUseCase>();

        builder.Services.AddSingleton<IWintunService, WintunService>();
        builder.Services.AddSingleton<IRuntimeEnvironment, DefaultRuntimeEnvironment>();
        builder.Services.AddSingleton<IRuntimeCommandExecutor, ProcessRuntimeCommandExecutor>();
        builder.Services.AddSingleton<IWindowsRuntimeAssetLocator, WindowsRuntimeAssetLocator>();
        builder.Services.AddSingleton<IAmneziaRuntimeConfigStore, ProgramDataAmneziaRuntimeConfigStore>();
        builder.Services.AddSingleton<IAmneziaDaemonTransport, NamedPipeAmneziaDaemonTransport>();
        builder.Services.AddSingleton<BundledAmneziaRuntimeAdapter>();
        builder.Services.AddSingleton<WindowsFirstVpnRuntimeAdapter>();
        builder.Services.AddSingleton<AmneziaDaemonRuntimeAdapter>();
        builder.Services.AddSingleton<IVpnRuntimeAdapter, HybridVpnRuntimeAdapter>();
        builder.Services.AddSingleton<IVpnDiagnosticsService, VpnDiagnosticsService>();
        builder.Services.AddSingleton(builder.Configuration.GetSection("Updates").Get<AppUpdateOptions>() ?? new AppUpdateOptions());
        builder.Services.AddSingleton<IAppUpdateService, JsonManifestAppUpdateService>();

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
