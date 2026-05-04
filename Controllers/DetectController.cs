using System.Net;
using Object_Detection_ASP.NETMVC.Models.Api;
using Object_Detection_ASP.NETMVC.Models.ViewModels;
using Object_Detection_ASP.NETMVC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Object_Detection_ASP.NETMVC.Controllers
{
    [Route("Detect")]
    public class DetectController : Controller
    {
        private readonly IObjectDetectionApiClient _apiClient;
        private readonly ObjectDetectionApiOptions _apiOptions;
        private readonly ILogger<DetectController> _logger;

        public DetectController(
            IObjectDetectionApiClient apiClient,
            IOptions<ObjectDetectionApiOptions> apiOptions,
            ILogger<DetectController> logger)
        {
            _apiClient = apiClient;
            _apiOptions = apiOptions.Value;
            _logger = logger;
        }

        [HttpGet("Upload")]
        public IActionResult Upload()
        {
            return View(CreateUploadViewModel());
        }

        [HttpPost("Upload")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(DetectUploadViewModel viewModel, CancellationToken cancellationToken)
        {
            viewModel.MaxUploadBytes = _apiOptions.MaxUploadBytes;
            viewModel.AllowedExtensionsDisplay = string.Join(", ", GetAllowedExtensions());

            if (viewModel.Image is null || viewModel.Image.Length == 0)
            {
                ModelState.AddModelError(nameof(viewModel.Image), "Please select an image file.");
                return View(viewModel);
            }

            var extension = Path.GetExtension(viewModel.Image.FileName);
            if (string.IsNullOrWhiteSpace(extension)
                || !GetAllowedExtensions().Any(item =>
                    string.Equals(item, extension, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(
                    nameof(viewModel.Image),
                    $"Invalid file type. Allowed extensions: {viewModel.AllowedExtensionsDisplay}");
                return View(viewModel);
            }

            if (viewModel.Image.Length > _apiOptions.MaxUploadBytes)
            {
                ModelState.AddModelError(
                    nameof(viewModel.Image),
                    $"File is too large. Max allowed size is {FormatFileSize(_apiOptions.MaxUploadBytes)}.");
                return View(viewModel);
            }

            byte[] imageBytes;
            await using (var memoryStream = new MemoryStream())
            {
                await viewModel.Image.CopyToAsync(memoryStream, cancellationToken);
                imageBytes = memoryStream.ToArray();
            }

            try
            {
                var detectionResponse = await _apiClient.DetectAsync(
                    imageBytes,
                    viewModel.Image.FileName,
                    viewModel.Image.ContentType,
                    cancellationToken);

                var resultViewModel = MapToResultViewModel(
                    viewModel.Image.FileName,
                    viewModel.Image.ContentType,
                    imageBytes,
                    detectionResponse);

                return View("Result", resultViewModel);
            }
            catch (ObjectDetectionApiException exception)
            {
                _logger.LogWarning(exception, "Detect endpoint failed.");
                ModelState.AddModelError(string.Empty, BuildFriendlyErrorMessage(exception));
                return View(viewModel);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while calling detect endpoint.");
                ModelState.AddModelError(
                    string.Empty,
                    "Unexpected error happened while processing your image. Please try again.");
                return View(viewModel);
            }
        }

        [HttpGet("Result")]
        public IActionResult Result()
        {
            return RedirectToAction(nameof(Upload));
        }

        private DetectUploadViewModel CreateUploadViewModel()
        {
            return new DetectUploadViewModel
            {
                MaxUploadBytes = _apiOptions.MaxUploadBytes,
                AllowedExtensionsDisplay = string.Join(", ", GetAllowedExtensions())
            };
        }

        private IReadOnlyList<string> GetAllowedExtensions()
        {
            return _apiOptions.AllowedExtensions
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string FormatFileSize(long bytes)
        {
            var megaBytes = bytes / 1024d / 1024d;
            return $"{megaBytes:0.##} MB";
        }

        private static string BuildFriendlyErrorMessage(ObjectDetectionApiException exception)
        {
            if (!string.IsNullOrWhiteSpace(exception.ResponseContent))
            {
                if (exception.ResponseContent.Contains("MODEL_NOT_READY", StringComparison.OrdinalIgnoreCase))
                {
                    return "Model is not ready on backend API. Please start the API correctly and verify the model file can be loaded.";
                }

                if (exception.ResponseContent.Contains("INVALID_INPUT", StringComparison.OrdinalIgnoreCase))
                {
                    return "The backend rejected the uploaded file as invalid. Please verify image format and size.";
                }
            }

            return exception.StatusCode switch
            {
                HttpStatusCode.BadRequest => "The backend rejected this file. Please verify the image and try again.",
                HttpStatusCode.NotFound => "Detection endpoint was not found on backend API. Please verify ObjectDetectionApi:BaseUrl and API route.",
                HttpStatusCode.ServiceUnavailable => "Object Detection API is temporarily unavailable. Please retry in a moment.",
                HttpStatusCode.InternalServerError => "Object Detection API encountered an internal error. Please retry later.",
                _ when exception.InnerException is not null => "Unable to reach Object Detection API. Please make sure the backend is running on the configured port.",
                _ => "Unable to process your image right now. Please try again."
            };
        }

        private static DetectResultViewModel MapToResultViewModel(
            string fileName,
            string? contentType,
            byte[] imageBytes,
            DetectionResponseDto detectionResponse)
        {
            var resolvedContentType = string.IsNullOrWhiteSpace(contentType)
                ? "image/png"
                : contentType;

            return new DetectResultViewModel
            {
                FileName = fileName,
                ImageDataUrl = $"data:{resolvedContentType};base64,{Convert.ToBase64String(imageBytes)}",
                ImageWidth = detectionResponse.ImageWidth,
                ImageHeight = detectionResponse.ImageHeight,
                ProcessingTimeMs = detectionResponse.ProcessingTimeMs,
                Detections = detectionResponse.Detections.Select(item => new DetectionItemViewModel
                {
                    Label = item.Label,
                    Confidence = item.Confidence,
                    BoundingBox = new BoundingBoxViewModel
                    {
                        X1 = item.BoundingBox.X1,
                        Y1 = item.BoundingBox.Y1,
                        X2 = item.BoundingBox.X2,
                        Y2 = item.BoundingBox.Y2
                    }
                }).ToList()
            };
        }
    }
}
