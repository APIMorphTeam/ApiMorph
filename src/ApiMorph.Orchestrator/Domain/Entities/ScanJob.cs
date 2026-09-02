using ApiMorph.Orchestrator.Domain.Enums;

namespace ApiMorph.Orchestrator.Domain.Entities;

public class ScanJob
{
    public Guid Id { get; set; }

    public Guid? RepositoryId { get; set; }

    public Repository? Repository { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public DateTimeOffset TriggeredAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? Error { get; set; }

    public string? RepositoryPath { get; set; }

    public string? BranchName { get; set; }

    public string? PullRequestUrl { get; set; }

    public int? PullRequestNumber { get; set; }

    public string PatchMode { get; set; } = "detect-only";

    public int PatchCount { get; set; }

  /// <summary>JSON array of patch summaries (metadata only, no file content).</summary>
    public string? PatchesJson { get; set; }

    public ICollection<Finding> Findings { get; set; } = [];
}
