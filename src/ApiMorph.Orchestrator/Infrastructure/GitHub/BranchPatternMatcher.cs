using System.Text.RegularExpressions;

namespace ApiMorph.Orchestrator.Infrastructure.GitHub;

public static class BranchPatternMatcher
{
    public static bool Matches(string branchName, IEnumerable<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (MatchesSingle(branchName, pattern.Trim()))
            {
                return true;
            }
        }

        return false;
    }

    public static bool MatchesSingle(string branchName, string pattern)
    {
        if (pattern == "*")
        {
            return true;
        }

        if (!pattern.Contains('*'))
        {
            return string.Equals(branchName, pattern, StringComparison.Ordinal);
        }

        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(branchName, regex, RegexOptions.CultureInvariant);
    }

    public static string? BranchFromRef(string? gitRef)
    {
        if (string.IsNullOrWhiteSpace(gitRef))
        {
            return null;
        }

        const string prefix = "refs/heads/";
        return gitRef.StartsWith(prefix, StringComparison.Ordinal)
            ? gitRef[prefix.Length..]
            : gitRef;
    }
}
