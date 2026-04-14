using Object_Detection_ASP.NETMVC.Models.Api;

namespace Object_Detection_ASP.NETMVC.Services
{
    public interface IObjectDetectionApiClient
    {
        Task<HealthResponseDto> GetHealthAsync(CancellationToken cancellationToken = default);

        Task<ModelInfoResponseDto> GetModelInfoAsync(CancellationToken cancellationToken = default);

        Task<DetectionResponseDto> DetectAsync(
            byte[] imageBytes,
            string fileName,
            string? contentType,
            CancellationToken cancellationToken = default);
    }
}
