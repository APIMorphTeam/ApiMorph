using System.Net.Http.Json;
using ApiMorph.Orchestrator.Application.Contracts;
using ApiMorph.Orchestrator.Infrastructure.Data;
using ApiMorph.Orchestrator.Infrastructure.Engine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiMorph.Orchestrator.Tests;

public class ScanApiIntegrationTests : IClassFixture<ScanApiFactory>
{
    private readonly HttpClient _client;

    public ScanApiIntegrationTests(ScanApiFactory factory)
    {
        _client = factory.CreateClient();
        _factory = factory;
    }

    private ScanApiFactory _factory { get; }

    [Fact]
    public async Task CreateScan_PersistsFindingsAndReturnsReport()
    {
        var demoPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "examples", "stripe-csharp-demo", "StripeDemo"));

        var createResponse = await _client.PostAsJsonAsync("/api/v1/scans", new CreateScanRequest
        {
            RepositoryPath = demoPath,
            Provider = "stripe",
            Language = "csharp",
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ScanJobResponse>();
        Assert.NotNull(created);
        Assert.Equal("Completed", created.Status);
        Assert.True(created.FindingCount >= 3);

        var reportResponse = await _client.GetAsync($"/api/v1/scans/{created.Id}/report");
        reportResponse.EnsureSuccessStatusCode();
        var report = await reportResponse.Content.ReadFromJsonAsync<ScanReportResponse>();
        Assert.NotNull(report);
        Assert.Equal("markdown", report.Format);
        Assert.Contains("stripe.api-version.deprecated", report.Content);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiMorphDbContext>();
        var persistedFindings = await db.Findings.Where(f => f.ScanJobId == created.Id).CountAsync();
        Assert.True(persistedFindings >= 3);
    }

    [Fact]
    public async Task CreateScan_ReturnsBadRequest_WhenRepositoryPathMissing()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/scans", new CreateScanRequest());
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class ScanApiFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApiMorphDbContext>>();
            services.RemoveAll<ApiMorphDbContext>();

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            services.AddDbContext<ApiMorphDbContext>(options =>
                options.UseSqlite(_connection));

            services.RemoveAll<IEngineClient>();
            services.AddSingleton<IEngineClient, FakeEngineClient>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class FakeEngineClient : IEngineClient
{
    public Task<HealthResponseDto> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new HealthResponseDto { Status = "ok" });

    public Task<AnalyzeResponseDto> AnalyzeAsync(AnalyzeRequestDto request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AnalyzeResponseDto
        {
            ContractVersion = "1",
            Findings =
            [
                new FindingDto
                {
                    RuleId = "stripe.api-version.deprecated",
                    FilePath = "Services/PaymentService.cs",
                    Line = 8,
                    Message = "Deprecated Stripe API version configured in code",
                    Confidence = "high",
                    Evidence = "StripeConfiguration.ApiVersion = \"2019-12-03\";",
                },
                new FindingDto
                {
                    RuleId = "stripe.charge.source-deprecated",
                    FilePath = "Services/PaymentService.cs",
                    Line = 15,
                    Message = "ChargeCreateOptions.Source is deprecated; use PaymentMethod instead",
                    Confidence = "medium",
                    Evidence = "Source = token,",
                },
                new FindingDto
                {
                    RuleId = "stripe.openapi.removed-operation",
                    FilePath = "Services/PaymentService.cs",
                    Line = 21,
                    Message = "Operation removed from API contract: POST /v1/charges/{charge}/refund",
                    Confidence = "medium",
                    Evidence = "var refundService = new RefundService();",
                },
            ],
            Summary = new AnalyzeSummaryDto
            {
                FilesScanned = 2,
                FindingCount = 3,
            },
        });
    }
}
