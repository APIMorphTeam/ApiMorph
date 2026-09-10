using System.Security.Cryptography;
using System.Text;
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
            var fingerprint = await ComputeProviderFingerprintAsync(provider, cancellationToken);
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

    private async Task<string> ComputeProviderFingerprintAsync(
        string provider,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in EnumerateFingerprintCandidates(provider))
        {
            if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
            {
                continue;
            }

            await using var stream = File.OpenRead(candidate);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }

        logger.LogWarning(
            "No OpenAPI fingerprint file found for provider {Provider}; using placeholder",
            provider);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{provider}-placeholder")));
    }

    private IEnumerable<string?> EnumerateFingerprintCandidates(string provider)
    {
        var id = provider.Trim().ToLowerInvariant();

        if (id == "stripe" && !string.IsNullOrWhiteSpace(_options.ProviderFeedStripeOpenApiPath))
        {
            yield return _options.ProviderFeedStripeOpenApiPath;
        }

        // Operator custom packs + shipped packs mounted into the orchestrator
        yield return Path.Combine(_options.ConfigPath, "providers", id, "contract", "target.json");
        yield return Path.Combine("/providers", id, "contract", "target.json");
        yield return Path.Combine(_options.ConfigPath, "feeds", $"{id}_target.json");

        // Stage 8 Stripe feed path
        if (id == "stripe")
        {
            yield return Path.Combine(_options.ConfigPath, "feeds", "stripe_target.json");
            yield return "/app/fixtures/openapi/stripe_target.json";
            yield return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "providers", "stripe", "contract", "target.json"));
            yield return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "engine", "fixtures", "openapi", "stripe_target.json"));
        }
    }
}
