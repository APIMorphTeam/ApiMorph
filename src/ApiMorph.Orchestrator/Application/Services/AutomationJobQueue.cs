using ApiMorph.Orchestrator.Domain.Entities;
using ApiMorph.Orchestrator.Domain.Enums;
using ApiMorph.Orchestrator.Infrastructure.Config;
using ApiMorph.Orchestrator.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Application.Services;

public interface IAutomationJobQueue
{
    Task<AutomationJob?> EnqueueAsync(
        string owner,
        string repo,
        AutomationTrigger trigger,
        string? branch = null,
        string? commitSha = null,
        bool? createPullRequest = null,
        string? provider = null,
        CancellationToken cancellationToken = default);

    Task<AutomationJob?> DequeueNextAsync(CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(Guid jobId, Guid scanJobId, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(Guid jobId, string error, CancellationToken cancellationToken = default);
}

public sealed class AutomationJobQueue(
    ApiMorphDbContext dbContext,
    IOptions<AutomationOptions> automationOptions) : IAutomationJobQueue
{
    private readonly AutomationOptions _options = automationOptions.Value;

    public async Task<AutomationJob?> EnqueueAsync(
        string owner,
        string repo,
        AutomationTrigger trigger,
        string? branch = null,
        string? commitSha = null,
        bool? createPullRequest = null,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        var dedupeKey = BuildDedupeKey(owner, repo, trigger, commitSha, branch);
        if (!string.IsNullOrWhiteSpace(dedupeKey))
        {
            var existing = await dbContext.AutomationJobs
                .Where(j => j.DedupeKey == dedupeKey
                    && (j.Status == AutomationJobStatus.Pending || j.Status == AutomationJobStatus.Running))
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                return existing;
            }
        }

        var job = new AutomationJob
        {
            Id = Guid.NewGuid(),
            GitHubOwner = owner,
            GitHubRepo = repo,
            Provider = provider ?? _options.ScanProvider,
            Language = _options.ScanLanguage,
            CreatePullRequest = createPullRequest ?? _options.ScanCreatePullRequest,
            Trigger = trigger,
            Status = AutomationJobStatus.Pending,
            Branch = branch,
            CommitSha = commitSha,
            DedupeKey = dedupeKey,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.AutomationJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<AutomationJob?> DequeueNextAsync(CancellationToken cancellationToken = default)
    {
        // SQLite cannot ORDER BY DateTimeOffset in SQL; pending queue is small so order client-side.
        var pending = await dbContext.AutomationJobs
            .Where(j => j.Status == AutomationJobStatus.Pending)
            .ToListAsync(cancellationToken);

        var job = pending.OrderBy(j => j.CreatedAt).FirstOrDefault();

        if (job is null)
        {
            return null;
        }

        job.Status = AutomationJobStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task MarkCompletedAsync(Guid jobId, Guid scanJobId, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.AutomationJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.Status = AutomationJobStatus.Completed;
        job.ScanJobId = scanJobId;
        job.CompletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid jobId, string error, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.AutomationJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.Status = AutomationJobStatus.Failed;
        job.Error = error.Length > 4000 ? error[..4000] : error;
        job.CompletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? BuildDedupeKey(
        string owner,
        string repo,
        AutomationTrigger trigger,
        string? commitSha,
        string? branch)
    {
        if (!string.IsNullOrWhiteSpace(commitSha))
        {
            return $"{owner}/{repo}:{trigger}:{commitSha}".ToLowerInvariant();
        }

        if (trigger is AutomationTrigger.Cron or AutomationTrigger.ProviderFeed)
        {
            // Collapse duplicate pending cron/feed jobs for the same repo.
            return $"{owner}/{repo}:{trigger}:pending".ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(branch))
        {
            return $"{owner}/{repo}:{trigger}:{branch}:pending".ToLowerInvariant();
        }

        return null;
    }
}
