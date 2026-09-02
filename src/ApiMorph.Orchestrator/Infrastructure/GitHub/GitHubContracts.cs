namespace ApiMorph.Orchestrator.Infrastructure.GitHub;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public string? Token { get; set; }

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
