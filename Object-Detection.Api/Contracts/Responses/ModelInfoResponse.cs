namespace Object_Detection.Api.Contracts.Responses;

public sealed record ModelInfoResponse(
    string ModelName,
    string ModelVersion,
    IReadOnlyList<string> Labels,
    IReadOnlyDictionary<string, int> TemplatesByLabel,
    double ScoreThreshold,
    IReadOnlyList<double> Scales,
    IReadOnlyDictionary<string, double> ThresholdByLabel);
