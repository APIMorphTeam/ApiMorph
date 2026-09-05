using ApiMorph.Orchestrator.Domain.Entities;
using ApiMorph.Orchestrator.Infrastructure.Config;
using ApiMorph.Orchestrator.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Application.Services;

public interface IRepoRegistry
{
    Task<IReadOnlyList<RegisteredRepo>> GetEnabledReposAsync(CancellationToken cancellationToken = default);

    Task<RegisteredRepo?> FindAsync(string owner, string repo, CancellationToken cancellationToken = default);

    Task UpsertFromConfAsync(CancellationToken cancellationToken = default);
}

public sealed record RegisteredRepo(
    string Owner,
    string Name,
    string DefaultBranch,
    IReadOnlyList<string> Providers,
    IReadOnlyList<string> WebhookBranches,
    string? ScheduleCron,
    bool CreatePullRequest,
    Guid? RepositoryId);

public sealed class RepoRegistry(
    ApiMorphDbContext dbContext,
    IOptions<AutomationOptions> automationOptions,
    ILogger<RepoRegistry> logger) : IRepoRegistry
{
    private readonly AutomationOptions _options = automationOptions.Value;

    public async Task UpsertFromConfAsync(CancellationToken cancellationToken = default)
    {
        foreach (var confRepo in _options.Repos.Where(r => r.Enabled))
        {
            var existing = await dbContext.Repositories
                .FirstOrDefaultAsync(
                    r => r.GitHubOwner == confRepo.Owner && r.GitHubRepo == confRepo.Name,
                    cancellationToken);

            if (existing is null)
            {
                var installation = await EnsureDefaultInstallationAsync(cancellationToken);
                dbContext.Repositories.Add(new Repository
                {
                    Id = Guid.NewGuid(),
                    InstallationId = installation.Id,
                    GitHubOwner = confRepo.Owner,
                    GitHubRepo = confRepo.Name,
                    DefaultBranch = confRepo.DefaultBranch,
                    Providers = confRepo.Providers,
                    Enabled = true,
                    WebhookBranches = confRepo.WebhookBranches,
                    ScheduleCron = confRepo.ScheduleCron,
                });
            }
            else
            {
                existing.DefaultBranch = confRepo.DefaultBranch;
                existing.Providers = confRepo.Providers;
                existing.Enabled = confRepo.Enabled;
                existing.WebhookBranches = confRepo.WebhookBranches;
                existing.ScheduleCron = confRepo.ScheduleCron;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Synced {Count} repos from config", _options.Repos.Count);
    }

    public async Task<IReadOnlyList<RegisteredRepo>> GetEnabledReposAsync(CancellationToken cancellationToken = default)
    {
        var fromDb = await dbContext.Repositories
            .Where(r => r.Enabled)
            .ToListAsync(cancellationToken);

        if (fromDb.Count > 0)
        {
            return fromDb.Select(Map).ToList();
        }

        return _options.Repos
            .Where(r => r.Enabled)
            .Select(r => new RegisteredRepo(
                r.Owner,
                r.Name,
                r.DefaultBranch,
                r.GetProviders(),
                r.GetWebhookBranches(),
                r.ScheduleCron,
                r.CreatePullRequest,
                null))
            .ToList();
    }

    public async Task<RegisteredRepo?> FindAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Repositories
            .FirstOrDefaultAsync(
                r => r.Enabled
                    && r.GitHubOwner.ToLower() == owner.ToLower()
                    && r.GitHubRepo.ToLower() == repo.ToLower(),
                cancellationToken);

        if (entity is not null)
        {
            return Map(entity);
        }

        var conf = _options.Repos.FirstOrDefault(r =>
            r.Enabled
            && r.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase)
            && r.Name.Equals(repo, StringComparison.OrdinalIgnoreCase));

        return conf is null
            ? null
            : new RegisteredRepo(
                conf.Owner,
                conf.Name,
                conf.DefaultBranch,
                conf.GetProviders(),
                conf.GetWebhookBranches(),
                conf.ScheduleCron,
                conf.CreatePullRequest,
                null);
    }

    private async Task<Installation> EnsureDefaultInstallationAsync(CancellationToken cancellationToken)
    {
        var installation = await dbContext.Installations.FirstOrDefaultAsync(cancellationToken);
        if (installation is not null)
        {
            return installation;
        }

        installation = new Installation
        {
            Id = Guid.NewGuid(),
            Name = "default",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Installations.Add(installation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return installation;
    }

    private static RegisteredRepo Map(Repository repository) =>
        new(
            repository.GitHubOwner,
            repository.GitHubRepo,
            repository.DefaultBranch,
            repository.Providers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            (repository.WebhookBranches ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            repository.ScheduleCron,
            true,
            repository.Id);
}
