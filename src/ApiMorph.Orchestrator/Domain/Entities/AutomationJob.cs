using ApiMorph.Orchestrator.Domain.Enums;

namespace ApiMorph.Orchestrator.Domain.Entities;

public class AutomationJob
{
    public Guid Id { get; set; }

    public required string GitHubOwner { get; set; }

    public required string GitHubRepo { get; set; }

    public string Provider { get; set; } = "stripe";

    public string Language { get; set; } = "csharp";

    public bool CreatePullRequest { get; set; } = true;

    public AutomationTrigger Trigger { get; set; }

    public AutomationJobStatus Status { get; set; } = AutomationJobStatus.Pending;

    public string? Branch { get; set; }

    public string? CommitSha { get; set; }

    public string? DedupeKey { get; set; }

    public string? Error { get; set; }

    public Guid? ScanJobId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

public class ProviderFeedState
{
    public Guid Id { get; set; }

    public required string Provider { get; set; }

    public string? Fingerprint { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }

    public DateTimeOffset? LastChangedAt { get; set; }
}
