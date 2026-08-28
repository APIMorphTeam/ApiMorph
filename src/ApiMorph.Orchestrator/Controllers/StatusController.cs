using ApiMorph.Orchestrator.Infrastructure.Engine;
using Microsoft.AspNetCore.Mvc;

namespace ApiMorph.Orchestrator.Controllers;

[ApiController]
[Route("api/v1")]
public class StatusController(IEngineClient engineClient, IConfiguration configuration) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        string engineStatus;

        try
        {
            var health = await engineClient.GetHealthAsync(cancellationToken);
            engineStatus = health.Status.Equals("ok", StringComparison.OrdinalIgnoreCase) ? "ok" : "degraded";
        }
        catch (Exception ex)
        {
            engineStatus = "unreachable";
            return Ok(new
            {
                service = "apimorph-orchestrator",
                version = typeof(StatusController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                engine = new { status = engineStatus, error = ex.Message },
                configuration = new
                {
                    llmEnabled = configuration.GetValue("Llm:Enabled", false),
                    autoMerge = configuration.GetValue("GitHub:AutoMerge", false)
                }
            });
        }

        return Ok(new
        {
            service = "apimorph-orchestrator",
            version = typeof(StatusController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            engine = new { status = engineStatus },
            configuration = new
            {
                llmEnabled = configuration.GetValue("Llm:Enabled", false),
                autoMerge = configuration.GetValue("GitHub:AutoMerge", false)
            }
        });
    }
}
