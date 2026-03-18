using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.Extensions.Logging;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models.Updates;

namespace VpnClient.Infrastructure.Updates;

public sealed class JsonManifestAppUpdateService : IAppUpdateService
{
    private const string ApplicationId = "YourVpnClient";

    private readonly HttpClient _httpClient;
    private readonly AppUpdateOptions _options;
    private readonly ILogger<JsonManifestAppUpdateService> _logger;
    private readonly string _currentVersion;
    private readonly string _downloadRootDirectory;
    private readonly string _launcherSourcePath;
    private AppUpdateRelease? _cachedRelease;

    public JsonManifestAppUpdateService(
        AppUpdateOptions options,
        ILogger<JsonManifestAppUpdateService> logger)
    {
        _options = options;
        _logger = logger;
        _httpClient = new HttpClient();
        _currentVersion = AppVersionParser.GetCurrentVersion(Assembly.GetEntryAssembly() ?? typeof(JsonManifestAppUpdateService).Assembly);
        _downloadRootDirectory = string.IsNullOrWhiteSpace(options.DownloadRootDirectory)
            ? AppUpdateOptions.GetDefaultDownloadRootDirectory()
            : Path.GetFullPath(options.DownloadRootDirectory);
        _launcherSourcePath = Path.Combine(AppContext.BaseDirectory, "VpnClient.Updater.exe");
        CurrentState = string.IsNullOrWhiteSpace(_options.ManifestUrl)
            ? AppUpdateState.Disabled(_currentVersion, _options.ManifestUrl, "Update checks are disabled until Updates:ManifestUrl is configured.")
            : new AppUpdateState
            {
                Status = AppUpdateStatus.Idle,
                CurrentVersion = _currentVersion,
                ManifestUrl = _options.ManifestUrl
            };
    }

    public AppUpdateState CurrentState { get; private set; }

    public event Action<AppUpdateState>? StateChanged;

