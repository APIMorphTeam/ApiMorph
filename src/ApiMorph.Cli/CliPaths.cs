namespace ApiMorph.Cli;

internal static class CliPaths
{
    public static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ApiMorph.slnx"))
                || Directory.Exists(Path.Combine(current.FullName, "deploy")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    public static string GetDeployDirectory(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "deploy");

    public static string GetEnvFilePath(string repositoryRoot) =>
        Path.Combine(GetDeployDirectory(repositoryRoot), ".env");

    public static string GetEnvExamplePath(string repositoryRoot) =>
        Path.Combine(GetDeployDirectory(repositoryRoot), ".env.example");
}
