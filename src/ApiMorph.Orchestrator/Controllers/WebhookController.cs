using System.Text;
using System.Text.Json;
using ApiMorph.Orchestrator.Application.Services;
using ApiMorph.Orchestrator.Domain.Enums;
using ApiMorph.Orchestrator.Infrastructure.Config;
using ApiMorph.Orchestrator.Infrastructure.GitHub;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
public class WebhookController(
    IOptions<AutomationOptions> automationOptions,
    IAutomationJobQueue jobQueue,
    IRepoRegistry repoRegistry,
    ILogger<WebhookController> logger) : ControllerBase
{
    private readonly AutomationOptions _options = automationOptions.Value;

    [HttpPost("github")]
    public async Task<IActionResult> GitHub(CancellationToken cancellationToken)
    {
        if (!_options.WebhookEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                detail = "Webhook trigger is disabled. Uncomment webhook.enabled = true in triggers.conf.",
            });
        }

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;
        var payloadBytes = Encoding.UTF8.GetBytes(body);

        var secret = _options.ResolveWebhookSecret();
        if (_options.WebhookRequireSignature)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    detail = "Webhook signature required but no secret configured (github.webhook_secret_file or GitHub__WebhookSecret).",
                });
            }

            var signature = Request.Headers["X-Hub-Signature-256"].ToString();
            if (!GitHubWebhookSignature.IsValid(signature, secret, payloadBytes))
            {
                return Unauthorized(new { detail = "Invalid X-Hub-Signature-256." });
            }
        }

        var eventName = Request.Headers["X-GitHub-Event"].ToString();
        if (!string.Equals(eventName, "push", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { accepted = false, reason = $"Ignored event '{eventName}'." });
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var branch = BranchPatternMatcher.BranchFromRef(root.GetPropertyOrDefault("ref"));
        if (string.IsNullOrWhiteSpace(branch))
        {
            return BadRequest(new { detail = "Missing ref in push payload." });
        }

        var repository = root.GetProperty("repository");
        var owner = repository.GetProperty("owner").GetPropertyOrDefault("login")
            ?? repository.GetProperty("owner").GetPropertyOrDefault("name");
        var name = repository.GetPropertyOrDefault("name");
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { detail = "Missing repository owner/name." });
        }

        var commitSha = root.GetPropertyOrDefault("after");
        var registered = await repoRegistry.FindAsync(owner, name, cancellationToken);

        var branchPatterns = registered is { WebhookBranches.Count: > 0 }
            ? registered.WebhookBranches
            : _options.GetWebhookBranches();

        if (branchPatterns.Count == 0)
        {
            branchPatterns = ["main"];
        }

        if (!BranchPatternMatcher.Matches(branch, branchPatterns))
        {
            return Ok(new
            {
                accepted = false,
                reason = $"Branch '{branch}' does not match webhook.branches filter.",
            });
        }

        var pathFilters = _options.GetWebhookPathFilters();
        if (pathFilters.Count > 0 && !PushTouchesFilteredPaths(root, pathFilters))
        {
            return Ok(new
            {
                accepted = false,
                reason = "Push did not touch webhook.path_filters.",
            });
        }

        // If repo is not registered, still allow webhook for known owner/repo when enabled globally.
        var createPr = registered?.CreatePullRequest ?? _options.ScanCreatePullRequest;
        var provider = registered?.Providers.FirstOrDefault() ?? _options.ScanProvider;

        var job = await jobQueue.EnqueueAsync(
            owner,
            name,
            AutomationTrigger.Webhook,
            branch: branch,
            commitSha: commitSha,
            createPullRequest: createPr,
            provider: provider,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Webhook accepted push for {Owner}/{Repo}@{Branch} job {JobId}",
            owner,
            name,
            branch,
            job?.Id);

        return Accepted(new
        {
            accepted = true,
            automationJobId = job?.Id,
            branch,
            commitSha,
        });
    }

    private static bool PushTouchesFilteredPaths(JsonElement root, IReadOnlyList<string> pathFilters)
    {
        if (!root.TryGetProperty("commits", out var commits) || commits.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        foreach (var commit in commits.EnumerateArray())
        {
            foreach (var field in new[] { "added", "removed", "modified" })
            {
                if (!commit.TryGetProperty(field, out var files) || files.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var file in files.EnumerateArray())
                {
                    var path = file.GetString();
                    if (path is not null && BranchPatternMatcher.Matches(path, pathFilters))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}

internal static class JsonElementExtensions
{
    public static string? GetPropertyOrDefault(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
