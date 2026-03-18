using VpnClient.Core.Models;

namespace VpnClient.Core.Interfaces;

public interface IVpnRuntimeAdapter
{
    ConnectionState CurrentState { get; }

    event Action<ConnectionState>? StateChanged;

    Task<ConnectionState> ConnectAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default);

    Task<ConnectionState> DisconnectAsync(CancellationToken cancellationToken = default);

    Task<ConnectionState> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<ConnectionState> TryRestoreAsync(IReadOnlyList<ImportedServerProfile> profiles, CancellationToken cancellationToken = default)
    {
        return GetStatusAsync(cancellationToken);
    }
}
