using ApiMorph.Orchestrator.Application.Contracts;
using ApiMorph.Orchestrator.Application.Services;
using ApiMorph.Orchestrator.Infrastructure.Config;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Application.Workers;

public sealed class AutomationJobWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AutomationOptions> automationOptions,
    ILogger<AutomationJobWorker> logger) : BackgroundService
{
    private readonly AutomationOptions _options = automationOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Automation job worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<IAutomationJobQueue>();
                var scanService = scope.ServiceProvider.GetRequiredService<IScanService>();

                var job = await queue.DequeueNextAsync(stoppingToken);
                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                logger.LogInformation(
                    "Processing automation job {JobId} ({Trigger}) for {Owner}/{Repo}",
                    job.Id,
                    job.Trigger,
                    job.GitHubOwner,
                    job.GitHubRepo);

                try
                {
                    var result = await scanService.CreateAndRunAsync(
                        new CreateScanRequest
                        {
                            GitHubOwner = job.GitHubOwner,
                            GitHubRepo = job.GitHubRepo,
                            Provider = job.Provider,
                            Language = job.Language,
                            CreatePullRequest = job.CreatePullRequest,
                            DetectOnly = _options.ScanDetectOnly,
                            LlmEnabled = _options.ScanLlmEnabled,
                        },
                        stoppingToken);

                    if (string.Equals(result.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                    {
                        await queue.MarkFailedAsync(job.Id, result.Error ?? "Scan failed", stoppingToken);
                    }
                    else
                    {
                        await queue.MarkCompletedAsync(job.Id, result.Id, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Automation job {JobId} failed", job.Id);
                    await queue.MarkFailedAsync(job.Id, ex.Message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Automation worker loop error");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
