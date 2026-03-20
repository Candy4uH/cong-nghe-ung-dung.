using OpenCvSharp;

namespace Object_Detection;

// Vai tro: Chua logic train detector histogram mau va sliding-window detection.
internal static class BasicHistogramDetector
{
    private const double PrototypeWeight = 0.4;
    private const int MaxDetectionsPerImage = 30;

    public static DetectorModel Train(IReadOnlyList<AnnotatedImage> trainData, DetectorConfig config)
    {
        var templates = new List<TemplateFeature>();
        var countByLabel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sizeAgg = new Dictionary<string, (double SumW, double SumH, int Count)>(StringComparer.OrdinalIgnoreCase);
        var rng = new Random(42);

        for (var epoch = 1; epoch <= config.Epochs; epoch++)
        {
            var trainOrder = trainData.OrderBy(_ => rng.Next()).ToList();
            var addedInEpoch = 0;

            foreach (var item in trainOrder)
            {
                using var image = Cv2.ImRead(item.ImagePath);
                if (image.Empty())
                {
                    continue;
                }

                foreach (var box in item.Boxes)
                {
                    countByLabel.TryGetValue(box.Label, out var current);
                    if (current >= config.MaxTemplatesPerClass)
                    {
                        continue;
                    }

                    var rect = ClampRect(box, image.Width, image.Height);
                    if (rect.Width < 8 || rect.Height < 8)
                    {
                        continue;
                    }

                    using var roi = new Mat(image, rect);
                    foreach (var hist in ExtractAugmentedHistograms(roi, epoch, rng))
                    {
                        templates.Add(new TemplateFeature(box.Label, hist, rect.Width, rect.Height));
                        countByLabel[box.Label] = current + 1;
                        current++;
                        addedInEpoch++;

                        if (sizeAgg.TryGetValue(box.Label, out var agg))
                        {
                            sizeAgg[box.Label] = (agg.SumW + rect.Width, agg.SumH + rect.Height, agg.Count + 1);
                        }
                        else
                        {
                            sizeAgg[box.Label] = (rect.Width, rect.Height, 1);
                        }

                        if (current >= config.MaxTemplatesPerClass)
                        {
                            break;
                        }
                    }
                }
            }

            Console.WriteLine($"Epoch {epoch}/{config.Epochs}: +{addedInEpoch} templates");
        }

        var avgSize = sizeAgg.ToDictionary(
            kv => kv.Key,
            kv => (W: kv.Value.SumW / kv.Value.Count, H: kv.Value.SumH / kv.Value.Count),
            StringComparer.OrdinalIgnoreCase);
        var prototypes = BuildPrototypes(templates);

        Console.WriteLine($"Train templates: {templates.Count}");
        foreach (var label in countByLabel.OrderBy(k => k.Key))
        {
            Console.WriteLine($"  {label.Key}: {label.Value} templates");
        }

        return new DetectorModel(templates, avgSize, prototypes);
    }

    public static List<Detection> Detect(Mat image, DetectorModel model, DetectorConfig config)
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
                        var rect = new Rect(x, y, winW, winH);
                        using var roi = new Mat(image, rect);
                        var hist = ExtractHistogram(roi);

