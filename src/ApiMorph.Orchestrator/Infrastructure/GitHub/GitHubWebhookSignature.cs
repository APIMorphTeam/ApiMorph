using System.Security.Cryptography;
using System.Text;

namespace ApiMorph.Orchestrator.Infrastructure.GitHub;

public static class GitHubWebhookSignature
{
    public static bool IsValid(string? signatureHeader, string secret, ReadOnlySpan<byte> payload)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedHex = signatureHeader[prefix.Length..].Trim();
        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, payload);
        var expectedHex = Convert.ToHexString(hash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedHex.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(providedHex.ToLowerInvariant()));
    }
}
