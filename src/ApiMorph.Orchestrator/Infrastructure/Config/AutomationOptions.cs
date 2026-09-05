namespace ApiMorph.Orchestrator.Infrastructure.Config;

public sealed class AutomationOptions
{
    public const string SectionName = "Automation";

    public string ConfigPath { get; set; } = "/config";

    public bool ManualEnabled { get; set; } = true;

    public bool ScheduleEnabled { get; set; }

    public string ScheduleCron { get; set; } = "0 2 * * *";

    public string ScheduleTimezone { get; set; } = "UTC";

    public bool ScheduleCreatePullRequest { get; set; } = true;

    public bool WebhookEnabled { get; set; }

    public string WebhookBranches { get; set; } = "main";

    public string WebhookPathFilters { get; set; } = string.Empty;

    public bool WebhookRequireSignature { get; set; } = true;

    public string? WebhookSecret { get; set; }

    public string? WebhookSecretFile { get; set; }

    public bool ProviderFeedEnabled { get; set; }

    public string ProviderFeedProviders { get; set; } = "stripe";

    public TimeSpan ProviderFeedInterval { get; set; } = TimeSpan.FromHours(6);

    public bool ProviderFeedCreatePullRequest { get; set; } = true;

    public string? ProviderFeedStripeOpenApiPath { get; set; }

    public string ScanProvider { get; set; } = "stripe";

    public string ScanLanguage { get; set; } = "csharp";

    public bool ScanCreatePullRequest { get; set; } = true;

    public bool? ScanDetectOnly { get; set; }

    public bool? ScanLlmEnabled { get; set; }

    public IReadOnlyList<RegisteredRepoOptions> Repos { get; set; } = [];

    public IReadOnlyList<string> GetWebhookBranches() =>
        WebhookBranches.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyList<string> GetWebhookPathFilters() =>
        WebhookPathFilters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyList<string> GetProviderFeedProviders() =>
        ProviderFeedProviders.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public string ResolveWebhookSecret()
    {
        if (!string.IsNullOrWhiteSpace(WebhookSecretFile) && File.Exists(WebhookSecretFile))
        {
            return File.ReadAllText(WebhookSecretFile).Trim();
        }

        return WebhookSecret?.Trim() ?? string.Empty;
    }
}

public sealed class RegisteredRepoOptions
{
    public required string Owner { get; init; }

    public required string Name { get; init; }

    public bool Enabled { get; init; } = true;

    public string Providers { get; init; } = "stripe";

    public string DefaultBranch { get; init; } = "main";

    public string? WebhookBranches { get; init; }

    public string? ScheduleCron { get; init; }

    public bool CreatePullRequest { get; init; } = true;

    public IReadOnlyList<string> GetProviders() =>
        Providers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyList<string> GetWebhookBranches() =>
        (WebhookBranches ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
