using System.Text;

namespace ApiMorph.Orchestrator.Infrastructure.Config;

/// <summary>
/// FreeRADIUS-style conf parser: ignores comments (#) and blank lines; reads key = value.
/// </summary>
public static class ConfFileParser
{
    public static Dictionary<string, string> ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return ParseLines(File.ReadLines(path));
    }

    public static Dictionary<string, string> ParseDirectory(string directory, string searchPattern = "*.conf")
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory))
        {
            return merged;
        }

        foreach (var file in Directory.GetFiles(directory, searchPattern).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var (key, value) in ParseFile(file))
            {
                merged[key] = value;
            }
        }

        return merged;
    }

    public static Dictionary<string, string> ParseLines(IEnumerable<string> lines)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var commentIndex = line.IndexOf('#');
            if (commentIndex >= 0)
            {
                line = line[..commentIndex].TrimEnd();
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim().Trim('"');
            if (key.Length == 0)
            {
                continue;
            }

            result[key] = value;
        }

        return result;
    }

    public static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    public static bool GetBool(IReadOnlyDictionary<string, string> values, string key, bool defaultValue = false)
    {
        var raw = Get(values, key);
        if (raw is null)
        {
            return defaultValue;
        }

        return raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("1", StringComparison.Ordinal);
    }

    public static TimeSpan GetTimeSpan(IReadOnlyDictionary<string, string> values, string key, TimeSpan defaultValue)
    {
        var raw = Get(values, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (TimeSpan.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        // Support 6h / 30m / 90s
        if (raw.EndsWith("h", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(raw[..^1], out var hours))
        {
            return TimeSpan.FromHours(hours);
        }

        if (raw.EndsWith("m", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(raw[..^1], out var minutes))
        {
            return TimeSpan.FromMinutes(minutes);
        }

        if (raw.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(raw[..^1], out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return defaultValue;
    }

    public static IReadOnlyList<string> GetList(IReadOnlyDictionary<string, string> values, string key, string defaultCsv = "")
    {
        var raw = Get(values, key) ?? defaultCsv;
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToArray();
    }
}
