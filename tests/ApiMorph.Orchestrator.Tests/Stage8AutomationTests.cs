using System.Security.Cryptography;
using System.Text;
using ApiMorph.Orchestrator.Application.Services;
using ApiMorph.Orchestrator.Domain.Enums;
using ApiMorph.Orchestrator.Infrastructure.Config;
using ApiMorph.Orchestrator.Infrastructure.Data;
using ApiMorph.Orchestrator.Infrastructure.GitHub;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Tests;

public class Stage8AutomationTests
{
    [Fact]
    public async Task AutomationJobQueue_DequeueOrdersByCreatedAt_OnSqlite()
    {
        var options = new DbContextOptionsBuilder<ApiMorphDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new ApiMorphDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var queue = new AutomationJobQueue(db, Options.Create(new AutomationOptions()));
        var older = await queue.EnqueueAsync("o", "r", AutomationTrigger.Webhook, branch: "main", commitSha: "aaa");
        older!.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var newer = await queue.EnqueueAsync("o", "r", AutomationTrigger.Webhook, branch: "main", commitSha: "bbb");
        Assert.NotNull(newer);

        var dequeued = await queue.DequeueNextAsync();
        Assert.NotNull(dequeued);
        Assert.Equal(older.Id, dequeued.Id);
    }

    [Fact]
    public void ConfFileParser_IgnoresCommentsAndReadsKeys()
    {
        var values = ConfFileParser.ParseLines(
        [
            "# comment",
            "webhook.enabled = true",
            "webhook.branches = main,release/*  # trailing comment",
            "",
            "schedule.cron = 0 2 * * *",
        ]);

        Assert.True(ConfFileParser.GetBool(values, "webhook.enabled"));
        Assert.Equal("main,release/*", ConfFileParser.Get(values, "webhook.branches"));
        Assert.Equal("0 2 * * *", ConfFileParser.Get(values, "schedule.cron"));
    }

    [Theory]
    [InlineData("main", "main", true)]
    [InlineData("develop", "main", false)]
    [InlineData("release/1.0", "release/*", true)]
    [InlineData("feature/x", "release/*", false)]
    public void BranchPatternMatcher_MatchesGlobs(string branch, string pattern, bool expected)
    {
        Assert.Equal(expected, BranchPatternMatcher.MatchesSingle(branch, pattern));
    }

    [Fact]
    public void BranchFromRef_StripsHeadsPrefix()
    {
        Assert.Equal("main", BranchPatternMatcher.BranchFromRef("refs/heads/main"));
    }

    [Fact]
    public void GitHubWebhookSignature_ValidatesSha256()
    {
        const string secret = "test-secret";
        var payload = Encoding.UTF8.GetBytes("""{"ref":"refs/heads/main"}""");
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);
        var header = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        Assert.True(GitHubWebhookSignature.IsValid(header, secret, payload));
        Assert.False(GitHubWebhookSignature.IsValid("sha256=deadbeef", secret, payload));
    }

    [Fact]
    public void ConfFileParser_ParsesIntervalSuffixes()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["provider_feed.interval"] = "6h",
        };

        Assert.Equal(TimeSpan.FromHours(6), ConfFileParser.GetTimeSpan(values, "provider_feed.interval", TimeSpan.FromHours(1)));
    }
}
