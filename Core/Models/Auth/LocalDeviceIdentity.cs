namespace VpnClient.Core.Models.Auth;

public sealed record LocalDeviceIdentity(
    string DeviceName,
    string Platform,
    string Fingerprint,
    string ClientVersion);