                        var score = CombinedSimilarity(hist, label, templates, model.PrototypeByLabel);
                        var adaptiveThreshold = config.ScoreThreshold - 0.02 * (config.Epochs - 1);
                        var finalThreshold = Math.Clamp(adaptiveThreshold, 0.35, 1.0);
                        if (score >= finalThreshold)
                        {
                            output.Add(new Detection(label, x, y, x + winW - 1, y + winH - 1, score));
                        }
                    }
                }
            }
        }

        var nms = NonMaximumSuppression(output, config.NmsIouThreshold)
            .OrderByDescending(d => d.Score)
            .Take(MaxDetectionsPerImage)
            .ToList();

        return nms;
    }

    private static float[] ExtractHistogram(Mat bgr)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        const int hBins = 16;
        const int sBins = 16;
        var hist = new float[hBins * sBins];
        var total = Math.Max(1, hsv.Rows * hsv.Cols);

        for (var y = 0; y < hsv.Rows; y++)
        {
            for (var x = 0; x < hsv.Cols; x++)
            {
                var p = hsv.At<Vec3b>(y, x);
                var h = Math.Min(hBins - 1, p.Item0 * hBins / 180);
                var s = Math.Min(sBins - 1, p.Item1 * sBins / 256);
                hist[h * sBins + s]++;
            }
        }

        for (var i = 0; i < hist.Length; i++)
        {
            hist[i] /= total;
        }

        return hist;
    }

    private static IEnumerable<float[]> ExtractAugmentedHistograms(Mat roi, int epoch, Random rng)
    {
        yield return ExtractHistogram(roi);

        // Epoch sau bo sung du lieu ao de model robust hon voi bien doi anh.
        if (epoch >= 2)
        {
            using var flipped = new Mat();
            Cv2.Flip(roi, flipped, FlipMode.Y);
            yield return ExtractHistogram(flipped);
        }

        if (epoch >= 3)
        {
            var alpha = 0.85 + rng.NextDouble() * 0.35;
            var beta = -20 + rng.NextDouble() * 40;
            using var jitter = new Mat();
            roi.ConvertTo(jitter, MatType.CV_8UC3, alpha, beta);
            yield return ExtractHistogram(jitter);
        }
    }

    private static IReadOnlyDictionary<string, float[]> BuildPrototypes(IReadOnlyList<TemplateFeature> templates)
    {
        var grouped = templates.GroupBy(t => t.Label, StringComparer.OrdinalIgnoreCase);
        var prototypes = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var length = group.First().Histogram.Length;
            var sum = new float[length];
            var count = 0;

            foreach (var template in group)
            {
                for (var i = 0; i < length; i++)
                {
                    sum[i] += template.Histogram[i];
                }

                count++;
            }

            if (count > 0)
            {
                for (var i = 0; i < length; i++)
                {
                    sum[i] /= count;
                }

                prototypes[group.Key] = sum;
            }
        }

        return prototypes;
    }

    private static double CombinedSimilarity(
        float[] hist,
        string label,
        List<TemplateFeature> templates,
        IReadOnlyDictionary<string, float[]> prototypeByLabel)
    {
        var bestTemplate = 0.0;
        foreach (var template in templates)
        {
            var score = CosineSimilarity(hist, template.Histogram);
            if (score > bestTemplate)
            {
                bestTemplate = score;
            }
        }

        if (!prototypeByLabel.TryGetValue(label, out var prototype))
        {
            return bestTemplate;
        }

        var prototypeScore = CosineSimilarity(hist, prototype);
        return (1.0 - PrototypeWeight) * bestTemplate + PrototypeWeight * prototypeScore;
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

    private static Rect ClampRect(BoundingBox box, int imageW, int imageH)
    {
        var x1 = Math.Clamp(box.X1, 0, imageW - 1);
        var y1 = Math.Clamp(box.Y1, 0, imageH - 1);
        var x2 = Math.Clamp(box.X2, 0, imageW - 1);
        var y2 = Math.Clamp(box.Y2, 0, imageH - 1);
        if (x2 <= x1)
        {
            x2 = Math.Min(imageW - 1, x1 + 1);
        }

        if (y2 <= y1)
        {
            y2 = Math.Min(imageH - 1, y1 + 1);
        }

        return new Rect(x1, y1, x2 - x1 + 1, y2 - y1 + 1);
    }

    private static List<Detection> NonMaximumSuppression(List<Detection> detections, double iouThreshold)
    {
        var result = new List<Detection>();
        var byLabel = detections
            .GroupBy(d => d.Label)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Score).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var (_, group) in byLabel)
        {
            var suppressed = new bool[group.Count];
            for (var i = 0; i < group.Count; i++)
            {
                if (suppressed[i])
                {
                    continue;
                }

                var current = group[i];
                result.Add(current);

                for (var j = i + 1; j < group.Count; j++)
                {
                    if (suppressed[j])
                    {
                        continue;
                    }

                    if (Geometry.IoU(current, group[j]) >= iouThreshold)
                    {
                        suppressed[j] = true;
                    }
                }
            }
        }

        return result;
    }
}
