namespace ApiMorph.Cli.Commands;

internal static class ConfigValidateCommand
{
    public static int Run(string[] args)
    {
        var root = CliPaths.FindRepositoryRoot();
        var configPath = Path.Combine(root, "deploy", "config");

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--path" or "-p")
            {
                configPath = args[i + 1];
            }
        }

        Console.WriteLine("ApiMorph config validate");
        Console.WriteLine("========================");
        Console.WriteLine($"Config path: {configPath}");
        Console.WriteLine();

        if (!Directory.Exists(configPath))
        {
            Console.WriteLine("[FAIL] Config directory missing");
            return 1;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "apimorph.conf", "github.conf", "triggers.conf", "scan.conf" })
        {
            var file = Path.Combine(configPath, name);
            if (!File.Exists(file))
            {
                Console.WriteLine($"[WARN] Missing {name}");
                continue;
            }

            foreach (var pair in ParseFile(file))
            {
                values[pair.Key] = pair.Value;
            }

            Console.WriteLine($"[ OK ] Parsed {name}");
        }

        var webhookEnabled = GetBool(values, "webhook.enabled");
        var scheduleEnabled = GetBool(values, "schedule.enabled");
        var feedEnabled = GetBool(values, "provider_feed.enabled");

        Console.WriteLine();
        Console.WriteLine($"manual.enabled          = {GetBool(values, "manual.enabled", true)}");
        Console.WriteLine($"schedule.enabled        = {scheduleEnabled}");
        Console.WriteLine($"webhook.enabled         = {webhookEnabled}");
        Console.WriteLine($"provider_feed.enabled   = {feedEnabled}");
        Console.WriteLine($"webhook.branches        = {Get(values, "webhook.branches") ?? "main (default)"}");

        if (webhookEnabled)
        {
            var secretFile = Get(values, "webhook.secret_file") ?? Get(values, "github.webhook_secret_file");
            var envSecret = ReadEnv(CliPaths.GetEnvFilePath(root), "GitHub__WebhookSecret");
            if (string.IsNullOrWhiteSpace(secretFile)
                && string.IsNullOrWhiteSpace(envSecret)
                && string.IsNullOrWhiteSpace(Get(values, "webhook.secret")))
            {
                Console.WriteLine("[WARN] webhook.enabled but no webhook secret configured");
            }
            else
            {
                Console.WriteLine("[ OK ] Webhook secret reference present");
            }
        }

        var reposDir = Path.Combine(configPath, "repos.d");
        var repoCount = Directory.Exists(reposDir)
            ? Directory.GetFiles(reposDir, "*.conf").Count(f => !f.EndsWith(".example", StringComparison.OrdinalIgnoreCase))
            : 0;
        Console.WriteLine($"registered repo confs   = {repoCount}");
        Console.WriteLine();
        Console.WriteLine("Validation finished.");
        return 0;
    }

    private static Dictionary<string, string> ParseFile(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var commentIndex = line.IndexOf('#');
            if (commentIndex >= 0)
            {
                line = line[..commentIndex].TrimEnd();
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            result[line[..eq].Trim()] = line[(eq + 1)..].Trim().Trim('"');
        }

        return result;
    }

    private static string? Get(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static bool GetBool(Dictionary<string, string> values, string key, bool defaultValue = false)
    {
        var raw = Get(values, key);
        if (raw is null)
        {
            return defaultValue;
        }

        return raw is "true" or "yes" or "1" or "TRUE" or "YES";
    }

    private static string? ReadEnv(string envPath, string key)
    {
        if (!File.Exists(envPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(envPath))
        {
            if (line.StartsWith('#') || !line.Contains('='))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return parts[1].Trim();
            }
        }

        return null;
    }
}
