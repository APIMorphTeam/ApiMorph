using System.Net.Http.Json;

namespace ApiMorph.Orchestrator.Infrastructure.Engine;

public interface IEngineClient
{
    Task<HealthResponseDto> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<AnalyzeResponseDto> AnalyzeAsync(AnalyzeRequestDto request, CancellationToken cancellationToken = default);
}

public sealed class EngineClient(HttpClient httpClient) : IEngineClient
{
    public async Task<HealthResponseDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<HealthResponseDto>("/health", cancellationToken);
        return response ?? throw new InvalidOperationException("Engine health response was empty.");
    }

    public async Task<AnalyzeResponseDto> AnalyzeAsync(AnalyzeRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/v1/analyze", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Engine analyze failed ({(int)response.StatusCode}): {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<AnalyzeResponseDto>(cancellationToken);
        return result ?? throw new InvalidOperationException("Engine analyze response was empty.");
    }
}
