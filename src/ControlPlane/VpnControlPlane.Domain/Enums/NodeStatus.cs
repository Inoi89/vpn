namespace VpnControlPlane.Domain.Enums;

public enum NodeStatus
{
    Provisioning = 0,
    Healthy = 1,
    Unreachable = 2,
    Disabled = 3
}
