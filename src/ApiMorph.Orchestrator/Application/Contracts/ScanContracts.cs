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

public sealed class ScanJobResponse
{
    public required Guid Id { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset TriggeredAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? RepositoryPath { get; init; }

    public string? Error { get; init; }

    public int FindingCount { get; init; }

    public string? PullRequestUrl { get; init; }

    public int? PullRequestNumber { get; init; }
}

public sealed class ScanReportResponse
{
    public required Guid ScanJobId { get; init; }

    public required string Format { get; init; }

    public required string Content { get; init; }
}
