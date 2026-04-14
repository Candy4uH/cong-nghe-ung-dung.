using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Object_Detection_ASP.NETMVC.Models.Api;
using Object_Detection_ASP.NETMVC.Models;
using Object_Detection_ASP.NETMVC.Models.ViewModels;
using Object_Detection_ASP.NETMVC.Services;

namespace Object_Detection_ASP.NETMVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly IObjectDetectionApiClient _apiClient;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IObjectDetectionApiClient apiClient, ILogger<HomeController> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var viewModel = new HomeIndexViewModel();

            try
            {
                var healthResponse = await _apiClient.GetHealthAsync(cancellationToken);
                viewModel.HealthStatus = healthResponse.Status;
                viewModel.HealthMessage = healthResponse.Message;
                viewModel.HealthTimestamp = healthResponse.Timestamp;
            }
            catch (ObjectDetectionApiException exception)
            {
                _logger.LogWarning(exception, "Health endpoint call failed.");
                viewModel.HealthErrorMessage = BuildFriendlyErrorMessage(
                    exception,
                    "Unable to load API health information right now.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while loading health endpoint.");
                viewModel.HealthErrorMessage = "Unexpected error happened while checking API health.";
            }

            try
            {
                var modelInfoResponse = await _apiClient.GetModelInfoAsync(cancellationToken);
                viewModel.ModelName = modelInfoResponse.ModelName;
                viewModel.ModelVersion = modelInfoResponse.Version;
                viewModel.ModelFramework = modelInfoResponse.Framework;
                viewModel.ModelDescription = modelInfoResponse.Description;
                viewModel.Labels = modelInfoResponse.Labels ?? [];
                viewModel.AdditionalModelInfo = ToDisplayDictionary(
                    modelInfoResponse.AdditionalData,
                    "modelName",
                    "version",
                    "framework",
                    "description",
                    "labels");
            }
            catch (ObjectDetectionApiException exception)
            {
                _logger.LogWarning(exception, "Model info endpoint call failed.");
                viewModel.ModelInfoErrorMessage = BuildFriendlyErrorMessage(
                    exception,
                    "Unable to load model information right now.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while loading model info endpoint.");
                viewModel.ModelInfoErrorMessage = "Unexpected error happened while loading model information.";
            }

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private static string BuildFriendlyErrorMessage(ObjectDetectionApiException exception, string fallbackMessage)
        {
            return exception.StatusCode switch
            {
                HttpStatusCode.BadRequest => "The API rejected this request due to invalid input.",
                HttpStatusCode.NotFound => "API endpoint was not found. Please verify ObjectDetectionApi:BaseUrl and endpoint paths.",
                HttpStatusCode.ServiceUnavailable => "Object Detection API is temporarily unavailable.",
                HttpStatusCode.InternalServerError => "Object Detection API encountered an internal error.",
                _ => fallbackMessage
            };
        }

        private static Dictionary<string, string> ToDisplayDictionary(
            Dictionary<string, JsonElement>? source,
            params string[] ignoredKeys)
        {
            if (source is null || source.Count == 0)
            {
                return new Dictionary<string, string>();
            }

            var ignored = new HashSet<string>(ignoredKeys, StringComparer.OrdinalIgnoreCase);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (key, value) in source)
            {
                if (ignored.Contains(key))
                {
                    continue;
                }

                result[key] = value.ToString();
            }

            return result;
        }
    }
}
