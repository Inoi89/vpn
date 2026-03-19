using System.Security.Claims;

namespace VpnProductPlatform.Api.Infrastructure;

internal static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredAccountId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        return Guid.TryParse(value, out var accountId)
            ? accountId
            : throw new InvalidOperationException("Authenticated account id claim is missing.");
    }
}
