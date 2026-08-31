using ApiMorph.Orchestrator.Application.Contracts;
using ApiMorph.Orchestrator.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiMorph.Orchestrator.Controllers;

[ApiController]
[Route("api/v1/scans")]
public class ScanController(IScanService scanService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ScanJobResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateScan([FromBody] CreateScanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await scanService.CreateAndRunAsync(request, cancellationToken);
            var enriched = result with { Links = BuildLinks(result.Id) };
            return CreatedAtAction(nameof(GetScan), new { scanJobId = result.Id }, enriched);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpGet("{scanJobId:guid}")]
    [ProducesResponseType(typeof(ScanJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScan(Guid scanJobId, CancellationToken cancellationToken)
    {
        var result = await scanService.GetAsync(scanJobId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result with { Links = BuildLinks(scanJobId) });
    }

    [HttpGet("{scanJobId:guid}/findings")]
    [ProducesResponseType(typeof(IReadOnlyList<FindingSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFindings(Guid scanJobId, CancellationToken cancellationToken)
    {
        var findings = await scanService.GetFindingsAsync(scanJobId, cancellationToken);
        return findings is null ? NotFound() : Ok(findings);
    }

    [HttpGet("{scanJobId:guid}/report")]
    [HttpGet("{scanJobId:guid}/report.md")]
    [ProducesResponseType(typeof(ScanReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json", "text/markdown")]
    public async Task<IActionResult> GetReport(
        Guid scanJobId,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var result = await scanService.GetReportAsync(scanJobId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        if (WantsMarkdown(format))
        {
            return Content(result.Content, "text/markdown; charset=utf-8");
        }

        return Ok(result);
    }

    private static bool WantsMarkdown(string? format) =>
        string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase)
        || string.Equals(format, "md", StringComparison.OrdinalIgnoreCase);

    private ScanJobLinks BuildLinks(Guid scanJobId)
    {
        var self = Url.Link(nameof(GetScan), new { scanJobId })
            ?? $"/api/v1/scans/{scanJobId}";
        var reportBase = Url.Link(nameof(GetReport), new { scanJobId })
            ?? $"/api/v1/scans/{scanJobId}/report";
        var findings = Url.Link(nameof(GetFindings), new { scanJobId })
            ?? $"/api/v1/scans/{scanJobId}/findings";

        return new ScanJobLinks
        {
            Self = self,
            ReportMarkdown = $"{reportBase}?format=markdown",
            ReportJson = reportBase,
            Findings = findings,
        };
    }
}
