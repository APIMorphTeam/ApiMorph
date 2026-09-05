namespace ApiMorph.Orchestrator.Domain.Entities;

public class Repository
{
    public Guid Id { get; set; }

    public Guid InstallationId { get; set; }

    public Installation? Installation { get; set; }

    public required string GitHubOwner { get; set; }

    public required string GitHubRepo { get; set; }

    public string DefaultBranch { get; set; } = "main";

    public string Providers { get; set; } = "stripe";

    public bool Enabled { get; set; } = true;

    public string? WebhookBranches { get; set; }

    public string? ScheduleCron { get; set; }

    public DateTimeOffset? LastScanAt { get; set; }

    public ICollection<ScanJob> ScanJobs { get; set; } = [];
}
