using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;

namespace ApiMorph.Orchestrator.Infrastructure.GitHub;

public sealed class GitRepositoryService(IOptions<GitHubOptions> options, ILogger<GitRepositoryService> logger)
    : IGitRepositoryService
{
    private readonly GitHubOptions _options = options.Value;

    public async Task<string> CloneOrUpdateAsync(GitHubRepositoryRef repository, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_options.WorkspacePath);
        var targetPath = Path.Combine(_options.WorkspacePath, repository.Owner, repository.Repo);

        if (!Directory.Exists(Path.Combine(targetPath, ".git")))
        {
            var parentDirectory = Path.GetDirectoryName(targetPath)!;
            Directory.CreateDirectory(parentDirectory);
            var cloneUrl = BuildCloneUrl(repository);
            await RunGitAsync(parentDirectory, $"clone {cloneUrl} \"{targetPath}\"", cancellationToken);
        }
        else
        {
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
        CancellationToken cancellationToken = default)
    {
        await RunGitAsync(repositoryPath, $"checkout -B {branchName}", cancellationToken);

        var reportFullPath = Path.Combine(repositoryPath, relativeReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportFullPath)!);
        await File.WriteAllTextAsync(reportFullPath, reportContent, Encoding.UTF8, cancellationToken);

        await RunGitAsync(repositoryPath, "add " + relativeReportPath.Replace('\\', '/'), cancellationToken);

        var status = await RunGitAsync(repositoryPath, "status --porcelain", cancellationToken);
        if (string.IsNullOrWhiteSpace(status))
        {
            logger.LogInformation("No changes to commit for branch {BranchName}", branchName);
            return;
        }

        await RunGitAsync(
            repositoryPath,
            "commit -m \"chore(apimorph): add API migration scan report\"",
            cancellationToken);

        await RunGitAsync(repositoryPath, $"push -u origin {branchName}", cancellationToken);
    }

    private string BuildCloneUrl(GitHubRepositoryRef repository)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            return $"https://github.com/{repository.Owner}/{repository.Repo}.git";
        }

        return $"https://{_options.Token}@github.com/{repository.Owner}/{repository.Repo}.git";
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
