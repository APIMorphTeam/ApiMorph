using System.Text.Json;
using System.Text.Json.Serialization;
using ApiMorph.Orchestrator.Application.Services;
using ApiMorph.Orchestrator.Infrastructure.Data;
using ApiMorph.Orchestrator.Infrastructure.Engine;
using ApiMorph.Orchestrator.Infrastructure.GitHub;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
    });

builder.Services.AddRouting(options => options.LowercaseUrls = true);

builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=apimorph.db";

builder.Services.AddDbContext<ApiMorphDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddHttpClient<IEngineClient, EngineClient>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["Engine:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(configuration.GetValue("Engine:TimeoutSeconds", 60));
});

builder.Services.AddScoped<IScanService, ScanService>();
builder.Services.AddScoped<IScanReportGenerator, ScanReportGenerator>();
builder.Services.AddSingleton<IGitRepositoryService, GitRepositoryService>();
builder.Services.AddSingleton<IGitHubPullRequestService, GitHubPullRequestService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApiMorphDbContext>();
    db.Database.Migrate();
}

app.MapControllers();

app.Run();

public partial class Program;
