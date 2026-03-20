using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VpnClient.Core.Interfaces;

namespace VpnClient.Infrastructure.Runtime;

public sealed class UnixDomainSocketMacosRuntimeBridgeTransport : IMacosRuntimeBridgeTransport
{
    private const int ConnectTimeoutMilliseconds = 750;
    private const int ResponseTimeoutMilliseconds = 1500;

    private readonly string _socketPath;

    public UnixDomainSocketMacosRuntimeBridgeTransport()
        : this(MacosRuntimeBridgeProtocol.DefaultSocketPath)
    {
    }

    public UnixDomainSocketMacosRuntimeBridgeTransport(string socketPath)
    {
        if (string.IsNullOrWhiteSpace(socketPath))
        {
            throw new ArgumentException("Socket path is required.", nameof(socketPath));
        }

        _socketPath = Path.GetFullPath(socketPath);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            using var socket = await ConnectAsync(cancellationToken);
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

        using var socket = await ConnectAsync(cancellationToken);
        using var stream = new NetworkStream(socket, ownsSocket: false);
        await WriteAsync(stream, payload, cancellationToken);
    }

    public async Task<JsonDocument> RequestAsync(JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var socket = await ConnectAsync(cancellationToken);
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
