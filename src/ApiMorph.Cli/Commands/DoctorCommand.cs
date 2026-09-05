using System.Diagnostics;

namespace ApiMorph.Cli.Commands;

internal static class DoctorCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var baseUrl = ParseBaseUrl(args);
        Console.WriteLine("ApiMorph doctor");
        Console.WriteLine("===============");
        Console.WriteLine();

        var allOk = true;
        allOk &= CheckCommand("docker", "--version", "Docker");
        allOk &= CheckCommand("dotnet", "--version", ".NET SDK");

        var repositoryRoot = CliPaths.FindRepositoryRoot();
        var envPath = CliPaths.GetEnvFilePath(repositoryRoot);
        if (File.Exists(envPath))
        {
            Console.WriteLine($"[ OK ] Config file: {envPath}");
        }
        else
        {
            Console.WriteLine($"[WARN] Config file missing: {envPath} (run: apimorph init)");
        }

        Console.WriteLine();
        allOk &= await CheckOrchestratorAsync(baseUrl);

        var ollamaUrl = ReadEnvValue(envPath, "OLLAMA_BASE_URL") ?? "http://127.0.0.1:11434";
        if (IsTruthy(ReadEnvValue(envPath, "Llm__Enabled")))
        {
            allOk &= await CheckOllamaFromHostAsync(ollamaUrl);
            allOk &= await CheckOllamaFromEngineAsync(repositoryRoot, ollamaUrl);
        }

        Console.WriteLine();
        Console.WriteLine(allOk
            ? "All checks passed."
            : "Some checks failed. Fix the items above and run doctor again.");

        return allOk ? 0 : 1;
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

    private static async Task<bool> CheckOrchestratorAsync(string baseUrl)
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var api = new OrchestratorApiClient(httpClient);

        if (!await api.CheckHealthAsync())
        {
            Console.WriteLine($"[FAIL] Orchestrator health: unreachable at {baseUrl}");
            Console.WriteLine("       Run: cd deploy && docker compose up --build -d");
            return false;
        }

        Console.WriteLine($"[ OK ] Orchestrator health: {baseUrl}/health");

        try
        {
            var status = await api.GetStatusAsync();
            if (status is null)
            {
                Console.WriteLine("[FAIL] Orchestrator status: empty response");
                return false;
            }

            Console.WriteLine($"[ OK ] Engine status: {status.Engine?.Status ?? "unknown"}");
            Console.WriteLine($"[ OK ] Patch enabled: {status.Configuration?.PatchEnabled}");
            Console.WriteLine($"[ OK ] LLM enabled:   {status.Configuration?.LlmEnabled}");

            var authMode = status.Configuration?.GithubAuthMode ?? "none";
            var githubConfigured = status.Configuration?.GithubConfigured == true;
            if (githubConfigured)
            {
                Console.WriteLine($"[ OK ] GitHub auth:   {authMode}");
            }
            else
            {
                Console.WriteLine("[WARN] GitHub auth:   not configured (App preferred, or PAT fallback)");
                Console.WriteLine("       Run: apimorph init  — and see docs/github-app.md");
            }

            if (status.Configuration?.GithubAppIdConfigured == true
                && status.Configuration?.GithubPrivateKeyConfigured != true)
            {
                Console.WriteLine("[WARN] GitHub App ID set but private key file/content missing");
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] Orchestrator status: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> CheckOllamaFromHostAsync(string configuredUrl)
    {
        foreach (var candidate in ResolveHostOllamaUrls(configuredUrl))
        {
            if (await TryOllamaTagsAsync(candidate))
            {
                Console.WriteLine($"[ OK ] Ollama (host): {candidate}");
                if (!candidate.Equals(configuredUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                    && configuredUrl.Contains("host.docker.internal", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("       Note: deploy/.env uses host.docker.internal for Docker containers.");
                    Console.WriteLine("       Host checks use localhost; engine checks run separately below.");
                }

                return true;
            }
        }

        Console.WriteLine("[FAIL] Ollama (host): not reachable on localhost.");
        Console.WriteLine("       Start Ollama: ollama serve");
        Console.WriteLine("       Verify: curl http://127.0.0.1:11434/api/tags");
        return false;
    }

    private static async Task<bool> CheckOllamaFromEngineAsync(string repositoryRoot, string configuredUrl)
    {
        var deployDir = CliPaths.GetDeployDirectory(repositoryRoot);
        if (!Directory.Exists(deployDir))
        {
            Console.WriteLine("[WARN] Ollama (engine): deploy directory not found, skipped");
            return true;
        }

        var engineUrl = configuredUrl.Contains("host.docker.internal", StringComparison.OrdinalIgnoreCase)
            ? configuredUrl
            : "http://host.docker.internal:11434";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"compose exec -T engine python -c \"import httpx; r=httpx.get('{engineUrl.TrimEnd('/')}/api/tags', timeout=5); print(r.status_code)\"",
                WorkingDirectory = deployDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                Console.WriteLine("[WARN] Ollama (engine): could not run docker compose exec");
                return true;
            }

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && stdout.Trim() == "200")
            {
                Console.WriteLine($"[ OK ] Ollama (engine): {engineUrl}");
                return true;
            }

            Console.WriteLine($"[FAIL] Ollama (engine): cannot reach {engineUrl} from engine container");
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Console.WriteLine($"       {stderr.Trim()}");
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Ollama (engine): skipped ({ex.Message})");
            return true;
        }
    }

    private static IEnumerable<string> ResolveHostOllamaUrls(string configuredUrl)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new[]
        {
            configuredUrl,
            configuredUrl.Replace("host.docker.internal", "127.0.0.1", StringComparison.OrdinalIgnoreCase),
            "http://127.0.0.1:11434",
            "http://localhost:11434",
        };

        foreach (var candidate in candidates)
        {
            var normalized = candidate.TrimEnd('/');
            if (seen.Add(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static async Task<bool> TryOllamaTagsAsync(string baseUrl)
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await httpClient.GetAsync($"{baseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private static bool CheckCommand(string fileName, string arguments, string label)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                Console.WriteLine($"[FAIL] {label}: could not start process");
                return false;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Console.WriteLine($"[ OK ] {label}: {output.Split('\n')[0]}");
                return true;
            }

            Console.WriteLine($"[FAIL] {label}: exit code {process.ExitCode}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] {label}: {ex.Message}");
            return false;
        }
    }

    private static string? ReadEnvValue(string envPath, string key)
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

    private static bool IsTruthy(string? value) =>
        value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
}
