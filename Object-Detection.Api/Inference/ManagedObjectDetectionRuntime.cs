using System.Diagnostics;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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

        ManagedImage image;
        try
        {
            image = ManagedImage.Load(imageBytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unable to decode image bytes: {ex.Message}", ex);
        }

        using (image)
        {
            var sw = Stopwatch.StartNew();
            var detections = ManagedHistogramDetector.Detect(image, _model, _config);
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
            Epochs: Math.Max(1, settings.Epochs),
            Scales: scales,
            StrideRatio: Math.Clamp(settings.StrideRatio, 0.05, 1.0),
            ScoreThreshold: Math.Clamp(settings.ScoreThreshold, 0.0, 1.0),
            NmsIouThreshold: Math.Clamp(settings.NmsIouThreshold, 0.01, 1.0),
            SimilarityTopK: Math.Max(1, settings.SimilarityTopK),
            HardNegativeWeight: Math.Clamp(settings.HardNegativeWeight, 0.0, 0.95),
            MaxDetectionsPerImage: Math.Clamp(settings.MaxDetectionsPerImage, 1, 100));
    }
}

internal sealed record Detection(string Label, int X1, int Y1, int X2, int Y2, double Score);

internal sealed record TemplateFeature(string Label, float[] Histogram, double Width, double Height);

internal sealed record DetectorModel(
    IReadOnlyList<TemplateFeature> Templates,
    IReadOnlyDictionary<string, (double W, double H)> AvgSizeByLabel,
    IReadOnlyDictionary<string, float[]> PrototypeByLabel,
    IReadOnlyDictionary<string, float[]> NegativePrototypeByLabel,
    IReadOnlyDictionary<string, double> ThresholdByLabel);

internal sealed record DetectorConfig(
    int Epochs,
    double[] Scales,
    double StrideRatio,
    double ScoreThreshold,
    double NmsIouThreshold,
    int SimilarityTopK,
    double HardNegativeWeight,
    int MaxDetectionsPerImage);

internal static class ManagedHistogramDetector
{
    private const double PrototypeWeight = 0.35;
    private const double SoftNmsSigma = 0.5;
    private const double SoftNmsMinScore = 0.15;

    public static List<Detection> Detect(ManagedImage image, DetectorModel model, DetectorConfig config)
    {
        var output = new List<Detection>();
        if (model.Templates.Count == 0)
        {
            return output;
        }

        var templatesByLabel = model.Templates
            .GroupBy(t => t.Label)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var (label, templates) in templatesByLabel)
        {
            if (!model.AvgSizeByLabel.TryGetValue(label, out var avgSize))
            {
                continue;
            }

            var finalThreshold = ResolveThreshold(config, label, model.ThresholdByLabel);

            foreach (var scale in config.Scales)
            {
                var winW = Math.Max(8, (int)Math.Round(avgSize.W * scale));
                var winH = Math.Max(8, (int)Math.Round(avgSize.H * scale));
                if (winW >= image.Width || winH >= image.Height)
                {
                    continue;
                }

                var stride = Math.Max(4, (int)Math.Round(Math.Min(winW, winH) * config.StrideRatio));

                for (var y = 0; y <= image.Height - winH; y += stride)
                {
                    for (var x = 0; x <= image.Width - winW; x += stride)
                    {
                        var hist = image.ExtractHistogram(x, y, winW, winH);
                        var score = CombinedSimilarity(
                            hist,
                            label,
                            templates,
                            model.PrototypeByLabel,
                            model.NegativePrototypeByLabel,
                            config);

                        if (score >= finalThreshold)
                        {
                            output.Add(new Detection(label, x, y, x + winW - 1, y + winH - 1, score));
                        }
                    }
                }
            }
        }

