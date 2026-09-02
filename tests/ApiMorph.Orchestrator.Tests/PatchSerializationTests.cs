using ApiMorph.Orchestrator.Application.Services;
using ApiMorph.Orchestrator.Infrastructure.Engine;

namespace ApiMorph.Orchestrator.Tests;

public class PatchSerializationTests
{
    [Fact]
    public void SerializeAndDeserialize_RoundTripsPatchSummaries()
    {
        var patches = new List<FilePatchDto>
        {
            new()
            {
                FilePath = "Services/PaymentService.cs",
                PatchType = "deterministic",
                Description = "Update Stripe API version",
                Content = "ignored in storage",
                LinkedRuleIds = ["stripe.api-version.deprecated"],
            },
        };

        var json = PatchSerialization.SerializeSummaries(patches);
        var restored = PatchSerialization.DeserializeSummaries(json);

        Assert.Single(restored);
        Assert.Equal("Services/PaymentService.cs", restored[0].FilePath);
        Assert.Equal("deterministic", restored[0].PatchType);
        Assert.Equal("Update Stripe API version", restored[0].Description);
        Assert.Equal("stripe.api-version.deprecated", restored[0].LinkedRuleIds[0]);
        Assert.Equal(string.Empty, restored[0].Content);
    }
}
