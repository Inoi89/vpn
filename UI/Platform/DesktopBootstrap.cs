using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;
using VpnClient.Application.Imports;
using VpnClient.Application.Profiles;
using VpnClient.Application.Updates;
using VpnClient.Core.Interfaces;
using VpnClient.Infrastructure.Auth;
using VpnClient.Infrastructure.Diagnostics;
using VpnClient.Infrastructure.Import;
using VpnClient.Infrastructure.Persistence;
using VpnClient.Infrastructure.Runtime;
using VpnClient.Infrastructure.Services;
using VpnClient.Infrastructure.Updates;

namespace VpnClient.UI.Platform;

internal static class DesktopBootstrap
{
    public static bool IsWindows => OperatingSystem.IsWindows();
    public static bool IsMacOS => OperatingSystem.IsMacOS();

    [SupportedOSPlatform("windows")]
    public static bool TryAcquireSingleInstance(out SingleInstanceCoordinator? coordinator)
    {
        if (!IsWindows)
        {
            coordinator = null;
            return true;
        }

        return SingleInstanceCoordinator.TryAcquirePrimary(out coordinator);
    }

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IImportService, AmneziaImportService>();
        services.AddSingleton<IProfileRepository, JsonProfileRepository>();
        services.AddSingleton<IClientSettingsService, JsonClientSettingsService>();
        services.AddSingleton<ImportTunnelConfigUseCase>();
        services.AddSingleton<ImportProfileUseCase>();
        services.AddSingleton<AddProfileUseCase>();
        services.AddSingleton<ListProfilesUseCase>();
        services.AddSingleton<RenameProfileUseCase>();
        services.AddSingleton<DeleteProfileUseCase>();
        services.AddSingleton<SetActiveProfileUseCase>();
        services.AddSingleton<CheckForAppUpdatesUseCase>();
        services.AddSingleton<PrepareAppUpdateUseCase>();
        services.AddSingleton<LaunchPreparedAppUpdateUseCase>();

        services.AddSingleton(configuration.GetSection("ProductPlatform").Get<ProductPlatformOptions>() ?? new ProductPlatformOptions());
        services.AddSingleton<IProductPlatformAuthService, JsonProductPlatformAuthService>();
        services.AddSingleton<ILocalDeviceIdentityService, JsonLocalDeviceIdentityService>();
        services.AddSingleton<IProductPlatformEnrollmentService, ProductPlatformEnrollmentService>();
        services.AddSingleton<IRuntimeEnvironment, DefaultRuntimeEnvironment>();

        if (IsWindows)
        {
            ConfigureWindowsServices(services, configuration);
            return;
        }

        if (IsMacOS)
        {
            ConfigureMacosServices(services, configuration);
            return;
        }

        ConfigureUnsupportedPlatformServices(services, configuration);
    }

    private static void ConfigureWindowsServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IWintunService, WintunService>();
        services.AddSingleton<IRuntimeCommandExecutor, ProcessRuntimeCommandExecutor>();
        services.AddSingleton<IWindowsRuntimeAssetLocator, WindowsRuntimeAssetLocator>();
        services.AddSingleton<IAmneziaRuntimeConfigStore, ProgramDataAmneziaRuntimeConfigStore>();
        services.AddSingleton<IAmneziaDaemonTransport, NamedPipeAmneziaDaemonTransport>();
        services.AddSingleton<IKillSwitchService, WindowsKillSwitchService>();
        services.AddSingleton<BundledAmneziaRuntimeAdapter>();
        services.AddSingleton<WindowsFirstVpnRuntimeAdapter>();
        services.AddSingleton<AmneziaDaemonRuntimeAdapter>();
        services.AddSingleton<IVpnRuntimeAdapter, HybridVpnRuntimeAdapter>();
        services.AddSingleton<IVpnDiagnosticsService, VpnDiagnosticsService>();
        services.AddSingleton(configuration.GetSection("Updates").Get<AppUpdateOptions>() ?? new AppUpdateOptions());
        services.AddSingleton<IAppUpdateService, JsonManifestAppUpdateService>();
    }

    private static void ConfigureMacosServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMacosRuntimeBridgeTransport, UnixDomainSocketMacosRuntimeBridgeTransport>();
        services.AddSingleton<IKillSwitchService, MacosNoOpKillSwitchService>();
        services.AddSingleton<IVpnRuntimeAdapter, MacosVpnRuntimeAdapter>();
        services.AddSingleton<IVpnDiagnosticsService, VpnDiagnosticsService>();
        services.AddSingleton(configuration.GetSection("Updates").Get<AppUpdateOptions>() ?? new AppUpdateOptions());
        services.AddSingleton<IAppUpdateService, NoopAppUpdateService>();
    }

    private static void ConfigureUnsupportedPlatformServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IVpnRuntimeAdapter, UnsupportedVpnRuntimeAdapter>();
        services.AddSingleton<IVpnDiagnosticsService, VpnDiagnosticsService>();
        services.AddSingleton<IKillSwitchService, NoopKillSwitchService>();
        services.AddSingleton(configuration.GetSection("Updates").Get<AppUpdateOptions>() ?? new AppUpdateOptions());
        services.AddSingleton<IAppUpdateService, NoopAppUpdateService>();
    }
}
