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
            return CreatedAtAction(nameof(GetScan), new { scanJobId = result.Id }, result);
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
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{scanJobId:guid}/report")]
    [ProducesResponseType(typeof(ScanReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReport(Guid scanJobId, CancellationToken cancellationToken)
    {
        var result = await scanService.GetReportAsync(scanJobId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
