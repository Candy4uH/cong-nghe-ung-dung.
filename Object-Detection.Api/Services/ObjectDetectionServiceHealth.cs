namespace Object_Detection.Api.Services;

public sealed record ObjectDetectionServiceHealth(
    bool IsHealthy,
    bool ModelLoaded,
    string Message,
    string ModelFilePath,
    DateTimeOffset? ModelLoadedAtUtc);
