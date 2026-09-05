using System.Security.Cryptography;
using ApiMorph.Orchestrator.Application.Services;
using ApiMorph.Orchestrator.Domain.Entities;
using ApiMorph.Orchestrator.Domain.Enums;
using ApiMorph.Orchestrator.Infrastructure.Config;
using ApiMorph.Orchestrator.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Application.Workers;

public sealed class ProviderFeedPoller(
    IServiceScopeFactory scopeFactory,
    IOptions<AutomationOptions> automationOptions,
    ILogger<ProviderFeedPoller> logger) : BackgroundService
{
    private readonly AutomationOptions _options = automationOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ProviderFeedEnabled)
        {
            logger.LogInformation("Provider feed disabled (provider_feed.enabled=false)");
            return;
        }

        logger.LogInformation(
            "Provider feed enabled for {Providers}, interval {Interval}",
            _options.ProviderFeedProviders,
            _options.ProviderFeedInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Provider feed poll failed");
            }

            await Task.Delay(_options.ProviderFeedInterval, stoppingToken);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiMorphDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<IRepoRegistry>();
        var queue = scope.ServiceProvider.GetRequiredService<IAutomationJobQueue>();

        foreach (var provider in _options.GetProviderFeedProviders())
        {
            if (!provider.Equals("stripe", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Provider feed for {Provider} is not implemented yet", provider);
                continue;
            }

            var fingerprint = await ComputeStripeFingerprintAsync(cancellationToken);
            var state = await db.ProviderFeedStates
                .FirstOrDefaultAsync(s => s.Provider == provider, cancellationToken);

            if (state is null)
            {
                state = new ProviderFeedState
                {
                    Id = Guid.NewGuid(),
                    Provider = provider,
                    Fingerprint = fingerprint,
                    LastCheckedAt = DateTimeOffset.UtcNow,
                    LastChangedAt = DateTimeOffset.UtcNow,
                };
                db.ProviderFeedStates.Add(state);
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Provider feed initialized fingerprint for {Provider}", provider);
                continue;
            }

            state.LastCheckedAt = DateTimeOffset.UtcNow;
            if (string.Equals(state.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            logger.LogInformation("Provider feed change detected for {Provider}", provider);
            state.Fingerprint = fingerprint;
            state.LastChangedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            await registry.UpsertFromConfAsync(cancellationToken);
            var repos = (await registry.GetEnabledReposAsync(cancellationToken))
                .Where(r => r.Providers.Any(p => p.Equals(provider, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var repo in repos)
            {
                await queue.EnqueueAsync(
                    repo.Owner,
                    repo.Name,
                    AutomationTrigger.ProviderFeed,
                    branch: repo.DefaultBranch,
                    createPullRequest: _options.ProviderFeedCreatePullRequest && repo.CreatePullRequest,
                    provider: provider,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<string> ComputeStripeFingerprintAsync(CancellationToken cancellationToken)
    {
        var candidates = new[]
        {
            _options.ProviderFeedStripeOpenApiPath,
            Path.Combine(_options.ConfigPath, "feeds", "stripe_target.json"),
            "/app/fixtures/openapi/stripe_target.json",
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "engine", "fixtures", "openapi", "stripe_target.json")),
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
            {
                continue;
            }

            await using var stream = File.OpenRead(candidate);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }

        // Stable placeholder when fixture is unavailable — still allows enablement without crash.
        return Convert.ToHexString(SHA256.HashData("stripe-placeholder"u8.ToArray()));
    }
}
