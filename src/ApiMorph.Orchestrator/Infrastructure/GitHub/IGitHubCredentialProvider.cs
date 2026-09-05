namespace ApiMorph.Orchestrator.Infrastructure.GitHub;

public enum GitHubAuthMode
{
    None = 0,
    Pat = 1,
    App = 2,
}

public sealed record GitHubAccessCredential(
    string Token,
    GitHubAuthMode Mode,
    DateTimeOffset? ExpiresAt);

public interface IGitHubCredentialProvider
{
    bool IsConfigured { get; }

    GitHubAuthMode AuthMode { get; }

    /// <summary>Returns a token suitable for Octokit and git HTTPS (PAT or installation token).</summary>
    Task<GitHubAccessCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
