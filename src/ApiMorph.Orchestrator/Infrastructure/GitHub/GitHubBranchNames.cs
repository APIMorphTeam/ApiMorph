namespace ApiMorph.Orchestrator.Infrastructure.GitHub;

public static class GitHubBranchNames
{
    public static string MigrationBranch(string branchPrefix, string provider) =>
        $"{branchPrefix.TrimEnd('/')}/{provider.Trim().ToLowerInvariant()}-migration";

    public static string MigrationReportPath() => "apimorph/reports/migration-report.md";

    public static string HistoricalReportPath(Guid scanJobId) =>
        $"apimorph/reports/history/scan-{scanJobId:N}.md";
}
