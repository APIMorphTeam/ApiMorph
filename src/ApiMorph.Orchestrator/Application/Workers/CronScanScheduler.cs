using ApiMorph.Orchestrator.Application.Services;
using ApiMorph.Orchestrator.Domain.Enums;
using ApiMorph.Orchestrator.Infrastructure.Config;
using Cronos;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Application.Workers;

public sealed class CronScanScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<AutomationOptions> automationOptions,
    ILogger<CronScanScheduler> logger) : BackgroundService
{
    private readonly AutomationOptions _options = automationOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ScheduleEnabled)
        {
            logger.LogInformation("Cron scheduler disabled (schedule.enabled=false)");
            return;
        }

        if (!CronExpression.TryParse(_options.ScheduleCron, out var expression))
        {
            logger.LogError("Invalid schedule.cron expression: {Cron}", _options.ScheduleCron);
            return;
        }

        var timezone = ResolveTimezone(_options.ScheduleTimezone);
        logger.LogInformation(
            "Cron scheduler enabled: {Cron} ({Timezone})",
            _options.ScheduleCron,
            _options.ScheduleTimezone);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var next = expression.GetNextOccurrence(now.UtcDateTime, timezone);
            if (next is null)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                continue;
            }

            var delay = next.Value - now.UtcDateTime;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                await EnqueueScheduledScansAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cron enqueue failed");
            }
        }
    }

    private async Task EnqueueScheduledScansAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IRepoRegistry>();
        var queue = scope.ServiceProvider.GetRequiredService<IAutomationJobQueue>();

        await registry.UpsertFromConfAsync(cancellationToken);
        var repos = await registry.GetEnabledReposAsync(cancellationToken);

        foreach (var repo in repos)
        {
            await queue.EnqueueAsync(
                repo.Owner,
                repo.Name,
                AutomationTrigger.Cron,
                branch: repo.DefaultBranch,
                createPullRequest: _options.ScheduleCreatePullRequest && repo.CreatePullRequest,
                provider: repo.Providers.FirstOrDefault() ?? _options.ScanProvider,
                cancellationToken: cancellationToken);
        }

        logger.LogInformation("Cron enqueued scans for {Count} repositories", repos.Count);
    }

    private static TimeZoneInfo ResolveTimezone(string timezone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
