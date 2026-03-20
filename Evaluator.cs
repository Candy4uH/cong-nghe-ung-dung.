namespace Object_Detection;

// Vai tro: Danh gia ket qua detect (TP/FP/FN, Precision/Recall/F1) theo tung anh va tong hop.
internal sealed class Evaluator
{
    private readonly double _iouThreshold;
    private int _tp;
    private int _fp;
    private int _fn;

    public Evaluator(double iouThreshold)
    {
        _iouThreshold = iouThreshold;
    }

    public EvalResult EvaluateImage(List<Detection> detections, IReadOnlyList<BoundingBox> groundTruth)
    {
        var used = new bool[groundTruth.Count];
        var sorted = detections.OrderByDescending(d => d.Score).ToList();

        var tp = 0;
        var fp = 0;
        foreach (var det in sorted)
        {
            var bestIou = 0.0;
            var bestIdx = -1;

            for (var i = 0; i < groundTruth.Count; i++)
            {
                if (used[i])
                {
                    continue;
                }

                if (!string.Equals(groundTruth[i].Label, det.Label, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var iou = Geometry.IoU(det, groundTruth[i]);
                if (iou > bestIou)
                {
                    bestIou = iou;
                    bestIdx = i;
                }
            }

            if (bestIdx >= 0 && bestIou >= _iouThreshold)
            {
                used[bestIdx] = true;
                tp++;
            }
            else
            {
                fp++;
            }
        }

        var fn = used.Count(x => !x);

        _tp += tp;
        _fp += fp;
        _fn += fn;

        return new EvalResult(tp, fp, fn);
    }

    public EvalResult GetSummary() => new(_tp, _fp, _fn);
}
