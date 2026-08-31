using ApiMorph.Orchestrator.Infrastructure.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Tests;

public class GitHubPullRequestServiceTests
{
    [Fact]
    public void IsConfigured_ReturnsFalse_WhenTokenMissing()
    {
        var service = new GitHubPullRequestService(
            Options.Create(new GitHubOptions()),
            NullLogger<GitHubPullRequestService>.Instance);

        Assert.False(service.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ReturnsTrue_WhenTokenPresent()
    {
        var service = new GitHubPullRequestService(
            Options.Create(new GitHubOptions { Token = "test-token" }),
            NullLogger<GitHubPullRequestService>.Instance);

        Assert.True(service.IsConfigured);
    }

    [Fact]
    public void GitHubOptions_HasDefaultCommitIdentity()
    {
        var options = new GitHubOptions();

        Assert.Equal("ApiMorph Bot", options.CommitAuthorName);
        Assert.Equal("apimorph-bot@users.noreply.github.com", options.CommitAuthorEmail);
    }
}
