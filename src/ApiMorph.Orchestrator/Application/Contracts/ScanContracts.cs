namespace ApiMorph.Orchestrator.Application.Contracts;

public sealed class CreateScanRequest
{
    public string? RepositoryPath { get; set; }

    public string Provider { get; set; } = "stripe";

    public string Language { get; set; } = "csharp";

    public Guid? RepositoryId { get; set; }

    public string? GitHubOwner { get; set; }

    public string? GitHubRepo { get; set; }

    public bool CreatePullRequest { get; set; }
}

public sealed record FindingSummary
{
    public required string RuleId { get; init; }

    public required string FilePath { get; init; }

    public required int Line { get; init; }

    public required string Message { get; init; }

    public required string Confidence { get; init; }

    public string? Evidence { get; init; }
}

public sealed record ScanJobLinks
{
    public required string Self { get; init; }

    public required string ReportMarkdown { get; init; }

    public required string ReportJson { get; init; }

    public required string Findings { get; init; }
}

public sealed record ScanJobResponse
{
    public required Guid Id { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset TriggeredAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? RepositoryPath { get; init; }

    public string? Error { get; init; }

    public int FindingCount { get; init; }

    public IReadOnlyList<FindingSummary> Findings { get; init; } = [];

    public string? PullRequestUrl { get; init; }

    public int? PullRequestNumber { get; init; }

    public ScanJobLinks? Links { get; init; }
}

public sealed record ScanReportResponse
{
    public required Guid ScanJobId { get; init; }

    public required string Format { get; init; }

    public required string Content { get; init; }
}
