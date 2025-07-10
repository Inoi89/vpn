namespace VpnClient.Core.Interfaces;

public enum VpnState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting
}

public interface IVpnService
{
    VpnState State { get; }
    Task ConnectAsync(string config);
    Task DisconnectAsync();
}
