using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Infrastructure.GitHub;

public sealed class GitRepositoryService(
    IGitHubCredentialProvider credentialProvider,
    IOptions<GitHubOptions> options,
    ILogger<GitRepositoryService> logger) : IGitRepositoryService
{
    private readonly GitHubOptions _options = options.Value;

    public async Task<string> CloneOrUpdateAsync(GitHubRepositoryRef repository, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_options.WorkspacePath);
        var targetPath = Path.Combine(_options.WorkspacePath, repository.Owner, repository.Repo);
        var credential = credentialProvider.IsConfigured
            ? await credentialProvider.GetAccessTokenAsync(cancellationToken)
            : null;

        if (!Directory.Exists(Path.Combine(targetPath, ".git")))
        {
            var parentDirectory = Path.GetDirectoryName(targetPath)!;
            Directory.CreateDirectory(parentDirectory);
            var cloneUrl = BuildAuthenticatedUrl(repository, credential);
            await RunGitAsync(parentDirectory, $"clone {cloneUrl} \"{targetPath}\"", cancellationToken);
        }
        else
        {
            await EnsureRemoteUrlAsync(targetPath, repository, credential, cancellationToken);
            await RunGitAsync(targetPath, "fetch origin", cancellationToken);
            await RunGitAsync(targetPath, $"checkout {repository.DefaultBranch}", cancellationToken);
            await RunGitAsync(targetPath, "pull --ff-only origin " + repository.DefaultBranch, cancellationToken);
        }

        return targetPath;
    }

    public async Task CommitReportAsync(
        string repositoryPath,
        string branchName,
        string relativeReportPath,
        string reportContent,
        CancellationToken cancellationToken = default) =>
        await CommitReportsAsync(
            repositoryPath,
            branchName,
            [new GitReportFile(relativeReportPath, reportContent)],
            cancellationToken);

    public async Task CommitReportsAsync(
        string repositoryPath,
        string branchName,
        IReadOnlyList<GitReportFile> reports,
        CancellationToken cancellationToken = default)
    {
        var files = reports
            .Select(report => new GitFileChange(report.RelativePath, report.Content))
            .ToList();

        await CommitMigrationAsync(
            repositoryPath,
            branchName,
            files,
            "chore(apimorph): update API migration scan report",
            cancellationToken);
    }

    public async Task CommitMigrationAsync(
        string repositoryPath,
        string branchName,
        IReadOnlyList<GitFileChange> files,
        string commitMessage,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
        {
            throw new ArgumentException("At least one file change is required.", nameof(files));
        }

        var credential = credentialProvider.IsConfigured
            ? await credentialProvider.GetAccessTokenAsync(cancellationToken)
            : null;

        // Refresh remote credentials before network ops (installation tokens expire ~1h).
        var ownerRepo = ParseOwnerRepoFromPath(repositoryPath);
        if (ownerRepo is not null)
        {
            await EnsureRemoteUrlAsync(repositoryPath, ownerRepo, credential, cancellationToken);
        }

        await CheckoutMigrationBranchAsync(repositoryPath, branchName, cancellationToken);

        foreach (var file in files)
        {
            var fullPath = Path.Combine(repositoryPath, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, file.Content, Encoding.UTF8, cancellationToken);
            await RunGitAsync(
                repositoryPath,
                "add " + file.RelativePath.Replace('\\', '/'),
                cancellationToken);
        }

        var status = await RunGitAsync(repositoryPath, "status --porcelain", cancellationToken);
        if (string.IsNullOrWhiteSpace(status))
        {
            logger.LogInformation("No changes to commit for branch {BranchName}", branchName);
            return;
        }

        await EnsureGitIdentityAsync(repositoryPath, cancellationToken);

        await RunGitAsync(
            repositoryPath,
            $"commit -m \"{EscapeGitCommitMessage(commitMessage)}\"",
            cancellationToken);

        await RunGitAsync(repositoryPath, $"push -u origin {branchName}", cancellationToken);
    }

    private async Task CheckoutMigrationBranchAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken)
    {
        await RunGitAsync(repositoryPath, "fetch origin", cancellationToken);

        if (await RemoteBranchExistsAsync(repositoryPath, branchName, cancellationToken))
        {
            await RunGitAsync(repositoryPath, $"checkout {branchName}", cancellationToken);
            await RunGitAsync(repositoryPath, $"pull --ff-only origin {branchName}", cancellationToken);
            return;
        }

        await RunGitAsync(repositoryPath, $"checkout -B {branchName}", cancellationToken);
    }

    private static async Task<bool> RemoteBranchExistsAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken)
    {
        var output = await RunGitAsync(
            repositoryPath,
            $"ls-remote --heads origin {branchName}",
            cancellationToken);

        return !string.IsNullOrWhiteSpace(output);
    }

    private async Task EnsureGitIdentityAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(_options.CommitAuthorName)
            ? "ApiMorph Bot"
            : _options.CommitAuthorName.Trim();
        var email = string.IsNullOrWhiteSpace(_options.CommitAuthorEmail)
            ? "apimorph-bot@users.noreply.github.com"
            : _options.CommitAuthorEmail.Trim();

        await RunGitAsync(repositoryPath, $"config user.name \"{EscapeGitConfigValue(name)}\"", cancellationToken);
        await RunGitAsync(repositoryPath, $"config user.email \"{EscapeGitConfigValue(email)}\"", cancellationToken);
    }

    private async Task EnsureRemoteUrlAsync(
        string repositoryPath,
        GitHubRepositoryRef repository,
        GitHubAccessCredential? credential,
        CancellationToken cancellationToken)
    {
        var url = BuildAuthenticatedUrl(repository, credential);
        await RunGitAsync(repositoryPath, $"remote set-url origin {url}", cancellationToken);
    }

    private static GitHubRepositoryRef? ParseOwnerRepoFromPath(string repositoryPath)
    {
        var parts = repositoryPath.Replace('\\', '/').TrimEnd('/').Split('/');
        if (parts.Length < 2)
        {
            return null;
        }

        return new GitHubRepositoryRef(parts[^2], parts[^1]);
    }

    private static string EscapeGitConfigValue(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string EscapeGitCommitMessage(string value) =>
        value.Replace("\"", "\\\"", StringComparison.Ordinal);

    internal static string BuildAuthenticatedUrl(GitHubRepositoryRef repository, GitHubAccessCredential? credential)
    {
        if (credential is null || string.IsNullOrWhiteSpace(credential.Token))
        {
            return $"https://github.com/{repository.Owner}/{repository.Repo}.git";
        }

        // GitHub App installation tokens must use the x-access-token username.
        // PATs also work with this form.
        var token = Uri.EscapeDataString(credential.Token);
        return $"https://x-access-token:{token}@github.com/{repository.Owner}/{repository.Repo}.git";
    }

    private static async Task<string> RunGitAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {arguments} failed: {stderr}".Trim());
        }

        return stdout;
    }
}
