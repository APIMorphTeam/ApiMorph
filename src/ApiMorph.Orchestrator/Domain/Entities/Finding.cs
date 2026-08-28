using ApiMorph.Orchestrator.Domain.Enums;

namespace ApiMorph.Orchestrator.Domain.Entities;

public class Finding
{
    public Guid Id { get; set; }

    public Guid ScanJobId { get; set; }

    public ScanJob? ScanJob { get; set; }

    public required string RuleId { get; set; }

    public required string FilePath { get; set; }

    public int Line { get; set; }

    public required string Message { get; set; }

    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.Medium;

    public string? Evidence { get; set; }
}
