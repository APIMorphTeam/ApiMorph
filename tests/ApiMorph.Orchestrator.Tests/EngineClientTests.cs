using ApiMorph.Orchestrator.Infrastructure.Engine;
using Microsoft.Extensions.Configuration;

namespace ApiMorph.Orchestrator.Tests;

public class EngineClientTests
{
    [Fact]
    public void AnalyzeRequestDto_DefaultOptions_AreDetectOnlyWithoutLlm()
    {
        var request = new AnalyzeRequestDto
        {
            ContractVersion = "1",
            Provider = "stripe",
            RepositoryPath = "/workspace/demo",
            Language = "csharp"
        };

        Assert.Equal("1", request.ContractVersion);
        Assert.Equal("stripe", request.Provider);
        Assert.True(request.Options.DetectOnly);
        Assert.False(request.Options.LlmEnabled);
    }

    [Fact]
    public void Configuration_DefaultLlmAndAutoMerge_AreDisabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Assert.False(configuration.GetValue("Llm:Enabled", false));
        Assert.False(configuration.GetValue("GitHub:AutoMerge", false));
    }
}
