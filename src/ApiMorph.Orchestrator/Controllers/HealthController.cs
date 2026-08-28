using Microsoft.AspNetCore.Mvc;

namespace ApiMorph.Orchestrator.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult GetHealth() =>
        Ok(new { status = "ok" });
}
