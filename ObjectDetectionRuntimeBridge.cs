using System.Diagnostics;
using System.Text.Json;
using OpenCvSharp;

namespace Object_Detection;

public sealed class ObjectDetectionRuntime
{
    private readonly DetectorModel _model;
    private readonly DetectorConfig _config;

    private ObjectDetectionRuntime(
        DetectorModel model,
        DetectorConfig config,
        string modelFilePath,
        string modelName,
        string modelVersion)
    {
        _model = model;
        _config = config;
        ModelFilePath = modelFilePath;
        ModelName = modelName;
        ModelVersion = modelVersion;
    }

    public string ModelFilePath { get; }

    public string ModelName { get; }

    public string ModelVersion { get; }

    public static bool TryLoadFromFile(string modelFilePath, out ObjectDetectionRuntime? runtime, out string error)
    {
        runtime = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(modelFilePath))
        {
            error = "Model file path is empty.";
            return false;
        }

        if (!File.Exists(modelFilePath))
        {
            error = $"Model file not found: {modelFilePath}";
            return false;
        }

        try
        {
            var json = File.ReadAllText(modelFilePath);
            var snapshot = JsonSerializer.Deserialize<DetectionModelSnapshot>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (snapshot is null)
            {
                error = "Model file content is empty or invalid JSON.";
                return false;
            }

            if (snapshot.Templates.Count == 0)
            {
                error = "Model file has no templates. Inference cannot run.";
                return false;
            }

            var model = BuildDetectorModel(snapshot);
            var config = BuildDetectorConfig(snapshot.RuntimeSettings);

            runtime = new ObjectDetectionRuntime(
                model,
                config,
                Path.GetFullPath(modelFilePath),
                snapshot.ModelName,
                snapshot.ModelVersion);

            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to load model file: {ex.Message}";
            return false;
        }
    }

    public ObjectDetectionModelInfo GetModelInfo()
    {
        var templatesByLabel = _model.Templates
            .GroupBy(x => x.Label)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var labels = templatesByLabel.Keys.OrderBy(x => x).ToArray();
        var thresholds = _model.ThresholdByLabel
            .OrderBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        return new ObjectDetectionModelInfo(
            ModelName,
            ModelVersion,
            labels,
            templatesByLabel,
            _config.ScoreThreshold,
            _config.Scales,
            thresholds);
    }

    public ObjectDetectionInferenceResult Detect(byte[] imageBytes)
    {
        if (imageBytes.Length == 0)
        {
            throw new InvalidOperationException("Image content is empty.");
        }

        using var image = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (image.Empty())
        {
            throw new InvalidOperationException("Unable to decode image bytes.");
        }

        var sw = Stopwatch.StartNew();
        var detections = BasicHistogramDetector.Detect(image, _model, _config);
        sw.Stop();

        var output = detections
            .Select(det => new ObjectDetectionItem(
                det.Label,
                det.Score,
                new ObjectDetectionBoundingBox(det.X1, det.Y1, det.X2, det.Y2)))
            .ToList();

        return new ObjectDetectionInferenceResult(
            ImageWidth: image.Width,
            ImageHeight: image.Height,
            ProcessingTimeMs: sw.ElapsedMilliseconds,
            Detections: output);
    }

    private static DetectorModel BuildDetectorModel(DetectionModelSnapshot snapshot)
    {
        var templates = snapshot.Templates
            .Select(x => new TemplateFeature(x.Label, x.Histogram, x.Width, x.Height))
            .ToList();

        var avgSizeByLabel = snapshot.AvgSizeByLabel
            .ToDictionary(
                x => x.Label,
                x => (W: x.Width, H: x.Height),
                StringComparer.OrdinalIgnoreCase);

        var positivePrototypes = snapshot.PrototypeByLabel
            .ToDictionary(
                x => x.Label,
                x => x.Histogram,
                StringComparer.OrdinalIgnoreCase);

        var negativePrototypes = snapshot.NegativePrototypeByLabel
            .ToDictionary(
                x => x.Label,
                x => x.Histogram,
                StringComparer.OrdinalIgnoreCase);

        var thresholds = snapshot.ThresholdByLabel
            .ToDictionary(
                x => x.Label,
                x => x.Threshold,
                StringComparer.OrdinalIgnoreCase);

        return new DetectorModel(templates, avgSizeByLabel, positivePrototypes, negativePrototypes, thresholds);
    }

    private static DetectorConfig BuildDetectorConfig(DetectionRuntimeSettings settings)
    {
        var scales = settings.Scales.Where(x => x > 0.1).Distinct().OrderBy(x => x).ToArray();
        if (scales.Length == 0)
        {
            scales = [0.8, 1.0, 1.2];
        }

        return new DetectorConfig(
            TrainDir: Directory.GetCurrentDirectory(),
            TestDir: Directory.GetCurrentDirectory(),
            PredictImagePath: null,
            ExportModelPath: null,
            Epochs: Math.Max(1, settings.Epochs),
            MaxTemplatesPerClass: Math.Max(1, settings.MaxTemplatesPerClass),
            MaxTrainImages: Math.Max(1, settings.MaxTrainImages),
            Scales: scales,
            StrideRatio: Math.Clamp(settings.StrideRatio, 0.05, 1.0),
            ScoreThreshold: Math.Clamp(settings.ScoreThreshold, 0.0, 1.0),
            NmsIouThreshold: Math.Clamp(settings.NmsIouThreshold, 0.01, 1.0),
            SimilarityTopK: Math.Max(1, settings.SimilarityTopK),
            HardNegativeSamplesPerImage: Math.Max(0, settings.HardNegativeSamplesPerImage),
            MaxHardNegativesPerClass: Math.Max(0, settings.MaxHardNegativesPerClass),
            HardNegativeWeight: Math.Clamp(settings.HardNegativeWeight, 0.0, 0.95),
            EnableThresholdCalibration: settings.EnableThresholdCalibration,
            MaxDetectionsPerImage: Math.Clamp(settings.MaxDetectionsPerImage, 1, 100));
    }
}

