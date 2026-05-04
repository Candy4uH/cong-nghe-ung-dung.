using Microsoft.AspNetCore.Mvc;
using Object_Detection.Api.Contracts.Responses;
using Object_Detection.Api.Services;

namespace Object_Detection.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IObjectDetectionService _detectionService;

    public HealthController(IObjectDetectionService detectionService)
    {
        _detectionService = detectionService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponse>> GetHealth(CancellationToken cancellationToken)
    {
        var health = await _detectionService.GetHealthAsync(cancellationToken);
        var status = health.IsHealthy ? "healthy" : "degraded";

        return Ok(new HealthResponse(
            Status: status,
            ModelLoaded: health.ModelLoaded,
            Message: health.Message,
            ModelFilePath: health.ModelFilePath,
            ModelLoadedAtUtc: health.ModelLoadedAtUtc));
    }
}
