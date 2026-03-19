using Microsoft.AspNetCore.Identity;
using VpnProductPlatform.Application.Abstractions;

namespace VpnProductPlatform.Infrastructure.Security;

internal sealed class PasswordHashService : IPasswordHashService
{
    private readonly PasswordHasher<string> _hasher = new();

    public string HashPassword(string subject, string password)
    {
        return _hasher.HashPassword(subject, password);
    }

    public bool VerifyHashedPassword(string subject, string passwordHash, string password)
    {
        return _hasher.VerifyHashedPassword(subject, passwordHash, password) is not PasswordVerificationResult.Failed;
    }
}
