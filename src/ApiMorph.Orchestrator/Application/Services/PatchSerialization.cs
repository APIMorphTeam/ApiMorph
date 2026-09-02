using ApiMorph.Orchestrator.Infrastructure.Engine;
using System.Text.Json;

namespace ApiMorph.Orchestrator.Application.Services;

public static class PatchSerialization
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string SerializeSummaries(IReadOnlyList<FilePatchDto> patches)
    {
        if (patches.Count == 0)
        {
            return "[]";
        }

        var summaries = patches.Select(patch => new StoredPatchSummary
        {
            FilePath = patch.FilePath,
            PatchType = patch.PatchType,
            Description = patch.Description,
            LinkedRuleIds = patch.LinkedRuleIds,
        });

        return JsonSerializer.Serialize(summaries, JsonOptions);
    }

    public static IReadOnlyList<FilePatchDto> DeserializeSummaries(string? patchesJson)
    {
        if (string.IsNullOrWhiteSpace(patchesJson))
        {
            return [];
        }

        var summaries = JsonSerializer.Deserialize<List<StoredPatchSummary>>(patchesJson, JsonOptions);
        if (summaries is null || summaries.Count == 0)
        {
            return [];
        }

        return summaries
            .Select(summary => new FilePatchDto
            {
                FilePath = summary.FilePath,
                PatchType = summary.PatchType,
                Description = summary.Description,
                Content = string.Empty,
                LinkedRuleIds = summary.LinkedRuleIds,
            })
            .ToList();
    }

    private sealed class StoredPatchSummary
    {
        public required string FilePath { get; init; }

        public required string PatchType { get; init; }

        public required string Description { get; init; }

        public List<string> LinkedRuleIds { get; init; } = [];
    }
}
