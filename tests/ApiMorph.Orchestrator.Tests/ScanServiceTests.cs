using ApiMorph.Orchestrator.Application.Contracts;
using ApiMorph.Orchestrator.Application.Services;
using ApiMorph.Orchestrator.Domain.Entities;
using ApiMorph.Orchestrator.Domain.Enums;
using ApiMorph.Orchestrator.Infrastructure.Data;
using ApiMorph.Orchestrator.Infrastructure.Engine;
using ApiMorph.Orchestrator.Infrastructure.GitHub;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Tests;

public class ScanServiceTests
{
    [Fact]
    public async Task CreateAndRunAsync_DoesNotCreatePullRequest_WhenNotRequested()
    {
        await using var dbContext = CreateDbContext();
        var scanService = CreateScanService(dbContext, new FakeGitHubPullRequestService(), new FakeGitRepositoryService());

        var tempRepo = CreateTempRepository();
        var result = await scanService.CreateAndRunAsync(new CreateScanRequest
        {
            RepositoryPath = tempRepo,
        });

        Assert.Equal("Completed", result.Status);
        Assert.Null(result.PullRequestUrl);
    }

    [Fact]
    public async Task CreateAndRunAsync_SkipsPullRequest_WhenGitHubNotConfigured()
    {
        await using var dbContext = CreateDbContext();
        var scanService = CreateScanService(
            dbContext,
            new FakeGitHubPullRequestService { IsConfigured = false },
            new FakeGitRepositoryService());

        var result = await scanService.CreateAndRunAsync(new CreateScanRequest
        {
            RepositoryPath = CreateTempRepository(),
            GitHubOwner = "acme",
            GitHubRepo = "payments",
            CreatePullRequest = true,
        });

        Assert.Equal("Completed", result.Status);
        Assert.Null(result.PullRequestUrl);
    }

    [Fact]
    public async Task CreateAndRunAsync_UsesStableMigrationBranch_ForPullRequests()
    {
        await using var dbContext = CreateDbContext();
        var gitRepo = new FakeGitRepositoryService();
        var scanService = CreateScanService(
            dbContext,
            new FakeGitHubPullRequestService(),
            gitRepo);

        var request = new CreateScanRequest
        {
            GitHubOwner = "APIMorphTeam",
            GitHubRepo = "ApiMorph-test",
            Provider = "stripe",
            CreatePullRequest = true,
        };

        await scanService.CreateAndRunAsync(request);
        await scanService.CreateAndRunAsync(request);

        Assert.Equal(2, gitRepo.BranchNames.Count);
        Assert.Equal("apimorph/stripe-migration", gitRepo.BranchNames[0]);
        Assert.Equal(gitRepo.BranchNames[0], gitRepo.BranchNames[1]);
    }

  private static ScanService CreateScanService(
        ApiMorphDbContext dbContext,
        IGitHubPullRequestService pullRequestService,
        IGitRepositoryService gitRepositoryService)
    {
        return new ScanService(
            dbContext,
            new FakeEngineClient(),
            new ScanReportGenerator(),
            gitRepositoryService,
            pullRequestService,
            Options.Create(new GitHubOptions { Token = "test-token", BranchPrefix = "apimorph" }),
            Options.Create(new PatchOptions { Enabled = true }),
            Options.Create(new LlmOptions { Enabled = false }),
            NullLogger<ScanService>.Instance);
    }

    private static ApiMorphDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApiMorphDbContext>()
            .UseSqlite($"Data Source=:memory:")
            .Options;

        var context = new ApiMorphDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static string CreateTempRepository()
    {
        var path = Path.Combine(Path.GetTempPath(), $"apimorph-repo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        var file = Path.Combine(path, "PaymentService.cs");
        File.WriteAllText(
            file,
            """
            StripeConfiguration.ApiVersion = "2019-12-03";
            new ChargeCreateOptions { Source = "tok" };
            var refundService = new RefundService();
            """);
        return path;
    }

    private sealed class FakeGitHubPullRequestService : IGitHubPullRequestService
    {
        public bool IsConfigured { get; set; } = true;

        public Task<PullRequestResult?> FindOpenPullRequestAsync(
            GitHubRepositoryRef repository,
            string branchName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PullRequestResult?>(null);

        public Task<PullRequestResult> CreateDraftPullRequestAsync(
            GitHubRepositoryRef repository,
            string branchName,
            string title,
            string body,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PullRequestResult("https://github.com/example/pull/1", 1, branchName));
    }

    private sealed class FakeGitRepositoryService : IGitRepositoryService
    {
        public Task<string> CloneOrUpdateAsync(GitHubRepositoryRef repository, CancellationToken cancellationToken = default)
        {
            var path = Path.Combine(Path.GetTempPath(), "apimorph-clone", repository.Owner, repository.Repo);
            Directory.CreateDirectory(path);
            return Task.FromResult(path);
        }

        public Task CommitReportAsync(
            string repositoryPath,
            string branchName,
            string relativeReportPath,
            string reportContent,
            CancellationToken cancellationToken = default)
        {
            BranchNames.Add(branchName);
            return Task.CompletedTask;
        }

        public Task CommitReportsAsync(
            string repositoryPath,
            string branchName,
            IReadOnlyList<GitReportFile> reports,
            CancellationToken cancellationToken = default)
        {
            BranchNames.Add(branchName);
            return Task.CompletedTask;
        }

        public Task CommitMigrationAsync(
            string repositoryPath,
            string branchName,
            IReadOnlyList<GitFileChange> files,
            string commitMessage,
            CancellationToken cancellationToken = default)
        {
            BranchNames.Add(branchName);
            return Task.CompletedTask;
        }

        public List<string> BranchNames { get; } = [];
    }
}
