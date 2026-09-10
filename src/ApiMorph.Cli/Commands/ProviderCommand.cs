using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ApiMorph.Cli.Commands;

public static class ProviderCommand
{
    private static readonly Regex IdPattern = new("^[a-z][a-z0-9_-]{1,63}$", RegexOptions.Compiled);

    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "list" => ListProviders(),
            "init" => InitProvider(args.Skip(1).ToArray()),
            "validate" => ValidateProviders(args.Skip(1).ToArray()),
            _ => Unknown(args[0]),
        };
    }

    private static int ListProviders()
    {
        Console.WriteLine("ApiMorph provider packs");
        Console.WriteLine("=======================");

        var found = false;
        foreach (var root in EnumerateProviderRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var manifest = Path.Combine(dir, "manifest.json");
                if (!File.Exists(manifest))
                {
                    continue;
                }

                found = true;
                var id = Path.GetFileName(dir);
                var display = TryReadDisplayName(manifest) ?? id;
                var source = TryReadSourceType(manifest) ?? "manual";
                Console.WriteLine($"  {id,-20} {display}  [{source}]  ({dir})");
            }
        }

        if (!found)
        {
            Console.WriteLine("  (none found — run: apimorph provider init <id>)");
        }

        return 0;
    }

    private static readonly HashSet<string> AllowedSourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "manual", "openapi_url", "git", "github", "gitlab", "html_changelog",
    };

    private static int InitProvider(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            Console.Error.WriteLine(
                "Usage: apimorph provider init <id> [--display-name NAME] [--source TYPE] " +
                "[--openapi-url URL] [--git-url URL] [--git-path PATH] [--changelog-url URL] " +
                "[--from-openapi baseline.json target.json]");
            return 1;
        }

        var id = args[0].Trim().ToLowerInvariant();
        if (!IdPattern.IsMatch(id))
        {
            Console.Error.WriteLine("Provider id must match ^[a-z][a-z0-9_-]{1,63}$");
            return 1;
        }

        string? displayName = null;
        string? baselineSrc = null;
        string? targetSrc = null;
        var sourceType = "manual";
        string? openapiUrl = null;
        string? gitUrl = null;
        string? gitPath = null;
        string? changelogUrl = null;

        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--display-name" && i + 1 < args.Length)
            {
                displayName = args[++i];
            }
            else if (args[i] == "--source" && i + 1 < args.Length)
            {
                sourceType = args[++i].Trim().ToLowerInvariant();
            }
            else if (args[i] == "--openapi-url" && i + 1 < args.Length)
            {
                openapiUrl = args[++i];
            }
            else if (args[i] == "--git-url" && i + 1 < args.Length)
            {
                gitUrl = args[++i];
            }
            else if (args[i] == "--git-path" && i + 1 < args.Length)
            {
                gitPath = args[++i];
            }
            else if (args[i] == "--changelog-url" && i + 1 < args.Length)
            {
                changelogUrl = args[++i];
            }
            else if (args[i] == "--from-openapi" && i + 2 < args.Length)
            {
                baselineSrc = args[++i];
                targetSrc = args[++i];
            }
        }

        if (!AllowedSourceTypes.Contains(sourceType))
        {
            Console.Error.WriteLine(
                $"Unknown --source '{sourceType}'. Allowed: {string.Join(", ", AllowedSourceTypes.OrderBy(x => x))}");
            return 1;
        }

        displayName ??= CultureTitle(id);
        var root = Path.Combine(FindRepoRoot(), "deploy", "config", "providers", id);
        if (Directory.Exists(root) && File.Exists(Path.Combine(root, "manifest.json")))
        {
            Console.Error.WriteLine($"Provider pack already exists: {root}");
            return 1;
        }

        Directory.CreateDirectory(Path.Combine(root, "contract"));
        Directory.CreateDirectory(Path.Combine(root, "rules"));

        if (baselineSrc is not null && targetSrc is not null)
        {
            File.Copy(baselineSrc, Path.Combine(root, "contract", "baseline.json"), overwrite: true);
            File.Copy(targetSrc, Path.Combine(root, "contract", "target.json"), overwrite: true);
        }
        else
        {
            File.WriteAllText(
                Path.Combine(root, "contract", "baseline.json"),
                MinimalOpenApi(displayName, "1.0.0", includeLegacy: true),
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(root, "contract", "target.json"),
                MinimalOpenApi(displayName, "2.0.0", includeLegacy: false),
                Encoding.UTF8);
        }

        File.WriteAllText(
            Path.Combine(root, "rules", "csharp.json"),
            "[]\n",
            Encoding.UTF8);

        var manifest = $$"""
            {
              "id": "{{id}}",
              "displayName": "{{EscapeJson(displayName)}}",
              "languages": ["csharp"],
              "source": {
                "type": "{{sourceType}}",
                "openapiUrl": {{ToJsonStringOrNull(openapiUrl)}},
                "gitUrl": {{ToJsonStringOrNull(gitUrl)}},
                "gitPath": {{ToJsonStringOrNull(gitPath)}},
                "changelogUrl": {{ToJsonStringOrNull(changelogUrl)}}
              },
              "feed": {
                "enabled": false,
                "fingerprintFile": "contract/target.json"
              }
            }
            """;
        File.WriteAllText(Path.Combine(root, "manifest.json"), manifest.Trim() + "\n", Encoding.UTF8);

        var templatePath = Path.Combine(FindRepoRoot(), "deploy", "config", "providers", "PROVIDER.template.md");
        var providerMd = File.Exists(templatePath)
            ? File.ReadAllText(templatePath).Replace("<id>", id, StringComparison.Ordinal)
            : $"# Provider pack: {displayName}\n\nFill contract/baseline.json and contract/target.json.\n";
        File.WriteAllText(Path.Combine(root, "PROVIDER.md"), providerMd, Encoding.UTF8);

        // Validate immediately so wizard mistakes fail fast.
        var validateArgs = new[] { id };
        var validateCode = ValidateProviders(validateArgs);
        if (validateCode != 0)
        {
            Console.Error.WriteLine("Pack created but validation failed — removing incomplete pack.");
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                Console.Error.WriteLine($"Could not remove {root}; delete it manually.");
            }

            return validateCode;
        }

        Console.WriteLine($"Created provider pack: {root}");
        Console.WriteLine($"Source type: {sourceType}");
        Console.WriteLine("Next:");
        Console.WriteLine("  1. Edit contract/baseline.json and contract/target.json (or keep the stubs)");
        Console.WriteLine("  2. Optional: add rules in rules/csharp.json");
        Console.WriteLine($"  3. Set repo.providers = ...,{id} in deploy/config/repos.d/");
        if (sourceType is "html_changelog")
        {
            Console.WriteLine("  Note: html_changelog is assist-only — review the page, then update contract/ manually.");
        }

        return 0;
    }

    private static int ValidateProviders(string[] args)
    {
        var ids = args.Where(a => !a.StartsWith('-')).Select(a => a.ToLowerInvariant()).ToList();
        var packs = EnumeratePacks()
            .Where(p => ids.Count == 0 || ids.Contains(p.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (packs.Count == 0)
        {
            Console.Error.WriteLine(ids.Count == 0
                ? "No provider packs found."
                : $"No packs matching: {string.Join(", ", ids)}");
            return 1;
        }

        var errors = 0;
        foreach (var pack in packs)
        {
            var ok = true;
            void Fail(string msg)
            {
                ok = false;
                errors++;
                Console.WriteLine($"[FAIL] {pack.Id}: {msg}");
            }

            void Warn(string msg) => Console.WriteLine($"[WARN] {pack.Id}: {msg}");

            foreach (var relative in new[] { "manifest.json", "contract/baseline.json", "contract/target.json" })
            {
                if (!File.Exists(Path.Combine(pack.Root, relative)))
                {
                    Fail($"missing mandatory file {relative}");
                }
            }

            if (!File.Exists(pack.ManifestPath))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(pack.ManifestPath));
                var root = doc.RootElement;
                if (root.TryGetProperty("id", out var idEl)
                    && idEl.GetString() is { } mid
                    && !string.Equals(mid, pack.Id, StringComparison.OrdinalIgnoreCase))
                {
                    Fail($"manifest id '{mid}' != folder '{pack.Id}'");
                }

                var sourceType = "manual";
                JsonElement source = default;
                var hasSource = root.TryGetProperty("source", out source) && source.ValueKind == JsonValueKind.Object;
                if (hasSource && source.TryGetProperty("type", out var typeEl) && typeEl.GetString() is { } st)
                {
                    sourceType = st;
                }

                if (!AllowedSourceTypes.Contains(sourceType))
                {
                    Fail($"source.type '{sourceType}' is not allowed");
                }
                else if (hasSource)
                {
                    ValidateSourceFields(sourceType, source, Fail, Warn);
                }
            }
            catch (Exception ex)
            {
                Fail($"invalid manifest.json ({ex.Message})");
            }

            if (File.Exists(pack.BaselinePath) && !TryValidateOpenApi(pack.BaselinePath, out var baselineError))
            {
                Fail($"baseline.json: {baselineError}");
            }

            if (File.Exists(pack.TargetPath) && !TryValidateOpenApi(pack.TargetPath, out var targetError))
            {
                Fail($"target.json: {targetError}");
            }

            var rulesPath = Path.Combine(pack.Root, "rules", "csharp.json");
            if (File.Exists(rulesPath) && !TryParseJson(rulesPath, out var rulesError))
            {
                Fail($"rules/csharp.json: {rulesError}");
            }
            else if (!File.Exists(rulesPath))
            {
                Warn("optional rules/csharp.json is missing");
            }

            if (!File.Exists(Path.Combine(pack.Root, "PROVIDER.md")))
            {
                Warn("optional PROVIDER.md is missing");
            }

            if (ok)
            {
                Console.WriteLine($"[ OK ] {pack.Id}  ({pack.Root})");
            }
        }

        Console.WriteLine(errors == 0 ? "Validation finished." : $"Validation finished with {errors} error(s).");
        return errors == 0 ? 0 : 1;
    }

    private static void ValidateSourceFields(
        string sourceType,
        JsonElement source,
        Action<string> fail,
        Action<string> warn)
    {
        static string? GetString(JsonElement obj, string name) =>
            obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;

        static bool LooksLikeUrl(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("git@", StringComparison.OrdinalIgnoreCase));

        switch (sourceType.ToLowerInvariant())
        {
            case "manual":
                break;
            case "openapi_url":
                if (!LooksLikeUrl(GetString(source, "openapiUrl")))
                {
                    fail("source.type 'openapi_url' requires source.openapiUrl");
                }

                break;
            case "git":
            case "github":
            case "gitlab":
                if (!LooksLikeUrl(GetString(source, "gitUrl")))
                {
                    fail($"source.type '{sourceType}' requires source.gitUrl");
                }

                break;
            case "html_changelog":
                var changelog = GetString(source, "changelogUrl") ?? GetString(source, "htmlUrl");
                if (!LooksLikeUrl(changelog) || changelog!.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
                {
                    fail("source.type 'html_changelog' requires source.changelogUrl (http/https)");
                }
                else
                {
                    warn("html_changelog is assist-only: update contract/ manually after review");
                }

                break;
        }
    }

    private static bool TryValidateOpenApi(string path, out string error)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "must be a JSON object";
                return false;
            }

            var hasOpenApi = root.TryGetProperty("openapi", out _) || root.TryGetProperty("swagger", out _);
            if (!hasOpenApi)
            {
                error = "missing openapi/swagger field";
                return false;
            }

            if (!root.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object)
            {
                error = "missing paths object";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string ToJsonStringOrNull(string? value) =>
        value is null ? "null" : $"\"{EscapeJson(value)}\"";

    private static IEnumerable<string> EnumerateProviderRoots()
    {
        var repo = FindRepoRoot();
        yield return Path.Combine(repo, "providers");
        yield return Path.Combine(repo, "deploy", "config", "providers");
    }

    private static IEnumerable<PackInfo> EnumeratePacks()
    {
        foreach (var root in EnumerateProviderRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var dir in Directory.GetDirectories(root))
            {
                if (!File.Exists(Path.Combine(dir, "manifest.json")))
                {
                    continue;
                }

                yield return new PackInfo(
                    Path.GetFileName(dir),
                    dir,
                    Path.Combine(dir, "manifest.json"),
                    Path.Combine(dir, "contract", "baseline.json"),
                    Path.Combine(dir, "contract", "target.json"));
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ApiMorph.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "deploy", "config")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string? TryReadDisplayName(string manifestPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return doc.RootElement.TryGetProperty("displayName", out var el) ? el.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadSourceType(string manifestPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (doc.RootElement.TryGetProperty("source", out var source)
                && source.TryGetProperty("type", out var type))
            {
                return type.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static bool TryParseJson(string path, out string error)
    {
        try
        {
            JsonDocument.Parse(File.ReadAllText(path)).Dispose();
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string MinimalOpenApi(string title, string version, bool includeLegacy)
    {
        var legacy = includeLegacy
            ? """
                  ,
                  "/v1/items/{id}/legacy-action": {
                    "post": {
                      "operationId": "LegacyAction",
                      "responses": { "200": { "description": "ok" } }
                    }
                  }
              """
            : """
                  ,
                  "/v1/items/{id}/action": {
                    "post": {
                      "operationId": "Action",
                      "responses": { "200": { "description": "ok" } }
                    }
                  }
              """;

        return $$"""
            {
              "openapi": "3.0.0",
              "info": { "title": "{{EscapeJson(title)}}", "version": "{{version}}" },
              "paths": {
                "/v1/items/{id}": {
                  "get": {
                    "operationId": "GetItem",
                    "responses": { "200": { "description": "ok" } }
                  }
                }{{legacy}}
              }
            }
            """;
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string CultureTitle(string id) =>
        string.Join(' ', id.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown provider command: {command}");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            apimorph provider — manage OpenAPI provider packs

            Usage:
              apimorph provider list
              apimorph provider init <id> [options]
              apimorph provider validate [id]

            Init options:
              --display-name NAME
              --source manual|openapi_url|git|github|gitlab|html_changelog
              --openapi-url URL
              --git-url URL
              --git-path PATH
              --changelog-url URL
              --from-openapi baseline.json target.json

            Packs live under:
              providers/<id>/                 (shipped)
              deploy/config/providers/<id>/   (custom / operator)

            Automated tests:
              python scripts/test_provider_packs.py
            """);
    }

    private sealed record PackInfo(string Id, string Root, string ManifestPath, string BaselinePath, string TargetPath);
}
