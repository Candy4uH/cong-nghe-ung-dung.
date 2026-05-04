using System.Net.Http.Json;
using Object_Detection_ASP.NETMVC.Models.Api;

namespace Object_Detection_ASP.NETMVC.Services
{
    public class FruitInfoApiClient : IFruitInfoApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FruitInfoApiClient> _logger;

        public FruitInfoApiClient(HttpClient httpClient, ILogger<FruitInfoApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<FruitInfoResponseDto> GetFruitAsync(string fruitName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fruitName))
            {
                throw new ArgumentException("Fruit name is required.", nameof(fruitName));
            }

            HttpResponseMessage response;

            try
            {
                response = await _httpClient.GetAsync($"api/fruit/{Uri.EscapeDataString(fruitName)}", cancellationToken);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ExternalFruitApiException("External fruit API request timed out.", innerException: exception);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "External fruit API call failed before receiving a response.");
                throw new ExternalFruitApiException("Unable to reach external fruit API.", innerException: exception);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning(
                        "External fruit API returned an error. StatusCode: {StatusCode}, Response: {Response}",
                        (int)response.StatusCode,
                        errorBody);

                    throw new ExternalFruitApiException(
                        "External fruit API returned an unsuccessful status code.",
                        response.StatusCode,
                        errorBody);
                }

                try
                {
                    var payload = await response.Content.ReadFromJsonAsync<FruitInfoResponseDto>(cancellationToken);
                    return payload ?? throw new ExternalFruitApiException("External fruit API returned an empty response.");
                }
                catch (Exception exception) when (exception is not ExternalFruitApiException)
                {
                    _logger.LogError(exception, "External fruit API returned an invalid payload.");
                    throw new ExternalFruitApiException("External fruit API returned an invalid response.", innerException: exception);
                }
            }
        }
    }
}
