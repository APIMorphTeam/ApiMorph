using ApiMorph.Orchestrator.Infrastructure.Data;
using ApiMorph.Orchestrator.Infrastructure.Engine;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApiMorphDbContext>();
    db.Database.Migrate();
}

app.MapControllers();

app.Run();

public partial class Program;
