using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiMorph.Cli;

internal sealed class OrchestratorApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync("/health", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions, cancellationToken);
            return body?.Status.Equals("ok", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<StatusResponse?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("/api/v1/status", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StatusResponse>(JsonOptions, cancellationToken);
    }

    public async Task<ScanJobResponse> CreateScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/v1/scans", request, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Scan failed ({(int)response.StatusCode}): {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<ScanJobResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Scan response was empty.");
    }

    public async Task<string> GetReportMarkdownAsync(Guid scanJobId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/v1/scans/{scanJobId}/report?format=markdown", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    internal sealed class HealthResponse
    {
        public string Status { get; set; } = string.Empty;
    }

    internal sealed class StatusResponse
    {
        public string Service { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public EngineStatus? Engine { get; set; }

        public ConfigurationStatus? Configuration { get; set; }
    }

    internal sealed class EngineStatus
    {
        public string Status { get; set; } = string.Empty;
    }

    internal sealed class ConfigurationStatus
    {
        public bool LlmEnabled { get; set; }

        public bool PatchEnabled { get; set; }

        public bool AutoMerge { get; set; }

        public string? GithubAuthMode { get; set; }

        public bool GithubConfigured { get; set; }

        public bool GithubAppIdConfigured { get; set; }

        public bool GithubPrivateKeyConfigured { get; set; }

        public bool GithubInstallationIdConfigured { get; set; }

        public bool GithubPatConfigured { get; set; }

        public bool WebhookEnabled { get; set; }

        public bool ScheduleEnabled { get; set; }

        public bool ProviderFeedEnabled { get; set; }
    }

    internal sealed class ScanRequest
    {
        public string? RepositoryPath { get; set; }

        public string? GitHubOwner { get; set; }

        public string? GitHubRepo { get; set; }

        public string Provider { get; set; } = "stripe";

        public string Language { get; set; } = "csharp";

        public bool CreatePullRequest { get; set; }

        public bool? DetectOnly { get; set; }

        public bool? LlmEnabled { get; set; }
    }

    internal sealed class ScanJobResponse
    {
        public Guid Id { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? RepositoryPath { get; set; }

        public string? Error { get; set; }

        public int FindingCount { get; set; }

        public string? PullRequestUrl { get; set; }

        public int? PullRequestNumber { get; set; }

        public string PatchMode { get; set; } = "detect-only";

        public int PatchCount { get; set; }
    }
}
