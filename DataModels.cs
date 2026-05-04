using OpenCvSharp;

namespace Object_Detection;

// Vai tro: Chua cac kieu du lieu dung chung cho annotation, detection va model.
internal sealed record BoundingBox(string Label, int X1, int Y1, int X2, int Y2)
{
    public int Width => Math.Max(1, X2 - X1 + 1);
    public int Height => Math.Max(1, Y2 - Y1 + 1);
}

internal sealed record Detection(string Label, int X1, int Y1, int X2, int Y2, double Score)
{
    public Rect ToRect() => new(X1, Y1, Math.Max(1, X2 - X1 + 1), Math.Max(1, Y2 - Y1 + 1));
}

internal sealed record AnnotatedImage(string ImagePath, IReadOnlyList<BoundingBox> Boxes);

internal sealed record TemplateFeature(string Label, float[] Histogram, double Width, double Height);

internal sealed record DetectorModel(
    IReadOnlyList<TemplateFeature> Templates,
    IReadOnlyDictionary<string, (double W, double H)> AvgSizeByLabel,
    IReadOnlyDictionary<string, float[]> PrototypeByLabel,
    IReadOnlyDictionary<string, float[]> NegativePrototypeByLabel,
    IReadOnlyDictionary<string, double> ThresholdByLabel);

internal sealed record EvalResult(int TruePositive, int FalsePositive, int FalseNegative)
{
    public double Precision => TruePositive + FalsePositive == 0 ? 0 : (double)TruePositive / (TruePositive + FalsePositive);
    public double Recall => TruePositive + FalseNegative == 0 ? 0 : (double)TruePositive / (TruePositive + FalseNegative);
    public double F1 => Precision + Recall < 1e-12 ? 0 : 2 * Precision * Recall / (Precision + Recall);
    public double PredictionRate => TruePositive + FalsePositive + FalseNegative == 0
        ? 0
        : (double)TruePositive / (TruePositive + FalsePositive + FalseNegative);
}
