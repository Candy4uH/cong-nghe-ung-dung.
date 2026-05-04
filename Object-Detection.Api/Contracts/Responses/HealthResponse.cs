namespace Object_Detection.Api.Contracts.Responses;

public sealed record HealthResponse(
    string Status,
    bool ModelLoaded,
    string Message,
    string ModelFilePath,
    DateTimeOffset? ModelLoadedAtUtc);
