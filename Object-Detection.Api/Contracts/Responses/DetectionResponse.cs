namespace Object_Detection.Api.Contracts.Responses;

public sealed record DetectionResponse(
    int ImageWidth,
    int ImageHeight,
    long ProcessingTimeMs,
    IReadOnlyList<DetectionItemResponse> Detections);

public sealed record DetectionItemResponse(
    string Label,
    double Confidence,
    DetectionBoundingBoxResponse BoundingBox);

public sealed record DetectionBoundingBoxResponse(
    int X1,
    int Y1,
    int X2,
    int Y2);