    public async Task<AppUpdateState> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ManifestUrl))
        {
            return UpdateState(AppUpdateState.Disabled(_currentVersion, _options.ManifestUrl, "Update checks are disabled until Updates:ManifestUrl is configured."));
        }

        UpdateState(CurrentState with
        {
            Status = AppUpdateStatus.Checking,
            LastError = null
        });

        try
        {
            var manifest = await _httpClient.GetFromJsonAsync<UpdateManifestDocument>(_options.ManifestUrl, cancellationToken);
            if (manifest?.Release is null)
            {
                throw new InvalidOperationException("The update manifest did not contain a release payload.");
            }

            if (!string.IsNullOrWhiteSpace(manifest.ApplicationId)
                && !string.Equals(manifest.ApplicationId, ApplicationId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The update manifest application id '{manifest.ApplicationId}' does not match '{ApplicationId}'.");
            }

            var release = ToRelease(manifest, manifest.Release);
            _cachedRelease = release;

            if (!AppVersionParser.IsNewerVersion(release.Version, _currentVersion))
            {
                return UpdateState(new AppUpdateState
                {
                    Status = AppUpdateStatus.UpToDate,
                    CurrentVersion = _currentVersion,
                    ManifestUrl = _options.ManifestUrl,
                    AvailableRelease = null,
                    LastCheckedAtUtc = DateTimeOffset.UtcNow
                });
            }

            var stagedPath = GetPackagePath(release);
            var isAlreadyStaged = File.Exists(stagedPath);
            if (isAlreadyStaged)
            {
                await UpdatePackageVerifier.VerifyAsync(stagedPath, release.Sha256, release.PackageCertificateThumbprint, cancellationToken);
            }

            return UpdateState(new AppUpdateState
            {
                Status = isAlreadyStaged ? AppUpdateStatus.ReadyToInstall : AppUpdateStatus.UpdateAvailable,
                CurrentVersion = _currentVersion,
                ManifestUrl = _options.ManifestUrl,
                AvailableRelease = release,
                DownloadedPackagePath = isAlreadyStaged ? stagedPath : null,
                LastCheckedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to check for application updates.");
            return UpdateState(CurrentState with
            {
                Status = AppUpdateStatus.Failed,
                LastError = exception.Message,
                LastCheckedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }

    public async Task<AppUpdateState> PrepareUpdateAsync(CancellationToken cancellationToken = default)
    {
        var release = CurrentState.AvailableRelease ?? _cachedRelease;
        if (release is null)
        {
            var state = await CheckForUpdatesAsync(cancellationToken);
            release = state.AvailableRelease;
        }

        if (release is null)
        {
            return CurrentState;
        }

        UpdateState(CurrentState with
        {
            Status = AppUpdateStatus.Downloading,
            LastError = null
        });

        var packagePath = GetPackagePath(release);
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);

        try
        {
            using var response = await _httpClient.GetAsync(release.PackageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = File.Create(packagePath))
            {
                await networkStream.CopyToAsync(fileStream, cancellationToken);
            }

            await UpdatePackageVerifier.VerifyAsync(packagePath, release.Sha256, release.PackageCertificateThumbprint, cancellationToken);

            return UpdateState(CurrentState with
            {
                Status = AppUpdateStatus.ReadyToInstall,
                AvailableRelease = release,
                DownloadedPackagePath = packagePath,
                LastCheckedAtUtc = DateTimeOffset.UtcNow,
                LastError = null
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to prepare application update.");
            return UpdateState(CurrentState with
            {
                Status = AppUpdateStatus.Failed,
                LastError = exception.Message,
                LastCheckedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }

    public async Task<AppUpdateState> LaunchPreparedUpdateAsync(CancellationToken cancellationToken = default)
    {
        var state = CurrentState.CanInstall
            ? CurrentState
            : await PrepareUpdateAsync(cancellationToken);

        if (!state.CanInstall || string.IsNullOrWhiteSpace(state.DownloadedPackagePath))
        {
            return state;
        }

        if (!File.Exists(_launcherSourcePath))
        {
            return UpdateState(state with
            {
                Status = AppUpdateStatus.Failed,
                LastError = $"Updater launcher was not found at '{_launcherSourcePath}'."
            });
        }

        try
        {
            var stagedLauncherPath = StageLauncherExecutable();
            var currentExePath = Environment.ProcessPath
                                 ?? Process.GetCurrentProcess().MainModule?.FileName
                                 ?? throw new InvalidOperationException("Unable to resolve the current application executable path.");
            var currentProcessId = Environment.ProcessId;

            var arguments = new string[]
            {
                "--package", state.DownloadedPackagePath,
                "--restart", currentExePath,
                "--wait-pid", currentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            var processStartInfo = new ProcessStartInfo
            {
                FileName = stagedLauncherPath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(stagedLauncherPath) ?? AppContext.BaseDirectory,
                Arguments = BuildArguments(arguments)
            };

            var started = Process.Start(processStartInfo);
            if (started is null)
            {
                throw new InvalidOperationException("Failed to launch the external update installer.");
            }

            return UpdateState(state with
            {
                Status = AppUpdateStatus.Installing,
                LastError = null
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to launch prepared update.");
            return UpdateState(state with
            {
                Status = AppUpdateStatus.Failed,
                LastError = exception.Message
            });
        }
    }

    private AppUpdateRelease ToRelease(UpdateManifestDocument manifest, UpdateReleaseDocument release)
    {
        if (string.IsNullOrWhiteSpace(release.Version))
        {
            throw new InvalidOperationException("The update manifest release did not specify a version.");
        }

        if (string.IsNullOrWhiteSpace(release.PackageUrl))
        {
            throw new InvalidOperationException("The update manifest release did not specify a package URL.");
        }

        if (string.IsNullOrWhiteSpace(release.Sha256))
        {
            throw new InvalidOperationException("The update manifest release did not specify a package SHA-256.");
        }

        var channel = string.IsNullOrWhiteSpace(release.Channel)
            ? manifest.Channel ?? _options.Channel
            : release.Channel;

        return new AppUpdateRelease(
            release.Version,
            release.PackageUrl,
            release.Sha256,
            release.SizeBytes,
            release.PublishedAtUtc,
            release.ReleaseNotes,
            release.IsMandatory,
            release.MinimumSupportedVersion,
            channel,
            release.PackageCertificateThumbprint);
    }

    private string GetPackagePath(AppUpdateRelease release)
    {
        var packageFileName = Path.GetFileName(new Uri(release.PackageUrl, UriKind.Absolute).LocalPath);
        return Path.Combine(_downloadRootDirectory, SanitizeVersion(release.Version), packageFileName);
    }

    private string StageLauncherExecutable()
    {
        var launcherDirectory = Path.Combine(_downloadRootDirectory, "launcher");
        Directory.CreateDirectory(launcherDirectory);
        var stagedLauncherPath = Path.Combine(launcherDirectory, $"VpnClient.Updater-{Guid.NewGuid():N}.exe");
        File.Copy(_launcherSourcePath, stagedLauncherPath, overwrite: true);
        return stagedLauncherPath;
    }

    private static string SanitizeVersion(string version)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(version.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
    }

    private static string BuildArguments(IEnumerable<string> arguments)
    {
        return string.Join(" ", arguments.Select(argument => $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""));
    }

    private AppUpdateState UpdateState(AppUpdateState state)
    {
        CurrentState = state;
        StateChanged?.Invoke(state);
        return state;
    }
}
