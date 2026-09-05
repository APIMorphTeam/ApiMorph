namespace ApiMorph.Orchestrator.Infrastructure.GitHub;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>Legacy / fallback fine-scoped PAT. Prefer GitHub App credentials in production.</summary>
    public string? Token { get; set; }

    /// <summary>GitHub App ID (numeric string from App settings).</summary>
    public string? AppId { get; set; }

    /// <summary>
    /// Absolute path to the App private key PEM file (preferred for Docker / K8s secret mounts).
    /// End users configure this path — they do not need <c>dotnet user-secrets</c>.
    /// </summary>
    public string? AppPrivateKeyPath { get; set; }

    /// <summary>
    /// Optional inline PEM content from a secret manager / env var.
    /// Prefer <see cref="AppPrivateKeyPath"/> so the key is not present in process environment listings.
    /// </summary>
    public string? AppPrivateKey { get; set; }

    /// <summary>
    /// GitHub App installation id (numeric) or full installations URL
    /// (e.g. https://github.com/organizations/ORG/settings/installations/123).
    /// Bound as string so pasted URLs do not crash configuration binding.
    /// </summary>
    public string? InstallationId { get; set; }

    /// <summary>Parsed numeric installation id, or null when missing/invalid.</summary>
    public long? ParsedInstallationId => GitHubInstallationIdParser.Parse(InstallationId);

    /// <summary>Webhook HMAC secret (used by Stage 8). Stored here for configuration completeness.</summary>
    public string? WebhookSecret { get; set; }

    public bool AutoMerge { get; set; }

    public bool AutoCreatePullRequest { get; set; }

    public string BranchPrefix { get; set; } = "apimorph";

    public string WorkspacePath { get; set; } = "/workspace";

    /// <summary>Git commit author name for ApiMorph-generated commits.</summary>
    public string CommitAuthorName { get; set; } = "ApiMorph Bot";

    /// <summary>Git commit author email for ApiMorph-generated commits.</summary>
    public string CommitAuthorEmail { get; set; } = "apimorph-bot@users.noreply.github.com";
}

public sealed record GitHubRepositoryRef(string Owner, string Repo, string DefaultBranch = "main");

public sealed record PullRequestResult(string Url, int Number, string BranchName);

public sealed record GitFileChange(string RelativePath, string Content);

public sealed record GitReportFile(string RelativePath, string Content);

public interface IGitRepositoryService
{
    Task<string> CloneOrUpdateAsync(GitHubRepositoryRef repository, CancellationToken cancellationToken = default);

    Task CommitReportAsync(
        string repositoryPath,
        string branchName,
        string relativeReportPath,
        string reportContent,
        CancellationToken cancellationToken = default);

    Task CommitReportsAsync(
        string repositoryPath,
        string branchName,
        IReadOnlyList<GitReportFile> reports,
        CancellationToken cancellationToken = default);

    Task CommitMigrationAsync(
        string repositoryPath,
        string branchName,
        IReadOnlyList<GitFileChange> files,
        string commitMessage,
        CancellationToken cancellationToken = default);
}

public interface IGitHubPullRequestService
{
    bool IsConfigured { get; }

    GitHubAuthMode AuthMode { get; }

    Task<PullRequestResult?> FindOpenPullRequestAsync(
        GitHubRepositoryRef repository,
        string branchName,
        CancellationToken cancellationToken = default);

    Task<PullRequestResult> CreateDraftPullRequestAsync(
        GitHubRepositoryRef repository,
        string branchName,
        string title,
        string body,
        CancellationToken cancellationToken = default);
}
