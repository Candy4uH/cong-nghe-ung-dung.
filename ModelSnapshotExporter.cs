using System.Text.Json;

namespace Object_Detection;

internal static class ModelSnapshotExporter
{
    public static string Export(
        DetectorModel model,
        DetectorConfig config,
        string outputPath,
        string modelName,
        string modelVersion)
    {
        var fullPath = Path.GetFullPath(outputPath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var snapshot = new DetectionModelSnapshot(
            ModelName: modelName,
            ModelVersion: modelVersion,
            RuntimeSettings: new DetectionRuntimeSettings(
                Epochs: config.Epochs,
                MaxTemplatesPerClass: config.MaxTemplatesPerClass,
                MaxTrainImages: config.MaxTrainImages,
                Scales: config.Scales,
                StrideRatio: config.StrideRatio,
                ScoreThreshold: config.ScoreThreshold,
                NmsIouThreshold: config.NmsIouThreshold,
                SimilarityTopK: config.SimilarityTopK,
                HardNegativeSamplesPerImage: config.HardNegativeSamplesPerImage,
                MaxHardNegativesPerClass: config.MaxHardNegativesPerClass,
                HardNegativeWeight: config.HardNegativeWeight,
                EnableThresholdCalibration: config.EnableThresholdCalibration,
                MaxDetectionsPerImage: config.MaxDetectionsPerImage),
            Templates: model.Templates
                .Select(t => new TemplateFeatureSnapshot(t.Label, t.Histogram, t.Width, t.Height))
                .ToList(),
            AvgSizeByLabel: model.AvgSizeByLabel
                .OrderBy(kv => kv.Key)
                .Select(kv => new LabelSizeSnapshot(kv.Key, kv.Value.W, kv.Value.H))
                .ToList(),
            PrototypeByLabel: model.PrototypeByLabel
                .OrderBy(kv => kv.Key)
                .Select(kv => new LabelHistogramSnapshot(kv.Key, kv.Value))
                .ToList(),
            NegativePrototypeByLabel: model.NegativePrototypeByLabel
                .OrderBy(kv => kv.Key)
                .Select(kv => new LabelHistogramSnapshot(kv.Key, kv.Value))
                .ToList(),
            ThresholdByLabel: model.ThresholdByLabel
                .OrderBy(kv => kv.Key)
                .Select(kv => new LabelThresholdSnapshot(kv.Key, kv.Value))
                .ToList());

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(fullPath, json);
        return fullPath;
    }
}
