using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApiMorph.Orchestrator.Infrastructure.GitHub;

/// <summary>
/// Creates short-lived RS256 JWTs for GitHub App authentication.
/// GitHub requires iat/exp within a 10-minute window.
/// </summary>
public static class GitHubAppJwtFactory
{
    public static string Create(string appId, RSA privateKey, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentException("GitHub App ID is required.", nameof(appId));
        }

        ArgumentNullException.ThrowIfNull(privateKey);

        var clock = now ?? DateTimeOffset.UtcNow;
        // Slightly backdate iat to absorb clock skew; max lifetime is 10 minutes.
        var issuedAt = clock.AddSeconds(-60);
        var expiresAt = clock.AddMinutes(9);

        var headerJson = """{"alg":"RS256","typ":"JWT"}""";
        var payloadJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
            ["iss"] = appId.Trim(),
        });

        var header = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{payload}");
        var signature = privateKey.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{header}.{payload}.{Base64UrlEncode(signature)}";
    }

    public static RSA LoadPrivateKey(string pemOrPath)
    {
        if (string.IsNullOrWhiteSpace(pemOrPath))
        {
            throw new ArgumentException("Private key PEM or path is required.", nameof(pemOrPath));
        }

        var pem = pemOrPath.Contains("BEGIN", StringComparison.Ordinal)
            ? NormalizePem(pemOrPath)
            : File.ReadAllText(pemOrPath);

        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }

    internal static string NormalizePem(string value) =>
        value.Replace("\\n", "\n", StringComparison.Ordinal).Trim();

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
