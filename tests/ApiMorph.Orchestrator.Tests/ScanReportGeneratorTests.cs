using ApiMorph.Orchestrator.Domain.Entities;
using ApiMorph.Orchestrator.Domain.Enums;
using ApiMorph.Orchestrator.Application.Services;

namespace ApiMorph.Orchestrator.Tests;

public class ScanReportGeneratorTests
{
    private readonly ScanReportGenerator _generator = new();

    [Fact]
    public void GenerateMarkdown_IncludesRuleGroupsAndChecklist()
    {
        var scanJob = new ScanJob
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Status = JobStatus.Completed,
            TriggeredAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-01-01T00:01:00Z"),
            RepositoryPath = "/examples/stripe-csharp-demo",
        };

        var findings = new List<Finding>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ScanJobId = scanJob.Id,
                RuleId = "stripe.api-version.deprecated",
                FilePath = "Services/PaymentService.cs",
                Line = 8,
                Message = "Deprecated Stripe API version configured in code",
                Confidence = ConfidenceLevel.High,
                Evidence = "StripeConfiguration.ApiVersion = \"2019-12-03\";",
            },
        };

        var markdown = _generator.GenerateMarkdown(scanJob, findings);

        Assert.Contains("# ApiMorph Scan Report", markdown);
        Assert.Contains("stripe.api-version.deprecated", markdown);
        Assert.Contains("Services/PaymentService.cs:8", markdown);
        Assert.Contains("Review checklist", markdown);
    }
}
