using ApiMorph.Orchestrator.Infrastructure.Engine;
using ApiMorph.Orchestrator.Infrastructure.GitHub;
using Microsoft.AspNetCore.Mvc;

namespace ApiMorph.Orchestrator.Controllers;

[ApiController]
[Route("api/v1")]
public class StatusController(
    IEngineClient engineClient,
    IGitHubCredentialProvider gitHubCredentialProvider,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        string engineStatus;
        string? engineError = null;

        try
        {
            var health = await engineClient.GetHealthAsync(cancellationToken);
            engineStatus = health.Status.Equals("ok", StringComparison.OrdinalIgnoreCase) ? "ok" : "degraded";
        }
        catch (Exception ex)
        {
            engineStatus = "unreachable";
            engineError = ex.Message;
        }

        var github = BuildGitHubStatus();

        return Ok(new
        {
            service = "apimorph-orchestrator",
            version = typeof(StatusController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            engine = engineError is null
                ? (object)new { status = engineStatus }
                : new { status = engineStatus, error = engineError },
            configuration = new
            {
                llmEnabled = configuration.GetValue("Llm:Enabled", false),
                patchEnabled = configuration.GetValue("Patch:Enabled", true),
                autoMerge = configuration.GetValue("GitHub:AutoMerge", false),
                githubAuthMode = github.AuthMode,
                githubConfigured = github.Configured,
                githubAppIdConfigured = github.AppIdConfigured,
                githubPrivateKeyConfigured = github.PrivateKeyConfigured,
                githubInstallationIdConfigured = github.InstallationIdConfigured,
                githubPatConfigured = github.PatConfigured,
            },
        });
    }

    private GitHubStatusSnapshot BuildGitHubStatus()
    {
        var appId = !string.IsNullOrWhiteSpace(configuration["GitHub:AppId"]);
        var installationId = GitHubInstallationIdParser.Parse(configuration["GitHub:InstallationId"]) is > 0;
        var keyPath = configuration["GitHub:AppPrivateKeyPath"];
        var keyInline = configuration["GitHub:AppPrivateKey"];
        var privateKey =
            (!string.IsNullOrWhiteSpace(keyPath) && System.IO.File.Exists(keyPath))
            || (!string.IsNullOrWhiteSpace(keyInline) && keyInline.Contains("BEGIN", StringComparison.Ordinal));
        var pat = !string.IsNullOrWhiteSpace(configuration["GitHub:Token"]);

        return new GitHubStatusSnapshot(
            AuthMode: gitHubCredentialProvider.AuthMode.ToString().ToLowerInvariant(),
            Configured: gitHubCredentialProvider.IsConfigured,
            AppIdConfigured: appId,
            PrivateKeyConfigured: privateKey,
            InstallationIdConfigured: installationId,
            PatConfigured: pat);
    }

    private sealed record GitHubStatusSnapshot(
        string AuthMode,
        bool Configured,
        bool AppIdConfigured,
        bool PrivateKeyConfigured,
        bool InstallationIdConfigured,
        bool PatConfigured);
}
