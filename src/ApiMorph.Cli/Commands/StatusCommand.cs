namespace ApiMorph.Cli.Commands;

internal static class StatusCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var baseUrl = ParseBaseUrl(args);
        using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var api = new OrchestratorApiClient(httpClient);

        var status = await api.GetStatusAsync();
        if (status is null)
        {
            Console.Error.WriteLine("Empty status response.");
            return 1;
        }

        Console.WriteLine($"Service:        {status.Service}");
        Console.WriteLine($"Version:        {status.Version}");
        Console.WriteLine($"Engine:         {status.Engine?.Status ?? "unknown"}");
        Console.WriteLine($"Patch enabled:  {status.Configuration?.PatchEnabled}");
        Console.WriteLine($"LLM enabled:    {status.Configuration?.LlmEnabled}");
        Console.WriteLine($"Auto-merge:     {status.Configuration?.AutoMerge}");
        Console.WriteLine($"GitHub auth:    {status.Configuration?.GithubAuthMode ?? "none"}");
        Console.WriteLine($"GitHub ready:   {status.Configuration?.GithubConfigured}");
        Console.WriteLine($"Webhook:        {status.Configuration?.WebhookEnabled}");
        Console.WriteLine($"Schedule:       {status.Configuration?.ScheduleEnabled}");
        Console.WriteLine($"Provider feed:  {status.Configuration?.ProviderFeedEnabled}");
        return 0;
    }

    private static string ParseBaseUrl(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index] is "--base-url")
            {
                return args[index + 1];
            }
        }

        return "http://127.0.0.1:8080";
    }
}
