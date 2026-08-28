using ApiMorph.Orchestrator.Domain.Enums;

namespace ApiMorph.Orchestrator.Domain.Entities;

public class ScanJob
{
    public Guid Id { get; set; }

    public Guid RepositoryId { get; set; }

    public Repository? Repository { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public DateTimeOffset TriggeredAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? Error { get; set; }

    public ICollection<Finding> Findings { get; set; } = [];
}
