using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Infrastructure.Config;

public sealed class AutomationOptionsSetup(IConfiguration configuration) : IConfigureOptions<AutomationOptions>
{
    public void Configure(AutomationOptions options)
    {
        configuration.GetSection(AutomationOptions.SectionName).Bind(options);

        var configPath = configuration["Automation:ConfigPath"]
            ?? configuration["ApiMorph:ConfigPath"]
            ?? options.ConfigPath;

        options.ConfigPath = configPath;

        if (!Directory.Exists(configPath))
        {
            return;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in new[]
                 {
                     Path.Combine(configPath, "apimorph.conf"),
                     Path.Combine(configPath, "github.conf"),
                     Path.Combine(configPath, "triggers.conf"),
                     Path.Combine(configPath, "scan.conf"),
                 })
        {
            foreach (var pair in ConfFileParser.ParseFile(file))
            {
                values[pair.Key] = pair.Value;
            }
        }

        ApplyFlatKeys(options, values);
        ApplyGitHubEnvBridges(options, values, configuration);
        options.Repos = LoadRepos(Path.Combine(configPath, "repos.d"));
    }

    private static void ApplyFlatKeys(AutomationOptions options, Dictionary<string, string> values)
    {
        options.ManualEnabled = ConfFileParser.GetBool(values, "manual.enabled", options.ManualEnabled);
        options.ScheduleEnabled = ConfFileParser.GetBool(values, "schedule.enabled", options.ScheduleEnabled);
        options.ScheduleCron = ConfFileParser.Get(values, "schedule.cron") ?? options.ScheduleCron;
        options.ScheduleTimezone = ConfFileParser.Get(values, "schedule.timezone") ?? options.ScheduleTimezone;
        options.ScheduleCreatePullRequest = ConfFileParser.GetBool(
            values,
            "schedule.create_pull_request",
            options.ScheduleCreatePullRequest);

        options.WebhookEnabled = ConfFileParser.GetBool(values, "webhook.enabled", options.WebhookEnabled);
        options.WebhookBranches = ConfFileParser.Get(values, "webhook.branches") ?? options.WebhookBranches;
        options.WebhookPathFilters = ConfFileParser.Get(values, "webhook.path_filters") ?? options.WebhookPathFilters;
        options.WebhookRequireSignature = ConfFileParser.GetBool(
            values,
            "webhook.require_signature",
            options.WebhookRequireSignature);
        options.WebhookSecretFile = ConfFileParser.Get(values, "webhook.secret_file") ?? options.WebhookSecretFile;
        options.WebhookSecret = ConfFileParser.Get(values, "webhook.secret") ?? options.WebhookSecret;

        options.ProviderFeedEnabled = ConfFileParser.GetBool(
            values,
            "provider_feed.enabled",
            options.ProviderFeedEnabled);
        options.ProviderFeedProviders = ConfFileParser.Get(values, "provider_feed.providers")
            ?? options.ProviderFeedProviders;
        options.ProviderFeedInterval = ConfFileParser.GetTimeSpan(
            values,
            "provider_feed.interval",
            options.ProviderFeedInterval);
        options.ProviderFeedCreatePullRequest = ConfFileParser.GetBool(
            values,
            "provider_feed.create_pull_request",
            options.ProviderFeedCreatePullRequest);
        options.ProviderFeedStripeOpenApiPath = ConfFileParser.Get(values, "provider_feed.stripe_openapi_path")
            ?? options.ProviderFeedStripeOpenApiPath;

        options.ScanProvider = ConfFileParser.Get(values, "scan.provider") ?? options.ScanProvider;
        options.ScanLanguage = ConfFileParser.Get(values, "scan.language") ?? options.ScanLanguage;
        options.ScanCreatePullRequest = ConfFileParser.GetBool(
            values,
            "scan.create_pull_request",
            options.ScanCreatePullRequest);

        if (values.ContainsKey("scan.detect_only"))
        {
            options.ScanDetectOnly = ConfFileParser.GetBool(values, "scan.detect_only");
        }

        if (values.ContainsKey("scan.llm_enabled"))
        {
            options.ScanLlmEnabled = ConfFileParser.GetBool(values, "scan.llm_enabled");
        }
    }

    private static void ApplyGitHubEnvBridges(
        AutomationOptions options,
        Dictionary<string, string> values,
        IConfiguration configuration)
    {
        // Bridge github.webhook_secret* from conf into automation options if not set via env section.
        options.WebhookSecretFile ??= ConfFileParser.Get(values, "github.webhook_secret_file");
        options.WebhookSecret ??= ConfFileParser.Get(values, "github.webhook_secret");

        if (string.IsNullOrWhiteSpace(options.WebhookSecret))
        {
            options.WebhookSecret = configuration["GitHub:WebhookSecret"];
        }
    }

    private static IReadOnlyList<RegisteredRepoOptions> LoadRepos(string reposDirectory)
    {
        if (!Directory.Exists(reposDirectory))
        {
            return [];
        }

        var repos = new List<RegisteredRepoOptions>();
        foreach (var file in Directory.GetFiles(reposDirectory, "*.conf")
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            if (file.EndsWith(".example", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var values = ConfFileParser.ParseFile(file);
            var owner = ConfFileParser.Get(values, "repo.owner");
            var name = ConfFileParser.Get(values, "repo.name");
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            repos.Add(new RegisteredRepoOptions
            {
                Owner = owner,
                Name = name,
                Enabled = ConfFileParser.GetBool(values, "repo.enabled", true),
                Providers = ConfFileParser.Get(values, "repo.providers") ?? "stripe",
                DefaultBranch = ConfFileParser.Get(values, "repo.default_branch") ?? "main",
                WebhookBranches = ConfFileParser.Get(values, "repo.webhook_branches"),
                ScheduleCron = ConfFileParser.Get(values, "repo.schedule_cron"),
                CreatePullRequest = ConfFileParser.GetBool(values, "repo.create_pull_request", true),
            });
        }

        return repos;
    }
}
