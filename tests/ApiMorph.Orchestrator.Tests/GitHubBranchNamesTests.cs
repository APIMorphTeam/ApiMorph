using ApiMorph.Orchestrator.Infrastructure.GitHub;

namespace ApiMorph.Orchestrator.Tests;

public class GitHubBranchNamesTests
{
    [Fact]
    public void MigrationBranch_IsStablePerProvider()
    {
        var branch = GitHubBranchNames.MigrationBranch("apimorph", "stripe");

        Assert.Equal("apimorph/stripe-migration", branch);
    }

    [Fact]
    public void MigrationBranch_NormalizesProviderCase()
    {
        var branch = GitHubBranchNames.MigrationBranch("apimorph", "Stripe");

        Assert.Equal("apimorph/stripe-migration", branch);
    }
}
