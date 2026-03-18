using System.Text.Json;
using System.Text.Json.Nodes;

namespace VpnClient.Infrastructure.Runtime;

public interface IAmneziaDaemonTransport
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task SendAsync(JsonObject payload, CancellationToken cancellationToken = default);

    Task<JsonDocument> RequestAsync(JsonObject payload, CancellationToken cancellationToken = default);
}
