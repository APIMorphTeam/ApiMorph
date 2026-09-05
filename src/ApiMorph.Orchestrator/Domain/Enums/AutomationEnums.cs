namespace ApiMorph.Orchestrator.Domain.Enums;

public enum AutomationTrigger
{
    Manual = 0,
    Cron = 1,
    Webhook = 2,
    ProviderFeed = 3,
}

public enum AutomationJobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Skipped = 4,
}
