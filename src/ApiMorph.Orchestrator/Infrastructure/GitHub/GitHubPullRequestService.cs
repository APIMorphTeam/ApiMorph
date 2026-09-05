using Octokit;

namespace ApiMorph.Orchestrator.Infrastructure.GitHub;

public sealed class GitHubPullRequestService(
    IGitHubCredentialProvider credentialProvider,
    ILogger<GitHubPullRequestService> logger) : IGitHubPullRequestService
{

    public bool IsConfigured => credentialProvider.IsConfigured;

    public GitHubAuthMode AuthMode => credentialProvider.AuthMode;

    public async Task<PullRequestResult?> FindOpenPullRequestAsync(
        GitHubRepositoryRef repository,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        var client = await CreateClientAsync(cancellationToken);
        var request = new PullRequestRequest
        {
            State = ItemStateFilter.Open,
            Head = $"{repository.Owner}:{branchName}",
        };

        var pullRequests = await client.PullRequest.GetAllForRepository(
            repository.Owner,
            repository.Repo,
            request);

        var existing = pullRequests.FirstOrDefault();
        if (existing is null)
        {
            return null;
        }

        return new PullRequestResult(existing.HtmlUrl, existing.Number, branchName);
    }

    public async Task<PullRequestResult> CreateDraftPullRequestAsync(
        GitHubRepositoryRef repository,
        string branchName,
        string title,
        string body,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindOpenPullRequestAsync(repository, branchName, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation(
                "Reusing existing open PR #{Number} for branch {Branch}",
                existing.Number,
                branchName);
            return existing;
        }

        var client = await CreateClientAsync(cancellationToken);
        var pullRequest = await client.PullRequest.Create(
            repository.Owner,
            repository.Repo,
            new NewPullRequest(title, branchName, repository.DefaultBranch)
            {
                Body = body,
                Draft = true,
            });

        return new PullRequestResult(pullRequest.HtmlUrl, pullRequest.Number, branchName);
    }

    private async Task<GitHubClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("GitHub is not configured (App or PAT).");
        }

        var credential = await credentialProvider.GetAccessTokenAsync(cancellationToken);
        return new GitHubClient(new ProductHeaderValue("ApiMorph"))
        {
            Credentials = new Credentials(credential.Token),
        };
    }
}
