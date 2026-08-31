using ApiMorph.Orchestrator.Application.Contracts;
using ApiMorph.Orchestrator.Application.Services;
using ApiMorph.Orchestrator.Domain.Entities;
using ApiMorph.Orchestrator.Domain.Enums;
using ApiMorph.Orchestrator.Infrastructure.Data;
using ApiMorph.Orchestrator.Infrastructure.Engine;
using ApiMorph.Orchestrator.Infrastructure.GitHub;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Application.Services;

public interface IScanService
{
    Task<ScanJobResponse> CreateAndRunAsync(CreateScanRequest request, CancellationToken cancellationToken = default);

    Task<ScanJobResponse?> GetAsync(Guid scanJobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FindingSummary>?> GetFindingsAsync(Guid scanJobId, CancellationToken cancellationToken = default);

    Task<ScanReportResponse?> GetReportAsync(Guid scanJobId, CancellationToken cancellationToken = default);
}

public sealed class ScanService(
    ApiMorphDbContext dbContext,
    IEngineClient engineClient,
    IScanReportGenerator reportGenerator,
    IGitRepositoryService gitRepositoryService,
    IGitHubPullRequestService gitHubPullRequestService,
    IOptions<GitHubOptions> gitHubOptions,
    ILogger<ScanService> logger) : IScanService
{
    private readonly GitHubOptions _gitHubOptions = gitHubOptions.Value;

    public async Task<ScanJobResponse> CreateAndRunAsync(CreateScanRequest request, CancellationToken cancellationToken = default)
    {
        var (repositoryPath, repositoryRef) = await ResolveRepositoryAsync(request, cancellationToken);

        var scanJob = new ScanJob
        {
            Id = Guid.NewGuid(),
            RepositoryId = request.RepositoryId,
            RepositoryPath = repositoryPath,
            Status = JobStatus.Running,
            TriggeredAt = DateTimeOffset.UtcNow,
        };

        dbContext.ScanJobs.Add(scanJob);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var analyzeResponse = await engineClient.AnalyzeAsync(
                new AnalyzeRequestDto
                {
                    ContractVersion = "1",
                    Provider = request.Provider,
                    RepositoryPath = repositoryPath,
                    Language = request.Language,
                    Options = new AnalyzeOptionsDto
                    {
                        DetectOnly = true,
                        LlmEnabled = false,
                    },
                },
                cancellationToken);

            var findings = analyzeResponse.Findings.Select(dto => new Finding
            {
                Id = Guid.NewGuid(),
                ScanJobId = scanJob.Id,
                RuleId = dto.RuleId,
                FilePath = dto.FilePath,
                Line = dto.Line,
                Message = dto.Message,
                Confidence = ParseConfidence(dto.Confidence),
                Evidence = dto.Evidence,
            }).ToList();

            dbContext.Findings.AddRange(findings);
            scanJob.Status = JobStatus.Completed;
            scanJob.CompletedAt = DateTimeOffset.UtcNow;

            if (ShouldCreatePullRequest(request, repositoryRef))
            {
                await CreateDraftPullRequestAsync(
                    scanJob,
                    findings,
                    repositoryRef!,
                    request.Provider,
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ToResponse(scanJob, findings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scan job {ScanJobId} failed", scanJob.Id);
            scanJob.Status = JobStatus.Failed;
            scanJob.CompletedAt = DateTimeOffset.UtcNow;
            scanJob.Error = ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToResponse(scanJob, []);
        }
    }

    public async Task<ScanJobResponse?> GetAsync(Guid scanJobId, CancellationToken cancellationToken = default)
    {
        var scanJob = await dbContext.ScanJobs
            .Include(j => j.Findings)
            .FirstOrDefaultAsync(j => j.Id == scanJobId, cancellationToken);

        return scanJob is null ? null : ToResponse(scanJob, scanJob.Findings.ToList());
    }

    public async Task<IReadOnlyList<FindingSummary>?> GetFindingsAsync(
        Guid scanJobId,
        CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.ScanJobs.AnyAsync(j => j.Id == scanJobId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        return await dbContext.Findings
            .Where(f => f.ScanJobId == scanJobId)
            .OrderBy(f => f.FilePath)
            .ThenBy(f => f.Line)
            .Select(f => new FindingSummary
            {
                RuleId = f.RuleId,
                FilePath = f.FilePath,
                Line = f.Line,
                Message = f.Message,
                Confidence = f.Confidence.ToString(),
                Evidence = f.Evidence,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ScanReportResponse?> GetReportAsync(Guid scanJobId, CancellationToken cancellationToken = default)
    {
        var scanJob = await dbContext.ScanJobs
            .Include(j => j.Findings)
            .FirstOrDefaultAsync(j => j.Id == scanJobId, cancellationToken);

        if (scanJob is null)
        {
            return null;
        }

        return new ScanReportResponse
        {
            ScanJobId = scanJob.Id,
            Format = "markdown",
            Content = reportGenerator.GenerateMarkdown(scanJob, scanJob.Findings.ToList()),
        };
    }

    private async Task<(string RepositoryPath, GitHubRepositoryRef? RepositoryRef)> ResolveRepositoryAsync(
        CreateScanRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RepositoryPath))
        {
            var path = Path.GetFullPath(request.RepositoryPath);
            if (!Directory.Exists(path))
            {
                throw new InvalidOperationException($"Repository path does not exist: {path}");
            }

            return (path, null);
        }

        if (!string.IsNullOrWhiteSpace(request.GitHubOwner) && !string.IsNullOrWhiteSpace(request.GitHubRepo))
        {
            var repositoryRef = new GitHubRepositoryRef(request.GitHubOwner, request.GitHubRepo);
            var clonedPath = await gitRepositoryService.CloneOrUpdateAsync(repositoryRef, cancellationToken);
            return (clonedPath, repositoryRef);
        }

        if (request.RepositoryId.HasValue)
        {
            var repository = await dbContext.Repositories
                .FirstOrDefaultAsync(r => r.Id == request.RepositoryId.Value, cancellationToken)
                ?? throw new InvalidOperationException($"Repository {request.RepositoryId} was not found.");

            var repositoryRef = new GitHubRepositoryRef(repository.GitHubOwner, repository.GitHubRepo, repository.DefaultBranch);
            var clonedPath = await gitRepositoryService.CloneOrUpdateAsync(repositoryRef, cancellationToken);
            return (clonedPath, repositoryRef);
        }

        throw new InvalidOperationException("Provide repositoryPath, gitHubOwner/gitHubRepo, or repositoryId.");
    }

    private bool ShouldCreatePullRequest(CreateScanRequest request, GitHubRepositoryRef? repositoryRef)
    {
        if (!request.CreatePullRequest || repositoryRef is null)
        {
            return false;
        }

        if (_gitHubOptions.AutoMerge)
        {
            throw new InvalidOperationException("Auto-merge is disabled by policy.");
        }

        return gitHubPullRequestService.IsConfigured;
    }

    private async Task CreateDraftPullRequestAsync(
        ScanJob scanJob,
        IReadOnlyList<Finding> findings,
        GitHubRepositoryRef repositoryRef,
        string provider,
        CancellationToken cancellationToken)
    {
        // Stable branch per repo+provider so repeat scans update one draft PR (idempotent).
        var branchName = GitHubBranchNames.MigrationBranch(_gitHubOptions.BranchPrefix, provider);
        var reportPath = GitHubBranchNames.MigrationReportPath();
        var historyReportPath = GitHubBranchNames.HistoricalReportPath(scanJob.Id);
        var report = reportGenerator.GenerateMarkdown(scanJob, findings);

        await gitRepositoryService.CommitReportsAsync(
            scanJob.RepositoryPath!,
            branchName,
            [
                new GitReportFile(reportPath, report),
                new GitReportFile(historyReportPath, report),
            ],
            cancellationToken);

        var title = $"ApiMorph: {provider} API migration report ({findings.Count} findings)";
        var body = $"""
            ## ApiMorph draft migration report

            This **draft** PR was opened by ApiMorph (detect-only mode).

            - Scan job: `{scanJob.Id}`
            - Findings: **{findings.Count}**
            - Latest report: `{reportPath}`
            - History copy: `{historyReportPath}`

            Repeat scans update this branch and reuse the open draft PR when possible.

            ### Review required
            - Do not merge without human review.
            - ApiMorph did not apply automatic code changes in this scan (Stage 5).
            """;

        var pullRequest = await gitHubPullRequestService.CreateDraftPullRequestAsync(
            repositoryRef,
            branchName,
            title,
            body,
            cancellationToken);

        scanJob.BranchName = pullRequest.BranchName;
        scanJob.PullRequestUrl = pullRequest.Url;
        scanJob.PullRequestNumber = pullRequest.Number;
    }

    private static ConfidenceLevel ParseConfidence(string confidence) =>
        confidence.ToLowerInvariant() switch
        {
            "high" => ConfidenceLevel.High,
            "low" => ConfidenceLevel.Low,
            _ => ConfidenceLevel.Medium,
        };

    private static ScanJobResponse ToResponse(ScanJob scanJob, IReadOnlyList<Finding> findings) =>
        new()
        {
            Id = scanJob.Id,
            Status = scanJob.Status.ToString(),
            TriggeredAt = scanJob.TriggeredAt,
            CompletedAt = scanJob.CompletedAt,
            RepositoryPath = scanJob.RepositoryPath,
            Error = scanJob.Error,
            FindingCount = findings.Count,
            Findings = findings
                .OrderBy(f => f.FilePath)
                .ThenBy(f => f.Line)
                .Select(f => new FindingSummary
                {
                    RuleId = f.RuleId,
                    FilePath = f.FilePath,
                    Line = f.Line,
                    Message = f.Message,
                    Confidence = f.Confidence.ToString(),
                    Evidence = f.Evidence,
                })
                .ToList(),
            PullRequestUrl = scanJob.PullRequestUrl,
            PullRequestNumber = scanJob.PullRequestNumber,
        };
}
