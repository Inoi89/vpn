namespace VpnClient.Core.Interfaces;

public interface IWireGuardDumpReader
{
    Task<string?> ReadDumpAsync(string interfaceName, CancellationToken cancellationToken = default);
}
