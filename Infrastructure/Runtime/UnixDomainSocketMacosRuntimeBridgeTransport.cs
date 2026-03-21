using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using VpnClient.Core.Interfaces;

namespace VpnClient.Infrastructure.Runtime;

public sealed class UnixDomainSocketMacosRuntimeBridgeTransport : IMacosRuntimeBridgeTransport
{
    private const int ConnectTimeoutMilliseconds = 750;
    private const int ResponseTimeoutMilliseconds = 1500;
    private const int HelperStartupTimeoutMilliseconds = 5000;
    private const int HelperProbeDelayMilliseconds = 125;
    private const string SocketPathEnvironmentVariable = "ETOVPN_RUNTIME_SOCKET_PATH";

    private readonly string _socketPath;
    private readonly string? _helperExecutablePath;
    private readonly ILogger<UnixDomainSocketMacosRuntimeBridgeTransport>? _logger;
    private readonly SemaphoreSlim _launchGate = new(1, 1);
    private Process? _helperProcess;

    public UnixDomainSocketMacosRuntimeBridgeTransport(ILogger<UnixDomainSocketMacosRuntimeBridgeTransport> logger)
        : this(
            MacosRuntimeBridgeProtocol.DefaultSocketPath,
            ResolveDefaultHelperExecutablePath(AppContext.BaseDirectory),
            logger)
    {
    }

    internal UnixDomainSocketMacosRuntimeBridgeTransport(
        string socketPath,
        string? helperExecutablePath = null,
        ILogger<UnixDomainSocketMacosRuntimeBridgeTransport>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(socketPath))
        {
            throw new ArgumentException("Socket path is required.", nameof(socketPath));
        }

        _socketPath = Path.GetFullPath(socketPath);
        _helperExecutablePath = string.IsNullOrWhiteSpace(helperExecutablePath)
            ? null
            : Path.GetFullPath(helperExecutablePath);
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            using var socket = await TryConnectAsync(cancellationToken)
                ?? await EnsureBridgeAvailableAsync(cancellationToken);
            return socket.Connected;
        }
        catch
        {
            return false;
        }
    }

    public async Task SendAsync(JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var socket = await EnsureBridgeAvailableAsync(cancellationToken);
        using var stream = new NetworkStream(socket, ownsSocket: false);
        await WriteAsync(stream, payload, cancellationToken);
    }

    public async Task<JsonDocument> RequestAsync(JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var socket = await EnsureBridgeAvailableAsync(cancellationToken);
        using var stream = new NetworkStream(socket, ownsSocket: false);
        await WriteAsync(stream, payload, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ResponseTimeoutMilliseconds);

        using var reader = new StreamReader(stream, new UTF8Encoding(false), leaveOpen: true);
        var line = await reader.ReadLineAsync().WaitAsync(timeoutCts.Token);
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new InvalidOperationException("MacOS runtime bridge returned an empty response.");
        }

        return JsonDocument.Parse(line);
    }

    private UnixDomainSocketEndPoint CreateEndpoint()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("The macOS runtime bridge transport is available only on macOS.");
        }

        return new UnixDomainSocketEndPoint(_socketPath);
    }

    private async Task<Socket> ConnectAsync(CancellationToken cancellationToken)
    {
        var endpoint = CreateEndpoint();
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ConnectTimeoutMilliseconds);
            await socket.ConnectAsync(endpoint, timeoutCts.Token);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private async Task<Socket?> TryConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await ConnectAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<Socket> EnsureBridgeAvailableAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("The macOS runtime bridge transport is available only on macOS.");
        }

        var socket = await TryConnectAsync(cancellationToken);
        if (socket is not null)
        {
            return socket;
        }

        await _launchGate.WaitAsync(cancellationToken);
        try
        {
            socket = await TryConnectAsync(cancellationToken);
            if (socket is not null)
            {
                return socket;
            }

            StartHelperProcess();
        }
        finally
        {
            _launchGate.Release();
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(HelperStartupTimeoutMilliseconds);

        while (!timeoutCts.IsCancellationRequested)
        {
            socket = await TryConnectAsync(timeoutCts.Token);
            if (socket is not null)
            {
                return socket;
            }

            if (_helperProcess is { HasExited: true })
            {
                throw new InvalidOperationException(
                    $"The macOS runtime helper exited immediately with code {_helperProcess.ExitCode}.");
            }

            try
            {
                await Task.Delay(HelperProbeDelayMilliseconds, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                break;
            }
        }

        throw new InvalidOperationException(
            $"The macOS runtime bridge socket '{_socketPath}' did not become available after starting the helper.");
    }

    private void StartHelperProcess()
    {
        if (string.IsNullOrWhiteSpace(_helperExecutablePath))
        {
            throw new InvalidOperationException(
                "The macOS runtime helper executable was not found in the desktop app bundle.");
        }

        if (!File.Exists(_helperExecutablePath))
        {
            throw new FileNotFoundException(
                $"The macOS runtime helper executable was not found at '{_helperExecutablePath}'.",
                _helperExecutablePath);
        }

        if (_helperProcess is not null)
        {
            try
            {
                if (!_helperProcess.HasExited)
                {
                    return;
                }
            }
            catch
            {
                // Ignore process state race and relaunch below.
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _helperExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(_helperExecutablePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment[SocketPathEnvironmentVariable] = _socketPath;

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        process.Exited += (_, _) =>
        {
            try
            {
                _logger?.LogWarning(
                    "The macOS runtime helper exited. ExitCode={ExitCode}; HelperPath={HelperPath}",
                    process.ExitCode,
                    _helperExecutablePath);
            }
            catch
            {
                // Ignore logging failures from the exit callback.
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"The macOS runtime helper could not be started from '{_helperExecutablePath}'.");
        }

        _helperProcess = process;
        _logger?.LogInformation(
            "Started macOS runtime helper. HelperPath={HelperPath}; SocketPath={SocketPath}",
            _helperExecutablePath,
            _socketPath);
    }

    internal static string? ResolveDefaultHelperExecutablePath(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        var helperAppExecutable = Path.GetFullPath(Path.Combine(
            baseDirectory,
            "..",
            "Helpers",
            "etoVPNMacBridge.app",
            "Contents",
            "MacOS",
            "etoVPNMacBridge"));

        if (File.Exists(helperAppExecutable))
        {
            return helperAppExecutable;
        }

        var standaloneHelper = Path.GetFullPath(Path.Combine(
            baseDirectory,
            "..",
            "Helpers",
            "etoVPNMacBridge"));

        return File.Exists(standaloneHelper)
            ? standaloneHelper
            : helperAppExecutable;
    }

    private static async Task WriteAsync(Stream stream, JsonObject payload, CancellationToken cancellationToken)
    {
        var json = payload.ToJsonString();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true)
        {
            AutoFlush = true
        };

        await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
    }
}