public sealed record ObjectDetectionModelInfo(
    string ModelName,
    string ModelVersion,
    IReadOnlyList<string> Labels,
    IReadOnlyDictionary<string, int> TemplatesByLabel,
    double ScoreThreshold,
    IReadOnlyList<double> Scales,
    IReadOnlyDictionary<string, double> ThresholdByLabel);

public sealed record ObjectDetectionInferenceResult(
    int ImageWidth,
    int ImageHeight,
    long ProcessingTimeMs,
    IReadOnlyList<ObjectDetectionItem> Detections);

public sealed record ObjectDetectionItem(
    string Label,
    double Confidence,
    ObjectDetectionBoundingBox BoundingBox);

public sealed record ObjectDetectionBoundingBox(
    int X1,
    int Y1,
    int X2,
    int Y2);

public sealed record DetectionModelSnapshot(
    string ModelName,
    string ModelVersion,
    DetectionRuntimeSettings RuntimeSettings,
    IReadOnlyList<TemplateFeatureSnapshot> Templates,
    IReadOnlyList<LabelSizeSnapshot> AvgSizeByLabel,
    IReadOnlyList<LabelHistogramSnapshot> PrototypeByLabel,
    IReadOnlyList<LabelHistogramSnapshot> NegativePrototypeByLabel,
    IReadOnlyList<LabelThresholdSnapshot> ThresholdByLabel);

public sealed record DetectionRuntimeSettings(
    int Epochs,
    int MaxTemplatesPerClass,
    int MaxTrainImages,
    double[] Scales,
    double StrideRatio,
    double ScoreThreshold,
    double NmsIouThreshold,
    int SimilarityTopK,
    int HardNegativeSamplesPerImage,
    int MaxHardNegativesPerClass,
    double HardNegativeWeight,
    bool EnableThresholdCalibration,
    int MaxDetectionsPerImage)
{
    public DetectionRuntimeSettings()
        : this(
            Epochs: 1,
            MaxTemplatesPerClass: 100,
            MaxTrainImages: 1,
            Scales: [0.8, 1.0, 1.2],
            StrideRatio: 0.2,
            ScoreThreshold: 0.7,
            NmsIouThreshold: 0.35,
            SimilarityTopK: 5,
            HardNegativeSamplesPerImage: 10,
            MaxHardNegativesPerClass: 200,
            HardNegativeWeight: 0.35,
            EnableThresholdCalibration: true,
            MaxDetectionsPerImage: 30)
    {
    }
}

public sealed record TemplateFeatureSnapshot(
    string Label,
    float[] Histogram,
    double Width,
    double Height);

public sealed record LabelSizeSnapshot(
    string Label,
    double Width,
    double Height);

public sealed record LabelHistogramSnapshot(
    string Label,
    float[] Histogram);

public sealed record LabelThresholdSnapshot(
    string Label,
    double Threshold);