        return SoftNonMaximumSuppression(output, config.NmsIouThreshold)
            .OrderByDescending(d => d.Score)
            .Take(config.MaxDetectionsPerImage)
            .ToList();
    }

    private static double CombinedSimilarity(
        float[] hist,
        string label,
        List<TemplateFeature> templates,
        IReadOnlyDictionary<string, float[]> prototypeByLabel,
        IReadOnlyDictionary<string, float[]> negativePrototypeByLabel,
        DetectorConfig config)
    {
        var positiveScore = PositiveSimilarity(hist, label, templates, prototypeByLabel, config.SimilarityTopK);

        if (!negativePrototypeByLabel.TryGetValue(label, out var negativePrototype))
        {
            return positiveScore;
        }

        var negativeScore = CosineSimilarity(hist, negativePrototype);
        var fused = (1.0 - config.HardNegativeWeight) * positiveScore + config.HardNegativeWeight * (1.0 - negativeScore);
        return Math.Clamp(fused, 0.0, 1.0);
    }

    private static double PositiveSimilarity(
        float[] hist,
        string label,
        IReadOnlyList<TemplateFeature> templates,
        IReadOnlyDictionary<string, float[]> prototypeByLabel,
        int topK)
    {
        if (templates.Count == 0)
        {
            return 0.0;
        }

        var topScores = templates
            .Select(template => CosineSimilarity(hist, template.Histogram))
            .OrderByDescending(score => score)
            .Take(Math.Max(1, topK))
            .ToArray();

        double weightedSum = 0;
        double weightedNorm = 0;
        for (var i = 0; i < topScores.Length; i++)
        {
            var weight = 1.0 / (i + 1);
            weightedSum += topScores[i] * weight;
            weightedNorm += weight;
        }

        var topKScore = weightedNorm <= 1e-12 ? 0.0 : weightedSum / weightedNorm;
        if (!prototypeByLabel.TryGetValue(label, out var prototype))
        {
            return topKScore;
        }

        var prototypeScore = CosineSimilarity(hist, prototype);
        return (1.0 - PrototypeWeight) * topKScore + PrototypeWeight * prototypeScore;
    }

    private static double ResolveThreshold(
        DetectorConfig config,
        string label,
        IReadOnlyDictionary<string, double> thresholdByLabel)
    {
        if (thresholdByLabel.TryGetValue(label, out var threshold))
        {
            return threshold;
        }

        var adaptiveThreshold = config.ScoreThreshold - 0.02 * (config.Epochs - 1);
        return Math.Clamp(adaptiveThreshold, 0.35, 1.0);
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0;
        double normA = 0;
        double normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA <= 1e-12 || normB <= 1e-12)
        {
            return 0;
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private static List<Detection> SoftNonMaximumSuppression(List<Detection> detections, double iouThreshold)
    {
        var result = new List<Detection>();
        var byLabel = detections
            .GroupBy(d => d.Label)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Score).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var (_, group) in byLabel)
        {
            var candidates = new List<Detection>(group);
            while (candidates.Count > 0)
            {
                candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
                var best = candidates[0];
                candidates.RemoveAt(0);

                if (best.Score < SoftNmsMinScore)
                {
                    continue;
                }

                result.Add(best);

                for (var i = candidates.Count - 1; i >= 0; i--)
                {
                    var candidate = candidates[i];
                    var iou = Geometry.IoU(best, candidate);
                    if (iou < iouThreshold)
                    {
                        continue;
                    }

                    var decay = Math.Exp(-(iou * iou) / SoftNmsSigma);
                    var decayedScore = candidate.Score * decay;
                    if (decayedScore < SoftNmsMinScore)
                    {
                        candidates.RemoveAt(i);
                    }
                    else
                    {
                        candidates[i] = candidate with { Score = decayedScore };
                    }
                }
            }
        }

        return result;
    }
}

internal static class Geometry
{
    public static double IoU(Detection a, Detection b)
        => IoU(a.X1, a.Y1, a.X2, a.Y2, b.X1, b.Y1, b.X2, b.Y2);

    public static double IoU(int ax1, int ay1, int ax2, int ay2, int bx1, int by1, int bx2, int by2)
    {
        var ix1 = Math.Max(ax1, bx1);
        var iy1 = Math.Max(ay1, by1);
        var ix2 = Math.Min(ax2, bx2);
        var iy2 = Math.Min(ay2, by2);

        if (ix2 < ix1 || iy2 < iy1)
        {
            return 0;
        }

        var inter = (ix2 - ix1 + 1.0) * (iy2 - iy1 + 1.0);
        var areaA = (ax2 - ax1 + 1.0) * (ay2 - ay1 + 1.0);
        var areaB = (bx2 - bx1 + 1.0) * (by2 - by1 + 1.0);
        return inter / Math.Max(1e-8, areaA + areaB - inter);
    }
}

internal sealed class ManagedImage : IDisposable
{
    private readonly Rgb24[] _pixels;

    private ManagedImage(int width, int height, Rgb24[] pixels)
    {
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public static ManagedImage Load(byte[] imageBytes)
    {
        using var image = Image.Load<Rgb24>(imageBytes);
        var pixels = new Rgb24[image.Width * image.Height];
        image.CopyPixelDataTo(pixels);
        return new ManagedImage(image.Width, image.Height, pixels);
    }

    public float[] ExtractHistogram(int startX, int startY, int width, int height)
    {
        const int hBins = 16;
        const int sBins = 16;

        var hist = new float[hBins * sBins];
        var total = Math.Max(1, width * height);

        for (var y = startY; y < startY + height; y++)
        {
            var rowOffset = y * Width;
            for (var x = startX; x < startX + width; x++)
            {
                var pixel = _pixels[rowOffset + x];
                var (h, s) = RgbToHsv(pixel.R, pixel.G, pixel.B);
                var hBin = Math.Min(hBins - 1, (int)(h * hBins / 360.0));
                var sBin = Math.Min(sBins - 1, (int)(s * sBins));
                hist[hBin * sBins + sBin]++;
            }
        }

        for (var i = 0; i < hist.Length; i++)
        {
            hist[i] /= total;
        }

        return hist;
    }

    public void Dispose()
    {
    }

    private static (double H, double S) RgbToHsv(byte rByte, byte gByte, byte bByte)
    {
        var r = rByte / 255d;
        var g = gByte / 255d;
        var b = bByte / 255d;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        double h;
        if (delta <= 1e-12)
        {
            h = 0;
        }
        else if (Math.Abs(max - r) < 1e-12)
        {
            h = 60 * (((g - b) / delta) % 6);
        }
        else if (Math.Abs(max - g) < 1e-12)
        {
            h = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            h = 60 * (((r - g) / delta) + 4);
        }

        if (h < 0)
        {
            h += 360;
        }

        var s = max <= 1e-12 ? 0 : delta / max;
        return (h, Math.Clamp(s, 0, 0.999999));
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
