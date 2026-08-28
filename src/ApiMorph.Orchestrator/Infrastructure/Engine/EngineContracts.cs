namespace ApiMorph.Orchestrator.Infrastructure.Engine;

public sealed class AnalyzeOptionsDto
{
    public bool DetectOnly { get; set; } = true;

    public bool LlmEnabled { get; set; }
}

public sealed class AnalyzeRequestDto
{
    public required string ContractVersion { get; set; }

    public required string Provider { get; set; }

    public required string RepositoryPath { get; set; }

    public required string Language { get; set; }

    public AnalyzeOptionsDto Options { get; set; } = new();
}

public sealed class FindingDto
{
    public required string RuleId { get; set; }

    public required string FilePath { get; set; }

    public int Line { get; set; }

    public required string Message { get; set; }

    public required string Confidence { get; set; }

    public string? Evidence { get; set; }
}

public sealed class AnalyzeSummaryDto
{
    public int FilesScanned { get; set; }

    public int FindingCount { get; set; }
}

public sealed class AnalyzeResponseDto
{
    public required string ContractVersion { get; set; }

    public List<FindingDto> Findings { get; set; } = [];

    public AnalyzeSummaryDto Summary { get; set; } = new();
}

public sealed class HealthResponseDto
{
    public required string Status { get; set; }
}
