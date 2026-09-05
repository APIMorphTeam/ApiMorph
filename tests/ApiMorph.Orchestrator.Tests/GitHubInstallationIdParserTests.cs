namespace ApiMorph.Orchestrator.Tests;

public class GitHubInstallationIdParserTests
{
    [Theory]
    [InlineData("159257812", 159257812)]
    [InlineData(" 159257812 ", 159257812)]
    [InlineData("https://github.com/organizations/APIMorphTeam/settings/installations/159257812", 159257812)]
    [InlineData("https://github.com/settings/installations/42", 42)]
    public void Parse_AcceptsNumberOrInstallationsUrl(string input, long expected)
    {
        Assert.Equal(expected, ApiMorph.Orchestrator.Infrastructure.GitHub.GitHubInstallationIdParser.Parse(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("https://github.com/APIMorphTeam/ApiMorph")]
    public void Parse_ReturnsNull_WhenInvalid(string? input)
    {
        Assert.Null(ApiMorph.Orchestrator.Infrastructure.GitHub.GitHubInstallationIdParser.Parse(input));
    }
}
