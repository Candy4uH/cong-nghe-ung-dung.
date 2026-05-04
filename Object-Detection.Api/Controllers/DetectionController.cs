using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Object_Detection.Api.Contracts.Requests;
using Object_Detection.Api.Contracts.Responses;
using Object_Detection.Api.Infrastructure.ErrorHandling;
using Object_Detection.Api.Options;
using Object_Detection.Api.Services;

namespace Object_Detection.Api.Controllers;

[ApiController]
[Route("api/v1/detection")]
public sealed class DetectionController : ControllerBase
{
    private readonly IObjectDetectionService _detectionService;
    private readonly ObjectDetectionApiOptions _options;

    public DetectionController(
        IObjectDetectionService detectionService,
        IOptions<ObjectDetectionApiOptions> options)
    {
        _detectionService = detectionService;
        _options = options.Value;
    }

    [HttpPost("detect")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DetectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DetectionResponse>> Detect([FromForm] DetectImageRequest request, CancellationToken cancellationToken)
    {
        var image = request.Image;
        ValidateImageInput(image);

        await using var stream = image!.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        var inference = await _detectionService.DetectAsync(memory.ToArray(), cancellationToken);

        var response = new DetectionResponse(
            ImageWidth: inference.ImageWidth,
            ImageHeight: inference.ImageHeight,
            ProcessingTimeMs: inference.ProcessingTimeMs,
            Detections: inference.Detections
                .Select(x => new DetectionItemResponse(
                    Label: x.Label,
                    Confidence: x.Confidence,
                    BoundingBox: new DetectionBoundingBoxResponse(
                        X1: x.BoundingBox.X1,
                        Y1: x.BoundingBox.Y1,
                        X2: x.BoundingBox.X2,
                        Y2: x.BoundingBox.Y2)))
                .ToList());

        return Ok(response);
    }

    private void ValidateImageInput(IFormFile? image)
    {
        if (image is null)
        {
            throw new ApiException("INVALID_INPUT", "Image is required.", StatusCodes.Status400BadRequest);
        }

        if (image.Length == 0)
        {
            throw new ApiException("INVALID_INPUT", "Image file is empty.", StatusCodes.Status400BadRequest);
        }

        if (image.Length > _options.MaxUploadBytes)
        {
            throw new ApiException(
                "INVALID_INPUT",
                $"Image exceeds max allowed size of {_options.MaxUploadBytes} bytes.",
                StatusCodes.Status400BadRequest);
        }

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (!_options.AllowedExtensions.Any(x => string.Equals(x, extension, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ApiException(
                "INVALID_INPUT",
                $"Unsupported image extension '{extension}'.",
                StatusCodes.Status400BadRequest);
        }
    }
}
