using OpenCvSharp;

namespace Object_Detection;

// Vai tro: Chua logic train detector histogram mau va sliding-window detection.
internal static class BasicHistogramDetector
{
    private const double PrototypeWeight = 0.35;
    private const double SoftNmsSigma = 0.5;
    private const double SoftNmsMinScore = 0.15;

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
        var templatesByLabel = templates
            .GroupBy(t => t.Label)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var hardNegativesByLabel = MineHardNegatives(
            trainData,
            templatesByLabel,
            prototypes,
            avgSize,
            config,
            rng);

        var negativePrototypes = BuildPrototypesFromHistograms(hardNegativesByLabel);
        var calibratedThresholds = config.EnableThresholdCalibration
            ? CalibrateThresholds(templatesByLabel, hardNegativesByLabel, prototypes, negativePrototypes, config)
            : new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"Train templates: {templates.Count}");
        foreach (var label in countByLabel.OrderBy(k => k.Key))
        {
            Console.WriteLine($"  {label.Key}: {label.Value} templates");
        }

        if (hardNegativesByLabel.Count > 0)
        {
            Console.WriteLine("Hard negatives mined:");
            foreach (var item in hardNegativesByLabel.OrderBy(k => k.Key))
            {
                Console.WriteLine($"  {item.Key}: {item.Value.Count}");
            }
        }

        if (calibratedThresholds.Count > 0)
        {
            Console.WriteLine("Calibrated thresholds:");
            foreach (var item in calibratedThresholds.OrderBy(k => k.Key))
            {
                Console.WriteLine($"  {item.Key}: {item.Value:0.###}");
            }
        }

        return new DetectorModel(templates, avgSize, prototypes, negativePrototypes, calibratedThresholds);
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
                        var rect = new Rect(x, y, winW, winH);
                        using var roi = new Mat(image, rect);
                        var hist = ExtractHistogram(roi);

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

        var nms = SoftNonMaximumSuppression(output, config.NmsIouThreshold)
            .OrderByDescending(d => d.Score)
            .Take(config.MaxDetectionsPerImage)
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

    private static IReadOnlyDictionary<string, float[]> BuildPrototypesFromHistograms(
        IReadOnlyDictionary<string, List<float[]>> samplesByLabel)
    {
        var prototypes = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var (label, samples) in samplesByLabel)
        {
            if (samples.Count == 0)
            {
                continue;
            }

            var length = samples[0].Length;
            var sum = new float[length];
            foreach (var sample in samples)
            {
                for (var i = 0; i < length; i++)
                {
                    sum[i] += sample[i];
                }
            }

            for (var i = 0; i < length; i++)
            {
                sum[i] /= samples.Count;
            }

            prototypes[label] = sum;
        }

        return prototypes;
    }

    private static Dictionary<string, List<float[]>> MineHardNegatives(
        IReadOnlyList<AnnotatedImage> trainData,
        IReadOnlyDictionary<string, List<TemplateFeature>> templatesByLabel,
        IReadOnlyDictionary<string, float[]> prototypeByLabel,
        IReadOnlyDictionary<string, (double W, double H)> avgSizeByLabel,
        DetectorConfig config,
        Random rng)
    {
        var result = new Dictionary<string, List<float[]>>(StringComparer.OrdinalIgnoreCase);
        if (config.HardNegativeSamplesPerImage <= 0 || config.MaxHardNegativesPerClass <= 0)
        {
            return result;
        }

        var scored = templatesByLabel.Keys.ToDictionary(
            key => key,
            _ => new List<(float[] Hist, double Score)>(),
            StringComparer.OrdinalIgnoreCase);
        var mineThreshold = Math.Max(0.35, config.ScoreThreshold - 0.10);

        foreach (var item in trainData)
        {
            using var image = Cv2.ImRead(item.ImagePath);
            if (image.Empty())
            {
                continue;
            }

            foreach (var (label, templates) in templatesByLabel)
            {
                if (!avgSizeByLabel.TryGetValue(label, out var avgSize))
                {
                    continue;
                }

                for (var sampleIdx = 0; sampleIdx < config.HardNegativeSamplesPerImage; sampleIdx++)
                {
                    var randomScale = 0.8 + rng.NextDouble() * 0.6;
                    var winW = Math.Max(8, (int)Math.Round(avgSize.W * randomScale));
                    var winH = Math.Max(8, (int)Math.Round(avgSize.H * randomScale));
                    if (winW >= image.Width || winH >= image.Height)
                    {
                        continue;
                    }

                    var x = rng.Next(0, image.Width - winW + 1);
                    var y = rng.Next(0, image.Height - winH + 1);
                    var rect = new Rect(x, y, winW, winH);

                    if (MaxIoUWithGroundTruth(rect, item.Boxes) >= 0.12)
                    {
                        continue;
                    }

                    using var roi = new Mat(image, rect);
                    var hist = ExtractHistogram(roi);
                    var score = PositiveSimilarity(hist, label, templates, prototypeByLabel, config.SimilarityTopK);
                    if (score < mineThreshold)
                    {
                        continue;
                    }

                    scored[label].Add((hist, score));
                }
            }
        }

        foreach (var (label, list) in scored)
        {
            var selected = list
                .OrderByDescending(x => x.Score)
                .Take(config.MaxHardNegativesPerClass)
                .Select(x => x.Hist)
                .ToList();

            if (selected.Count > 0)
            {
                result[label] = selected;
            }
        }

        return result;
    }

    private static Dictionary<string, double> CalibrateThresholds(
        IReadOnlyDictionary<string, List<TemplateFeature>> templatesByLabel,
        IReadOnlyDictionary<string, List<float[]>> hardNegativesByLabel,
        IReadOnlyDictionary<string, float[]> prototypeByLabel,
        IReadOnlyDictionary<string, float[]> negativePrototypeByLabel,
        DetectorConfig config)
    {
        var calibrated = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (label, templates) in templatesByLabel)
        {
            var positiveScores = templates
                .Select(t => CombinedSimilarity(t.Histogram, label, templates, prototypeByLabel, negativePrototypeByLabel, config))
                .OrderBy(x => x)
                .ToArray();
            if (positiveScores.Length == 0)
            {
                continue;
            }

            var negativeSamples = hardNegativesByLabel.TryGetValue(label, out var negatives)
                ? negatives
                : [];
            if (negativeSamples.Count == 0)
            {
                var relaxed = Percentile(positiveScores, 0.20);
                calibrated[label] = Math.Clamp(relaxed, 0.35, 0.90);
                continue;
            }

            var negativeScores = negativeSamples
                .Select(n => CombinedSimilarity(n, label, templates, prototypeByLabel, negativePrototypeByLabel, config))
                .OrderBy(x => x)
                .ToArray();

            var candidates = positiveScores
                .Concat(negativeScores)
                .Append(config.ScoreThreshold)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            var bestThreshold = config.ScoreThreshold;
            var bestF1 = double.NegativeInfinity;
            var bestPrecision = 0.0;
            var bestRecall = 0.0;

            foreach (var candidate in candidates)
            {
                var tp = positiveScores.Count(x => x >= candidate);
                var fn = positiveScores.Length - tp;
                var fp = negativeScores.Count(x => x >= candidate);

                var precision = tp + fp == 0 ? 0.0 : (double)tp / (tp + fp);
                var recall = tp + fn == 0 ? 0.0 : (double)tp / (tp + fn);
                var f1 = precision + recall < 1e-12 ? 0.0 : 2 * precision * recall / (precision + recall);

                if (f1 > bestF1 ||
                    (Math.Abs(f1 - bestF1) < 1e-9 && precision > bestPrecision) ||
                    (Math.Abs(f1 - bestF1) < 1e-9 && Math.Abs(precision - bestPrecision) < 1e-9 && recall > bestRecall))
                {
                    bestF1 = f1;
                    bestPrecision = precision;
                    bestRecall = recall;
                    bestThreshold = candidate;
                }
            }

            calibrated[label] = Math.Clamp(bestThreshold, 0.35, 0.92);
        }

        return calibrated;
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

    private static double Percentile(double[] sortedAsc, double p)
    {
        if (sortedAsc.Length == 0)
        {
            return 0;
        }

        p = Math.Clamp(p, 0, 1);
        var index = p * (sortedAsc.Length - 1);
        var lo = (int)Math.Floor(index);
        var hi = (int)Math.Ceiling(index);
        if (lo == hi)
        {
            return sortedAsc[lo];
        }

        var frac = index - lo;
        return sortedAsc[lo] * (1 - frac) + sortedAsc[hi] * frac;
    }

    private static double MaxIoUWithGroundTruth(Rect rect, IReadOnlyList<BoundingBox> groundTruth)
    {
        var x1 = rect.X;
        var y1 = rect.Y;
        var x2 = rect.X + rect.Width - 1;
        var y2 = rect.Y + rect.Height - 1;

        var maxIou = 0.0;
        foreach (var gt in groundTruth)
        {
            var iou = Geometry.IoU(x1, y1, x2, y2, gt.X1, gt.Y1, gt.X2, gt.Y2);
            if (iou > maxIou)
            {
                maxIou = iou;
            }
        }

        return maxIou;
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
