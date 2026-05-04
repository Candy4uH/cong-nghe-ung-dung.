using Object_Detection;

namespace Object_Detection.Api.Services;

public interface IObjectDetectionService
{
    Task<ObjectDetectionServiceHealth> GetHealthAsync(CancellationToken cancellationToken);

    Task<ObjectDetectionModelInfo> GetModelInfoAsync(CancellationToken cancellationToken);

    Task<ObjectDetectionInferenceResult> DetectAsync(byte[] imageBytes, CancellationToken cancellationToken);
}
