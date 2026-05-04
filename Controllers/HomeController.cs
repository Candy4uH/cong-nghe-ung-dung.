using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Object_Detection_ASP.NETMVC.Models.Api;
using Object_Detection_ASP.NETMVC.Models;
using Object_Detection_ASP.NETMVC.Models.ViewModels;
using Object_Detection_ASP.NETMVC.Services;

namespace Object_Detection_ASP.NETMVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly IObjectDetectionApiClient _apiClient;
        private readonly IFruitInfoApiClient _fruitInfoApiClient;
        private readonly FruitInfoApiOptions _fruitInfoApiOptions;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IObjectDetectionApiClient apiClient,
            IFruitInfoApiClient fruitInfoApiClient,
            IOptions<FruitInfoApiOptions> fruitInfoApiOptions,
            ILogger<HomeController> logger)
        {
            _apiClient = apiClient;
            _fruitInfoApiClient = fruitInfoApiClient;
            _fruitInfoApiOptions = fruitInfoApiOptions.Value;
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
                viewModel.HealthTimestamp = healthResponse.ModelLoadedAtUtc;
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
                viewModel.ModelVersion = modelInfoResponse.ModelVersion;
                viewModel.ModelFramework = "Custom object detection runtime";
                viewModel.ModelDescription = BuildModelDescription(modelInfoResponse);
                viewModel.Labels = modelInfoResponse.Labels ?? [];
                viewModel.AdditionalModelInfo = ToDisplayDictionary(
                    modelInfoResponse.AdditionalData,
                    "modelName",
                    "modelVersion",
                    "templatesByLabel",
                    "scoreThreshold",
                    "scales",
                    "thresholdByLabel",
                    "labels");

                if (modelInfoResponse.TemplatesByLabel?.Count > 0)
                {
                    viewModel.AdditionalModelInfo["templatesByLabel"] = string.Join(
                        ", ",
                        modelInfoResponse.TemplatesByLabel.Select(item => $"{item.Key}:{item.Value}"));
                }

                if (modelInfoResponse.ScoreThreshold.HasValue)
                {
                    viewModel.AdditionalModelInfo["scoreThreshold"] = modelInfoResponse.ScoreThreshold.Value.ToString("0.##");
                }

                if (modelInfoResponse.Scales?.Count > 0)
                {
                    viewModel.AdditionalModelInfo["scales"] = string.Join(", ", modelInfoResponse.Scales.Select(item => item.ToString("0.##")));
                }

                if (modelInfoResponse.ThresholdByLabel?.Count > 0)
                {
                    viewModel.AdditionalModelInfo["thresholdByLabel"] = string.Join(
                        ", ",
                        modelInfoResponse.ThresholdByLabel.Select(item => $"{item.Key}:{item.Value:0.##}"));
                }
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

            try
            {
                var fruitNames = _fruitInfoApiOptions.FeaturedFruits
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList();

                foreach (var fruitName in fruitNames)
                {
                    var fruitResponse = await _fruitInfoApiClient.GetFruitAsync(fruitName, cancellationToken);
                    viewModel.FeaturedFruits.Add(new FruitInfoCardViewModel
                    {
                        Name = fruitResponse.Name,
                        Family = fruitResponse.Family,
                        Order = fruitResponse.Order,
                        Genus = fruitResponse.Genus,
                        Calories = fruitResponse.Nutritions?.Calories ?? 0,
                        Fat = fruitResponse.Nutritions?.Fat ?? 0,
                        Sugar = fruitResponse.Nutritions?.Sugar ?? 0,
                        Carbohydrates = fruitResponse.Nutritions?.Carbohydrates ?? 0,
                        Protein = fruitResponse.Nutritions?.Protein ?? 0
                    });
                }
            }
            catch (ExternalFruitApiException exception)
            {
                _logger.LogWarning(exception, "External fruit API call failed.");
                viewModel.ExternalFruitApiErrorMessage = BuildFriendlyExternalFruitApiErrorMessage(exception);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while loading external fruit API.");
                viewModel.ExternalFruitApiErrorMessage = "Unexpected error happened while loading third-party fruit information.";
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

        private static string BuildModelDescription(ModelInfoResponseDto modelInfoResponse)
        {
            var labelCount = modelInfoResponse.Labels?.Count ?? 0;
            var templateCount = modelInfoResponse.TemplatesByLabel?.Values.Sum() ?? 0;

            return $"Labels: {labelCount}, templates: {templateCount}.";
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

        private static string BuildFriendlyExternalFruitApiErrorMessage(ExternalFruitApiException exception)
        {
            return exception.StatusCode switch
            {
                HttpStatusCode.NotFound => "The third-party fruit API did not recognize one of the configured fruit names.",
                HttpStatusCode.TooManyRequests => "The third-party fruit API rate limit was reached. Please try again later.",
                HttpStatusCode.ServiceUnavailable => "The third-party fruit API is temporarily unavailable.",
                HttpStatusCode.InternalServerError => "The third-party fruit API encountered an internal error.",
                _ => "Unable to load fruit nutrition data from the external API right now."
            };
        }
    }
}
