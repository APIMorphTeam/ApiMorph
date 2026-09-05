using ApiMorph.Orchestrator.Application.Services;
using ApiMorph.Orchestrator.Infrastructure.Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Controllers;

[ApiController]
[Route("api/v1/repos")]
public class ReposController(
    IRepoRegistry repoRegistry,
    IOptions<AutomationOptions> automationOptions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        await repoRegistry.UpsertFromConfAsync(cancellationToken);
        var repos = await repoRegistry.GetEnabledReposAsync(cancellationToken);
        return Ok(repos.Select(r => new
        {
            owner = r.Owner,
            name = r.Name,
            defaultBranch = r.DefaultBranch,
            providers = r.Providers,
            webhookBranches = r.WebhookBranches,
            scheduleCron = r.ScheduleCron,
            createPullRequest = r.CreatePullRequest,
        }));
    }

    [HttpGet("~/api/v1/automation/status")]
    public IActionResult AutomationStatus()
    {
        var options = automationOptions.Value;
        return Ok(new
        {
            configPath = options.ConfigPath,
            manualEnabled = options.ManualEnabled,
            scheduleEnabled = options.ScheduleEnabled,
            scheduleCron = options.ScheduleCron,
            webhookEnabled = options.WebhookEnabled,
            webhookBranches = options.GetWebhookBranches(),
            webhookSecretConfigured = !string.IsNullOrWhiteSpace(options.ResolveWebhookSecret()),
            providerFeedEnabled = options.ProviderFeedEnabled,
            providerFeedProviders = options.GetProviderFeedProviders(),
            providerFeedInterval = options.ProviderFeedInterval.ToString(),
            registeredReposFromConf = options.Repos.Count,
        });
    }
}
