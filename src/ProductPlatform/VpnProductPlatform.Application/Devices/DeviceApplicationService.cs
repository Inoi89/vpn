using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Contracts;
using VpnProductPlatform.Domain.Entities;
using VpnProductPlatform.Domain.Enums;

namespace VpnProductPlatform.Application.Devices;

public sealed class DeviceApplicationService(
    IAccountRepository accountRepository,
    IDeviceRepository deviceRepository,
    ISubscriptionRepository subscriptionRepository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<IReadOnlyList<DeviceResponse>> ListAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var devices = await deviceRepository.ListByAccountIdAsync(accountId, cancellationToken);
        return devices
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Select(Map)
            .ToArray();
    }

    public async Task<DeviceResponse> RegisterAsync(Guid accountId, RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        _ = await accountRepository.GetByIdAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException("Account was not found.");

        var subscription = await subscriptionRepository.GetActiveByAccountIdAsync(accountId, clock.UtcNow, cancellationToken)
            ?? throw new InvalidOperationException("No active subscription was found for this account.");

        var existing = await deviceRepository.FindByFingerprintAsync(accountId, request.Fingerprint, cancellationToken);
        if (existing is not null)
        {
            existing.Touch(request.DeviceName, request.Platform, request.ClientVersion, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Map(existing);
        }

        var activeDeviceCount = await deviceRepository.CountActiveByAccountIdAsync(accountId, cancellationToken);
        if (activeDeviceCount >= subscription.Plan.MaxDevices)
        {
            throw new InvalidOperationException($"Device limit reached for the current subscription plan ({subscription.Plan.MaxDevices}).");
        }

        var device = Device.Create(
            Guid.NewGuid(),
            accountId,
            request.DeviceName,
            request.Platform,
            request.Fingerprint,
            request.ClientVersion,
            clock.UtcNow);

        await deviceRepository.AddAsync(device, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(device);
    }

    public async Task RevokeAsync(Guid accountId, Guid deviceId, CancellationToken cancellationToken)
    {
        var device = await deviceRepository.GetByIdAsync(deviceId, cancellationToken)
            ?? throw new InvalidOperationException("Device was not found.");

        if (device.AccountId != accountId)
        {
            throw new InvalidOperationException("Device does not belong to the current account.");
        }

        if (device.Status != DeviceStatus.Active)
        {
            return;
        }

        device.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static DeviceResponse Map(Device device)
    {
        return new DeviceResponse(
            device.Id,
            device.DeviceName,
            device.Platform,
            device.ClientVersion,
            device.Fingerprint,
            device.Status.ToString(),
            device.LastSeenAtUtc);
    }
}
