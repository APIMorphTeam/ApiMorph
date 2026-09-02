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
    IOptions<PatchOptions> patchOptions,
    IOptions<LlmOptions> llmOptions,
    ILogger<ScanService> logger) : IScanService
{
    private readonly GitHubOptions _gitHubOptions = gitHubOptions.Value;
    private readonly PatchOptions _patchOptions = patchOptions.Value;
    private readonly LlmOptions _llmOptions = llmOptions.Value;

    public async Task<ScanJobResponse> CreateAndRunAsync(CreateScanRequest request, CancellationToken cancellationToken = default)
    {
        var (repositoryPath, repositoryRef) = await ResolveRepositoryAsync(request, cancellationToken);
        var detectOnly = ResolveDetectOnly(request);
        var llmEnabled = ResolveLlmEnabled(request);

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
                        DetectOnly = detectOnly,
                        LlmEnabled = llmEnabled,
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

            var patchMode = analyzeResponse.Summary.PatchMode;
            var patches = analyzeResponse.Patches;

            if (ShouldCreatePullRequest(request, repositoryRef))
            {
                await CreateDraftPullRequestAsync(
                    scanJob,
                    findings,
                    patches,
                    patchMode,
                    repositoryRef!,
                    request.Provider,
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ToResponse(scanJob, findings, patchMode, patches.Count);
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

    private bool ResolveDetectOnly(CreateScanRequest request)
    {
        if (request.DetectOnly.HasValue)
        {
            return request.DetectOnly.Value;
        }

        return !_patchOptions.Enabled;
    }

    private bool ResolveLlmEnabled(CreateScanRequest request) =>
        request.LlmEnabled ?? _llmOptions.Enabled;

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
        IReadOnlyList<FilePatchDto> patches,
        string patchMode,
        GitHubRepositoryRef repositoryRef,
        string provider,
        CancellationToken cancellationToken)
    {
        var branchName = GitHubBranchNames.MigrationBranch(_gitHubOptions.BranchPrefix, provider);
        var reportPath = GitHubBranchNames.MigrationReportPath();
        var historyReportPath = GitHubBranchNames.HistoricalReportPath(scanJob.Id);
        var report = reportGenerator.GenerateMarkdown(scanJob, findings, patchMode, patches);

        var files = new List<GitFileChange>
        {
            new(reportPath, report),
            new(historyReportPath, report),
        };

        foreach (var patch in patches)
        {
            files.Add(new GitFileChange(patch.FilePath, patch.Content));
        }

        var commitMessage = patches.Count > 0
            ? "chore(apimorph): apply API migration patches and update report"
            : "chore(apimorph): update API migration scan report";

        await gitRepositoryService.CommitMigrationAsync(
            scanJob.RepositoryPath!,
            branchName,
            files,
            commitMessage,
            cancellationToken);

        var title = patches.Count > 0
            ? $"ApiMorph: {provider} API migration ({findings.Count} findings, {patches.Count} patches)"
            : $"ApiMorph: {provider} API migration report ({findings.Count} findings)";

        var body = BuildPullRequestBody(scanJob, findings, patches, patchMode, reportPath, historyReportPath);

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

    private static string BuildPullRequestBody(
        ScanJob scanJob,
        IReadOnlyList<Finding> findings,
        IReadOnlyList<FilePatchDto> patches,
        string patchMode,
        string reportPath,
        string historyReportPath)
    {
        var lines = new List<string>
        {
            "## ApiMorph draft migration",
            string.Empty,
            "This **draft** PR was opened by ApiMorph.",
            string.Empty,
            $"- Scan job: `{scanJob.Id}`",
            $"- Findings: **{findings.Count}**",
            $"- Patch mode: **{patchMode}**",
            $"- Patches applied: **{patches.Count}**",
            $"- Latest report: `{reportPath}`",
            $"- History copy: `{historyReportPath}`",
            string.Empty,
            "Repeat scans update this branch and reuse the open draft PR when possible.",
            string.Empty,
        };

        if (patches.Count > 0)
        {
            lines.Add("### Applied patches");
            lines.Add(string.Empty);

            foreach (var patch in patches.OrderBy(p => p.FilePath))
            {
                lines.Add($"- `{patch.FilePath}` ({patch.PatchType}) — {patch.Description}");
            }

            lines.Add(string.Empty);
        }
        else
        {
            lines.Add("### Detect-only");
            lines.Add(string.Empty);
            lines.Add("No code patches were applied in this scan. Enable `Patch:Enabled` or pass `\"detectOnly\": false` to apply deterministic fixes.");
            lines.Add(string.Empty);
        }

        lines.Add("### Review required");
        lines.Add("- Do not merge without human review.");
        lines.Add("- Verify deterministic and LLM-assisted changes against Stripe documentation.");

        return string.Join(Environment.NewLine, lines);
    }

    private static ConfidenceLevel ParseConfidence(string confidence) =>
        confidence.ToLowerInvariant() switch
        {
            "high" => ConfidenceLevel.High,
            "low" => ConfidenceLevel.Low,
            _ => ConfidenceLevel.Medium,
        };

    private static ScanJobResponse ToResponse(
        ScanJob scanJob,
        IReadOnlyList<Finding> findings,
        string patchMode = "detect-only",
        int patchCount = 0) =>
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
            PatchMode = patchMode,
            PatchCount = patchCount,
        };
}
