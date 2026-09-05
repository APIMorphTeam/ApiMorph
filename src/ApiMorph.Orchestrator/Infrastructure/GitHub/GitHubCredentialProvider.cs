using Microsoft.Extensions.Options;
using Octokit;

namespace ApiMorph.Orchestrator.Infrastructure.GitHub;

/// <summary>
/// Resolves GitHub credentials with App preferred, PAT fallback.
/// Private keys are loaded from a mounted file path or env PEM — never from the repository.
/// </summary>
public sealed class GitHubCredentialProvider(
    IOptions<GitHubOptions> options,
    ILogger<GitHubCredentialProvider> logger) : IGitHubCredentialProvider
{
    private readonly GitHubOptions _options = options.Value;
    private readonly object _gate = new();
    private GitHubAccessCredential? _cachedAppToken;

    public bool IsConfigured => AuthMode is GitHubAuthMode.App or GitHubAuthMode.Pat;

    public GitHubAuthMode AuthMode
    {
        get
        {
            if (IsAppConfigured())
            {
                return GitHubAuthMode.App;
            }

            return string.IsNullOrWhiteSpace(_options.Token) ? GitHubAuthMode.None : GitHubAuthMode.Pat;
        }
    }

    public async Task<GitHubAccessCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        return AuthMode switch
        {
            GitHubAuthMode.App => await GetInstallationTokenAsync(cancellationToken),
            GitHubAuthMode.Pat => new GitHubAccessCredential(_options.Token!.Trim(), GitHubAuthMode.Pat, null),
            _ => throw new InvalidOperationException(
                "GitHub is not configured. Set GitHub App credentials (preferred) or GitHub__Token (PAT fallback)."),
        };
    }

    private bool IsAppConfigured() =>
        !string.IsNullOrWhiteSpace(_options.AppId)
        && _options.ParsedInstallationId is > 0
        && TryResolvePrivateKeyPem(out _);

    private async Task<GitHubAccessCredential> GetInstallationTokenAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_cachedAppToken is { ExpiresAt: { } expires }
                && expires > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return _cachedAppToken;
            }
        }

        if (!TryResolvePrivateKeyPem(out var pem))
        {
            throw new InvalidOperationException(
                "GitHub App private key is missing. Mount a PEM via GitHub__AppPrivateKeyPath "
                + "(recommended) or set GitHub__AppPrivateKey from a secret store.");
        }

        var installationId = _options.ParsedInstallationId
            ?? throw new InvalidOperationException(
                "GitHub__InstallationId must be a number (e.g. 159257812) or an installations URL ending with that id.");

        cancellationToken.ThrowIfCancellationRequested();

        using var rsa = GitHubAppJwtFactory.LoadPrivateKey(pem);
        var jwt = GitHubAppJwtFactory.Create(_options.AppId!, rsa);

        var appClient = new GitHubClient(new ProductHeaderValue("ApiMorph"))
        {
            Credentials = new Credentials(jwt, AuthenticationType.Bearer),
        };

        var installationToken = await appClient.GitHubApps.CreateInstallationToken(installationId);
        var credential = new GitHubAccessCredential(
            installationToken.Token,
            GitHubAuthMode.App,
            installationToken.ExpiresAt);

        lock (_gate)
        {
            _cachedAppToken = credential;
        }

        logger.LogInformation(
            "Minted GitHub App installation token for installation {InstallationId}, expires {ExpiresAt:O}",
            installationId,
            installationToken.ExpiresAt);

        return credential;
    }

    private bool TryResolvePrivateKeyPem(out string pem)
    {
        pem = string.Empty;

        if (!string.IsNullOrWhiteSpace(_options.AppPrivateKeyPath))
        {
            var path = _options.AppPrivateKeyPath.Trim();
            if (!File.Exists(path))
            {
                logger.LogWarning("GitHub App private key file not found: {Path}", path);
                return false;
            }

            pem = File.ReadAllText(path);
            return pem.Contains("BEGIN", StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(_options.AppPrivateKey))
        {
            pem = GitHubAppJwtFactory.NormalizePem(_options.AppPrivateKey);
            return pem.Contains("BEGIN", StringComparison.Ordinal);
        }

        return false;
    }
}
