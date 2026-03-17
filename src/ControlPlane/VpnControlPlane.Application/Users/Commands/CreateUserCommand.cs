using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Domain.Entities;

namespace VpnControlPlane.Application.Users.Commands;

public sealed record CreateUserCommand(
    string ExternalId,
    string DisplayName,
    string? Email,
    bool IsEnabled) : ICommand<UserSummaryDto>;

public sealed class CreateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<CreateUserCommand, UserSummaryDto>
{
    public async Task<UserSummaryDto> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var existing = await userRepository.FindByExternalIdAsync(command.ExternalId, cancellationToken);
        if (existing is not null)
        {
            existing.UpdateProfile(command.DisplayName, command.Email, command.IsEnabled, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new UserSummaryDto(
                existing.Id,
                existing.ExternalId,
                existing.DisplayName,
                existing.Email,
                existing.IsEnabled,
                existing.PeerConfigs.Count);
        }

        var user = VpnUser.Create(
            Guid.NewGuid(),
            command.ExternalId,
            command.DisplayName,
            command.Email,
            command.IsEnabled,
            clock.UtcNow);

        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserSummaryDto(user.Id, user.ExternalId, user.DisplayName, user.Email, user.IsEnabled, 0);
    }
}
