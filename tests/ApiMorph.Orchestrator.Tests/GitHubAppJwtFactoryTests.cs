using System.Security.Cryptography;

namespace ApiMorph.Orchestrator.Tests;

public class GitHubAppJwtFactoryTests
{
    [Fact]
    public void Create_ReturnsThreePartJwt()
    {
        using var rsa = RSA.Create(2048);
        var jwt = ApiMorph.Orchestrator.Infrastructure.GitHub.GitHubAppJwtFactory.Create("42", rsa);
        var parts = jwt.Split('.');

        Assert.Equal(3, parts.Length);
        Assert.False(string.IsNullOrWhiteSpace(parts[0]));
        Assert.False(string.IsNullOrWhiteSpace(parts[1]));
        Assert.False(string.IsNullOrWhiteSpace(parts[2]));
    }

    [Fact]
    public void LoadPrivateKey_ReadsPemFile()
    {
        using var rsa = RSA.Create(2048);
        var path = Path.Combine(Path.GetTempPath(), $"apimorph-jwt-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, rsa.ExportRSAPrivateKeyPem());

        try
        {
            using var loaded = ApiMorph.Orchestrator.Infrastructure.GitHub.GitHubAppJwtFactory.LoadPrivateKey(path);
            Assert.NotNull(loaded.ExportParameters(false).Modulus);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
