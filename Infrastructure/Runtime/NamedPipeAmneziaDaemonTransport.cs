using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VpnClient.Infrastructure.Runtime;

public sealed class NamedPipeAmneziaDaemonTransport : IAmneziaDaemonTransport
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromMilliseconds(1500);
    private const string PipeName = "amneziavpn";

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = await ConnectAsync(ConnectTimeout, cancellationToken);
            return stream.IsConnected;
        }
        catch (IOException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task SendAsync(JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        await using var stream = await ConnectAsync(ConnectTimeout, cancellationToken);
        await WriteAsync(stream, payload, cancellationToken);
    }

    public async Task<JsonDocument> RequestAsync(JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        await using var stream = await ConnectAsync(ConnectTimeout, cancellationToken);
        await WriteAsync(stream, payload, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ResponseTimeout);

        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var line = await reader.ReadLineAsync().WaitAsync(timeoutCts.Token);
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new InvalidOperationException("Amnezia daemon returned an empty response.");
        }

        return JsonDocument.Parse(line);
    }

    private static async Task<NamedPipeClientStream> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var stream = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            await stream.ConnectAsync(timeoutCts.Token);
            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
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
