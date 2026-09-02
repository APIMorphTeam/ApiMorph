using ApiMorph.Orchestrator.Domain.Entities;
using ApiMorph.Orchestrator.Infrastructure.Engine;

namespace ApiMorph.Orchestrator.Application.Services;

public interface IScanReportGenerator
{
    string GenerateMarkdown(
        ScanJob scanJob,
        IReadOnlyList<Finding> findings,
        string patchMode = "detect-only",
        IReadOnlyList<FilePatchDto>? patches = null);
}

public sealed class ScanReportGenerator : IScanReportGenerator
{
    public string GenerateMarkdown(
        ScanJob scanJob,
        IReadOnlyList<Finding> findings,
        string patchMode = "detect-only",
        IReadOnlyList<FilePatchDto>? patches = null)
    {
        var lines = new List<string>
        {
            "# ApiMorph Scan Report",
            string.Empty,
            $"**Scan job:** `{scanJob.Id}`",
            $"**Status:** {scanJob.Status}",
            $"**Repository path:** `{scanJob.RepositoryPath ?? "n/a"}`",
            $"**Triggered at:** {scanJob.TriggeredAt:O}",
            $"**Completed at:** {scanJob.CompletedAt:O}",
            string.Empty,
            "## Summary",
            string.Empty,
            $"- Findings: **{findings.Count}**",
            $"- Patch mode: **{patchMode}**",
            $"- Patches proposed: **{patches?.Count ?? 0}**",
            string.Empty,
        };

        if (patches is { Count: > 0 })
        {
            lines.Add("## Proposed patches");
            lines.Add(string.Empty);

            foreach (var patch in patches.OrderBy(p => p.FilePath))
            {
                lines.Add($"- `{patch.FilePath}` ({patch.PatchType})");
                lines.Add($"  - {patch.Description}");
                if (patch.LinkedRuleIds.Count > 0)
                {
                    lines.Add($"  - Rules: `{string.Join("`, `", patch.LinkedRuleIds)}`");
                }
            }

            lines.Add(string.Empty);
        }

        lines.Add("## Findings");
        lines.Add(string.Empty);

        if (findings.Count == 0)
        {
            lines.Add("_No findings detected._");
            return string.Join(Environment.NewLine, lines);
        }

        foreach (var group in findings.GroupBy(f => f.RuleId).OrderBy(g => g.Key))
        {
            lines.Add($"### `{group.Key}`");
            lines.Add(string.Empty);

            foreach (var finding in group.OrderBy(f => f.FilePath).ThenBy(f => f.Line))
            {
                lines.Add($"- **{finding.FilePath}:{finding.Line}** ({finding.Confidence})");
                lines.Add($"  - {finding.Message}");
                if (!string.IsNullOrWhiteSpace(finding.Evidence))
                {
                    lines.Add($"  - Evidence: `{finding.Evidence}`");
                }
            }

            lines.Add(string.Empty);
        }

        lines.Add("## Review checklist");
        lines.Add(string.Empty);
        lines.Add("- [ ] Verify each finding against current Stripe documentation");
        lines.Add("- [ ] Review proposed patches before merging");
        lines.Add("- [ ] Do not auto-merge without human review");

        return string.Join(Environment.NewLine, lines);
    }
}
