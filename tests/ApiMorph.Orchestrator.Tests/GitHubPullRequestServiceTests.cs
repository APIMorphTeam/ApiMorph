using ApiMorph.Orchestrator.Infrastructure.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Tests;

public class GitHubPullRequestServiceTests
{
    [Fact]
    public void IsConfigured_ReturnsFalse_WhenNoCredentials()
    {
        var service = CreateService(new GitHubOptions());

        Assert.False(service.IsConfigured);
        Assert.Equal(GitHubAuthMode.None, service.AuthMode);
    }

    [Fact]
    public void IsConfigured_ReturnsTrue_WhenPatPresent()
    {
        var service = CreateService(new GitHubOptions { Token = "test-token" });

        Assert.True(service.IsConfigured);
        Assert.Equal(GitHubAuthMode.Pat, service.AuthMode);
    }

    [Fact]
    public void AuthMode_PrefersApp_WhenAppFullyConfigured()
    {
        var pemPath = WriteTempPem();
        try
        {
            var provider = new GitHubCredentialProvider(
                Options.Create(new GitHubOptions
                {
                    Token = "fallback-pat",
                    AppId = "123456",
                    InstallationId = "987654",
                    AppPrivateKeyPath = pemPath,
                }),
                NullLogger<GitHubCredentialProvider>.Instance);

            Assert.Equal(GitHubAuthMode.App, provider.AuthMode);
            Assert.True(provider.IsConfigured);
        }
        finally
        {
            File.Delete(pemPath);
        }
    }

    [Fact]
    public void GitHubOptions_HasDefaultCommitIdentity()
    {
        var options = new GitHubOptions();

        Assert.Equal("ApiMorph Bot", options.CommitAuthorName);
        Assert.Equal("apimorph-bot@users.noreply.github.com", options.CommitAuthorEmail);
    }

    [Fact]
    public void BuildAuthenticatedUrl_UsesXAccessTokenUsername()
    {
        var url = GitRepositoryService.BuildAuthenticatedUrl(
            new GitHubRepositoryRef("acme", "payments"),
            new GitHubAccessCredential("ghs_test", GitHubAuthMode.App, null));

        Assert.Equal("https://x-access-token:ghs_test@github.com/acme/payments.git", url);
    }

    private static GitHubPullRequestService CreateService(GitHubOptions options)
    {
        var provider = new GitHubCredentialProvider(
            Options.Create(options),
            NullLogger<GitHubCredentialProvider>.Instance);

        return new GitHubPullRequestService(
            provider,
            NullLogger<GitHubPullRequestService>.Instance);
    }

    private static string WriteTempPem()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var path = Path.Combine(Path.GetTempPath(), $"apimorph-test-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, rsa.ExportRSAPrivateKeyPem());
        return path;
    }
}
