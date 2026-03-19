using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Contracts;

namespace VpnProductPlatform.Application.AccessGrants;

public sealed class AccessGrantApplicationService(IAccessGrantRepository accessGrantRepository)
{
    public async Task<IReadOnlyList<AccessGrantResponse>> ListAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var grants = await accessGrantRepository.ListByAccountIdAsync(accountId, cancellationToken);
        return grants
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new AccessGrantResponse(
                x.Id,
                x.DeviceId,
                x.Device.DeviceName,
                x.NodeId,
                x.PeerPublicKey,
                x.ConfigFormat,
                x.Status.ToString(),
                x.IssuedAtUtc,
                x.ExpiresAtUtc,
                x.RevokedAtUtc))
            .ToList();
    }
}
