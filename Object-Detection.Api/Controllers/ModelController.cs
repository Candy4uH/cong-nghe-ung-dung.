using Microsoft.AspNetCore.Mvc;
using Object_Detection.Api.Contracts.Responses;
using Object_Detection.Api.Services;

namespace Object_Detection.Api.Controllers;

[ApiController]
[Route("api/v1/model")]
public sealed class ModelController : ControllerBase
{
    private readonly IObjectDetectionService _detectionService;

    public ModelController(IObjectDetectionService detectionService)
    {
        _detectionService = detectionService;
    }

    [HttpGet("info")]
    [ProducesResponseType(typeof(ModelInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ModelInfoResponse>> GetModelInfo(CancellationToken cancellationToken)
    {
        var modelInfo = await _detectionService.GetModelInfoAsync(cancellationToken);

        return Ok(new ModelInfoResponse(
            ModelName: modelInfo.ModelName,
            ModelVersion: modelInfo.ModelVersion,
            Labels: modelInfo.Labels,
            TemplatesByLabel: modelInfo.TemplatesByLabel,
            ScoreThreshold: modelInfo.ScoreThreshold,
            Scales: modelInfo.Scales,
            ThresholdByLabel: modelInfo.ThresholdByLabel));
    }
}
