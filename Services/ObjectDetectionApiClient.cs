using System.Net.Http.Headers;
using System.Text.Json;
using Object_Detection_ASP.NETMVC.Models.Api;

namespace Object_Detection_ASP.NETMVC.Services
{
    public class ObjectDetectionApiClient : IObjectDetectionApiClient
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly ILogger<ObjectDetectionApiClient> _logger;

        public ObjectDetectionApiClient(HttpClient httpClient, ILogger<ObjectDetectionApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<HealthResponseDto> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/health");
            return await SendAsync<HealthResponseDto>(request, cancellationToken);
        }

        public async Task<ModelInfoResponseDto> GetModelInfoAsync(CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/model/info");
            return await SendAsync<ModelInfoResponseDto>(request, cancellationToken);
        }

        public async Task<DetectionResponseDto> DetectAsync(
            byte[] imageBytes,
            string fileName,
            string? contentType,
            CancellationToken cancellationToken = default)
        {
            var multiPartContent = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }

            multiPartContent.Add(imageContent, "image", fileName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/detection/detect")
            {
                Content = multiPartContent
            };

            return await SendAsync<DetectionResponseDto>(request, cancellationToken);
        }

        private async Task<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;

            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ObjectDetectionApiException("Object Detection API request timed out.", innerException: exception);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Object Detection API call failed before receiving a response.");
                throw new ObjectDetectionApiException("Unable to reach Object Detection API.", innerException: exception);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning(
                        "Object Detection API returned an error. StatusCode: {StatusCode}, Response: {Response}",
                        (int)response.StatusCode,
                        errorBody);

                    throw new ObjectDetectionApiException(
                        "Object Detection API returned an unsuccessful status code.",
                        response.StatusCode,
                        errorBody);
                }

                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                try
                {
                    var payload = await JsonSerializer.DeserializeAsync<TResponse>(
                        responseStream,
                        JsonSerializerOptions,
                        cancellationToken);

                    return payload ?? throw new ObjectDetectionApiException("Object Detection API returned an empty response.");
                }
                catch (JsonException exception)
                {
                    _logger.LogError(exception, "Object Detection API returned invalid JSON.");
                    throw new ObjectDetectionApiException("Object Detection API returned an invalid response.", innerException: exception);
                }
            }
        }
    }
}
