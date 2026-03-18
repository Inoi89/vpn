using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace VpnClient.Infrastructure.Updates;

internal static class UpdatePackageVerifier
{
    public static async Task VerifyAsync(string packagePath, string expectedSha256, string? expectedThumbprint, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);

        var actualHash = await ComputeSha256Async(packagePath, cancellationToken);
        if (!string.Equals(Normalize(actualHash), Normalize(expectedSha256), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The downloaded update package hash does not match the manifest SHA-256.");
        }

        if (string.IsNullOrWhiteSpace(expectedThumbprint))
        {
            return;
        }

        var certificate = TryGetCertificate(packagePath);
        if (certificate is null)
        {
            throw new InvalidOperationException("The downloaded update package is not Authenticode-signed, but the manifest requires a signer thumbprint.");
        }

        if (!string.Equals(
                Normalize(certificate.Thumbprint),
                Normalize(expectedThumbprint),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The downloaded update package signer thumbprint does not match the manifest.");
        }
    }

    private static async Task<string> ComputeSha256Async(string packagePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(packagePath);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes);
    }

    private static X509Certificate2? TryGetCertificate(string packagePath)
    {
        try
        {
            return new X509Certificate2(X509Certificate.CreateFromSignedFile(packagePath));
        }
        catch
        {
            return null;
        }
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
    }
}
