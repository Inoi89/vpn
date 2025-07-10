using System;

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
    event Action<string>? LogReceived;
    Task ConnectAsync(string config);
    Task DisconnectAsync();
}
