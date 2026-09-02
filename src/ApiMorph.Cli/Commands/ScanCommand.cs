namespace ApiMorph.Cli.Commands;

internal static class ScanCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var options = ScanOptions.Parse(args);
        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        if (string.IsNullOrWhiteSpace(options.RepositoryPath)
            && (string.IsNullOrWhiteSpace(options.GitHubOwner) || string.IsNullOrWhiteSpace(options.GitHubRepo)))
        {
            Console.Error.WriteLine("Provide --path or both --owner and --repo.");
            PrintHelp();
            return 1;
        }

        using var httpClient = new HttpClient { BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/") };
        httpClient.Timeout = TimeSpan.FromMinutes(5);
        var api = new OrchestratorApiClient(httpClient);

        var request = new OrchestratorApiClient.ScanRequest
        {
            RepositoryPath = options.RepositoryPath,
            GitHubOwner = options.GitHubOwner,
            GitHubRepo = options.GitHubRepo,
            Provider = options.Provider,
            Language = options.Language,
            CreatePullRequest = options.CreatePullRequest,
            DetectOnly = options.DetectOnly,
            LlmEnabled = options.LlmEnabled,
        };

        Console.WriteLine($"Scanning via {options.BaseUrl} ...");
        var result = await api.CreateScanAsync(request);

        Console.WriteLine();
        Console.WriteLine($"Scan job:   {result.Id}");
        Console.WriteLine($"Status:     {result.Status}");
        Console.WriteLine($"Findings:   {result.FindingCount}");
        Console.WriteLine($"Patch mode: {result.PatchMode}");
        Console.WriteLine($"Patches:    {result.PatchCount}");

        if (!string.IsNullOrWhiteSpace(result.PullRequestUrl))
        {
            Console.WriteLine($"Pull req:   {result.PullRequestUrl}");
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            Console.WriteLine($"Error:      {result.Error}");
            return 1;
        }

        if (!result.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (options.PrintReport)
        {
            Console.WriteLine();
            Console.WriteLine("--- Report ---");
            var markdown = await api.GetReportMarkdownAsync(result.Id);
            Console.WriteLine(markdown);
        }

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
        apimorph scan — run a scan via the orchestrator API

        Usage:
          apimorph scan --owner ORG --repo NAME [--pr] [options]
          apimorph scan --path /examples/stripe-csharp-demo/StripeDemo [options]

        Options:
          --owner, -o        GitHub owner
          --repo, -r         GitHub repository name
          --path, -p         Local repository path (inside container for Docker scans)
          --pr               Create or update draft pull request
          --detect-only      Findings only, no code patches
          --llm              Enable LLM-assisted patches for this scan
          --provider         API provider (default: stripe)
          --language         Source language (default: csharp)
          --base-url         Orchestrator URL (default: http://127.0.0.1:8080)
          --report           Print Markdown report after scan
          --help, -h         Show help
        """);
    }

    private sealed class ScanOptions
    {
        public string? GitHubOwner { get; private set; }

        public string? GitHubRepo { get; private set; }

        public string? RepositoryPath { get; private set; }

        public bool CreatePullRequest { get; private set; }

        public bool? DetectOnly { get; private set; }

        public bool? LlmEnabled { get; private set; }

        public string Provider { get; private set; } = "stripe";

        public string Language { get; private set; } = "csharp";

        public string BaseUrl { get; private set; } = "http://127.0.0.1:8080";

        public bool PrintReport { get; private set; }

        public bool ShowHelp { get; private set; }

        public static ScanOptions Parse(string[] args)
        {
            var options = new ScanOptions();

            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                switch (arg)
                {
                    case "--help":
                    case "-h":
                        options.ShowHelp = true;
                        break;
                    case "--owner":
                    case "-o":
                        options.GitHubOwner = ReadValue(args, ref index);
                        break;
                    case "--repo":
                    case "-r":
                        options.GitHubRepo = ReadValue(args, ref index);
                        break;
                    case "--path":
                    case "-p":
                        options.RepositoryPath = ReadValue(args, ref index);
                        break;
                    case "--pr":
                        options.CreatePullRequest = true;
                        break;
                    case "--detect-only":
                        options.DetectOnly = true;
                        break;
                    case "--llm":
                        options.LlmEnabled = true;
                        break;
                    case "--provider":
                        options.Provider = ReadValue(args, ref index) ?? "stripe";
                        break;
                    case "--language":
                        options.Language = ReadValue(args, ref index) ?? "csharp";
                        break;
                    case "--base-url":
                        options.BaseUrl = ReadValue(args, ref index) ?? options.BaseUrl;
                        break;
                    case "--report":
                        options.PrintReport = true;
                        break;
                }
            }

            return options;
        }

        private static string? ReadValue(string[] args, ref int index)
        {
            if (index + 1 >= args.Length)
            {
                return null;
            }

            index++;
            return args[index];
        }
    }
}
