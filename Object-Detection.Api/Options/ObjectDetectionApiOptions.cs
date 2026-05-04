namespace Object_Detection.Api.Options;

public sealed class ObjectDetectionApiOptions
{
    public const string SectionName = "ObjectDetection";

    public string ModelFilePath { get; set; } = "model/object-detection-model.json";

    public long MaxUploadBytes { get; set; } = 5 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".bmp", ".webp"];
}
