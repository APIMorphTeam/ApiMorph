using System.Text.RegularExpressions;

namespace ApiMorph.Orchestrator.Infrastructure.GitHub;

/// <summary>
/// Accepts a numeric installation id or a full GitHub installations URL.
/// </summary>
public static partial class GitHubInstallationIdParser
{
    [GeneratedRegex(@"installations/(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InstallationUrlRegex();

    public static long? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('"');
        if (long.TryParse(trimmed, out var direct) && direct > 0)
        {
            return direct;
        }

        var match = InstallationUrlRegex().Match(trimmed);
        if (match.Success && long.TryParse(match.Groups["id"].Value, out var fromUrl) && fromUrl > 0)
        {
            return fromUrl;
        }

        return null;
    }
}
