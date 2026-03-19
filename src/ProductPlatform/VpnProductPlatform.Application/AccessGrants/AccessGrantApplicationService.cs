using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Contracts;
using VpnProductPlatform.Domain.Entities;
using VpnProductPlatform.Domain.Enums;

namespace VpnProductPlatform.Application.AccessGrants;

public sealed class AccessGrantApplicationService(
    IAccountRepository accountRepository,
    IDeviceRepository deviceRepository,
    ISubscriptionRepository subscriptionRepository,
    IAccessGrantRepository accessGrantRepository,
    IControlPlaneProvisioningClient controlPlaneProvisioningClient,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const string DefaultConfigFormat = "amnezia-vpn";

    public async Task<IReadOnlyList<AccessGrantResponse>> ListAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var grants = await accessGrantRepository.ListByAccountIdAsync(accountId, cancellationToken);
        return grants
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(Map)
            .ToList();
    }

    public async Task<IReadOnlyList<IssuableNodeResponse>> ListIssuableNodesAsync(CancellationToken cancellationToken)
    {
        var nodes = await controlPlaneProvisioningClient.ListNodesAsync(cancellationToken);
        return nodes
            .Where(x => string.Equals(x.Status, "Healthy", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new IssuableNodeResponse(x.NodeId, x.Name, x.Status, x.ActiveSessions, x.EnabledPeerCount))
            .ToArray();
    }

    public async Task<IssuedAccessGrantResponse> IssueAsync(Guid accountId, IssueAccessGrantRequest request, CancellationToken cancellationToken)
    {
        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException("Account was not found.");

        if (account.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException("Email verification is required before issuing VPN access.");
        }

        var subscription = await subscriptionRepository.GetActiveByAccountIdAsync(accountId, clock.UtcNow, cancellationToken)
            ?? throw new InvalidOperationException("No active subscription was found for this account.");

        var device = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new InvalidOperationException("Device was not found.");

        if (device.AccountId != accountId)
        {
            throw new InvalidOperationException("Device does not belong to the current account.");
        }

        if (device.Status != DeviceStatus.Active)
        {
            throw new InvalidOperationException("Device must be active before issuing VPN access.");
        }

        if (subscription.Plan.MaxDevices <= 0)
        {
            throw new InvalidOperationException("The current subscription does not allow device-bound VPN access.");
        }

        var existingGrant = await accessGrantRepository.GetActiveByDeviceIdAsync(accountId, request.DeviceId, cancellationToken);
        if (existingGrant is not null)
        {
            if (!existingGrant.NodeId.HasValue || !existingGrant.ControlPlaneAccessId.HasValue)
            {
                throw new InvalidOperationException("An active VPN access exists for this device, but its config cannot be restored.");
            }

            var restoredFormat = string.IsNullOrWhiteSpace(request.ConfigFormat)
                ? existingGrant.ConfigFormat
                : request.ConfigFormat.Trim();
            var restoredConfig = await controlPlaneProvisioningClient.GetAccessConfigAsync(
                existingGrant.NodeId.Value,
                existingGrant.ControlPlaneAccessId.Value,
                restoredFormat,
                cancellationToken);

            device.Touch(device.DeviceName, device.Platform, device.ClientVersion, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new IssuedAccessGrantResponse(
                existingGrant.Id,
                device.Id,
                device.DeviceName,
                existingGrant.NodeId.Value,
                existingGrant.ControlPlaneAccessId,
                existingGrant.PeerPublicKey,
                existingGrant.AllowedIps,
                restoredFormat,
                existingGrant.Status.ToString(),
                existingGrant.IssuedAtUtc,
                existingGrant.ExpiresAtUtc,
                existingGrant.RevokedAtUtc,
                restoredConfig.FileName,
                restoredConfig.Config);
        }

        var availableNodes = await ListIssuableNodesAsync(cancellationToken);
        if (!availableNodes.Any(x => x.NodeId == request.NodeId))
        {
            throw new InvalidOperationException("The requested VPN node is unavailable.");
        }

        var format = string.IsNullOrWhiteSpace(request.ConfigFormat) ? DefaultConfigFormat : request.ConfigFormat.Trim();
        var issueResult = await controlPlaneProvisioningClient.IssueAccessAsync(
            new ControlPlaneIssueAccessRequest(
                request.NodeId,
                device.DeviceName,
                account.Email,
                format,
                new ControlPlaneProductMetadata(
                    account.Id,
                    account.Email,
                    account.DisplayName,
                    device.Id,
                    device.DeviceName,
                    device.Platform,
                    device.Fingerprint,
                    device.ClientVersion)),
            cancellationToken);

        var accessGrant = AccessGrant.Create(
            Guid.NewGuid(),
            accountId,
            device.Id,
            issueResult.NodeId,
            issueResult.AccessId,
            issueResult.PublicKey,
            issueResult.AllowedIps,
            format,
            clock.UtcNow,
            expiresAtUtc: subscription.EndsAtUtc,
            clock.UtcNow);
        accessGrant.Activate(issueResult.NodeId, issueResult.AccessId, issueResult.PublicKey, issueResult.AllowedIps, clock.UtcNow);

        await accessGrantRepository.AddAsync(accessGrant, cancellationToken);
        device.Touch(device.DeviceName, device.Platform, device.ClientVersion, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new IssuedAccessGrantResponse(
            accessGrant.Id,
            device.Id,
            device.DeviceName,
            issueResult.NodeId,
            accessGrant.ControlPlaneAccessId,
            accessGrant.PeerPublicKey,
            accessGrant.AllowedIps,
            accessGrant.ConfigFormat,
            accessGrant.Status.ToString(),
            accessGrant.IssuedAtUtc,
            accessGrant.ExpiresAtUtc,
            accessGrant.RevokedAtUtc,
            issueResult.ClientConfigFileName,
            issueResult.ClientConfig);
    }

    private static AccessGrantResponse Map(AccessGrant grant)
    {
        return new AccessGrantResponse(
            grant.Id,
            grant.DeviceId,
            grant.Device.DeviceName,
            grant.NodeId,
            grant.ControlPlaneAccessId,
            grant.PeerPublicKey,
            grant.AllowedIps,
            grant.ConfigFormat,
            grant.Status.ToString(),
            grant.IssuedAtUtc,
            grant.ExpiresAtUtc,
            grant.RevokedAtUtc);
    }
}
