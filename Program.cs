using OpenCvSharp;

namespace Object_Detection;

// Vai tro: Diem vao chuong trinh console va dieu phoi toan bo quy trinh train/detect/evaluate.
internal sealed class Program
{
    private const int DefaultMaxTestImages = 100;
    private const double DefaultEvalIouThreshold = 0.5;

    static void Main(string[] args)
    {
        var config = DetectorConfig.FromArgs(args);
        var modelName = GetArg(args, "modelName") ?? "ObjectDetectionModel";
        var modelVersion = GetArg(args, "modelVersion") ?? "1.0.0";
        Console.WriteLine("=== Basic Object Detection (.NET 9 / OpenCV) ===");
        Console.WriteLine(config);

        if (!Directory.Exists(config.TrainDir) || !Directory.Exists(config.TestDir))
        {
            Console.WriteLine("Khong tim thay thu muc train/test.");
            Console.WriteLine($"TrainDir dang dung: {config.TrainDir}");
            Console.WriteLine($"TestDir dang dung : {config.TestDir}");
            Console.WriteLine("Hay truyen --trainDir va --testDir hop le neu can.");
            return;
        }

        var rootDir = Directory.GetCurrentDirectory();
        var imageLocator = new ImageLocator(rootDir);

        var trainSet = AnnotationLoader.Load(config.TrainDir, imageLocator, config.MaxTrainImages);
        var testSet = AnnotationLoader.Load(config.TestDir, imageLocator, DefaultMaxTestImages);

        if (trainSet.Count == 0 || testSet.Count == 0)
        {
            Console.WriteLine("Khong co du du lieu train/test de chay.");
            return;
        }

        var model = BasicHistogramDetector.Train(trainSet, config);
        if (model.Templates.Count == 0)
        {
            Console.WriteLine("Khong tao duoc template nao tu train set. Kiem tra lai anh va annotation.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(config.ExportModelPath))
        {
            var exportedPath = ModelSnapshotExporter.Export(
                model,
                config,
                config.ExportModelPath,
                modelName,
                modelVersion);
            Console.WriteLine($"Model snapshot da duoc xuat ra: {exportedPath}");
        }

        var evaluator = new Evaluator(DefaultEvalIouThreshold);
        Console.WriteLine($"\nBat dau detect tren {testSet.Count} anh test...\n");

        var imageCounter = 0;
        foreach (var item in testSet)
        {
            imageCounter++;
            using var image = Cv2.ImRead(item.ImagePath);
            if (image.Empty())
            {
                Console.WriteLine($"[{imageCounter}] Bo qua vi khong doc duoc anh: {item.ImagePath}");
                continue;
            }

            var detections = BasicHistogramDetector.Detect(image, model, config);
            var result = evaluator.EvaluateImage(detections, item.Boxes);

            Console.WriteLine(
                $"[{imageCounter}] {Path.GetFileName(item.ImagePath)} | GT={item.Boxes.Count} " +
                $"Pred={detections.Count} | TP={result.TruePositive} FP={result.FalsePositive} FN={result.FalseNegative} " +
                $"P={result.Precision:F3} R={result.Recall:F3} F1={result.F1:F3} " +
                $"TyLeDoan={result.PredictionRate * 100:F2}%");

        }

        var summary = evaluator.GetSummary();
        Console.WriteLine("\n=== Tong ket ===");
        Console.WriteLine($"TP={summary.TruePositive} FP={summary.FalsePositive} FN={summary.FalseNegative}");
        Console.WriteLine($"Precision={summary.Precision:F3} Recall={summary.Recall:F3} F1={summary.F1:F3}");
        Console.WriteLine($"Ty le doan tong the: {summary.PredictionRate * 100:F2}%");

        if (!string.IsNullOrWhiteSpace(config.PredictImagePath))
        {
            PredictSingleImage(config.PredictImagePath, model, config);
        }
    }

    private static void PredictSingleImage(string imagePath, DetectorModel model, DetectorConfig config)
    {
        Console.WriteLine("\n=== Du doan anh dau vao ===");
        Console.WriteLine($"Image: {imagePath}");

        if (!File.Exists(imagePath))
        {
            Console.WriteLine("Khong tim thay file anh can du doan.");
            return;
        }

        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            Console.WriteLine("Doc anh that bai. Hay kiem tra lai dinh dang file.");
            return;
        }

        var detections = BasicHistogramDetector.Detect(image, model, config)
            .OrderByDescending(d => d.Score)
            .ToList();

        if (detections.Count == 0)
        {
            Console.WriteLine("Khong tim thay object nao vuot nguong score hien tai.");
            return;
        }

        Console.WriteLine("Ket qua (label + ti le du doan):");
        for (var i = 0; i < detections.Count; i++)
        {
            var det = detections[i];
            Console.WriteLine(
                $"{i + 1}. label={det.Label}, tiLe={det.Score * 100:F2}% " +
                $"box=({det.X1},{det.Y1})-({det.X2},{det.Y2})");
        }

        var best = detections[0];
        Console.WriteLine($"Top-1: label={best.Label}, tiLe={best.Score * 100:F2}%");
    }

    private static string? GetArg(string[] args, string key)
    {
        var pattern = $"--{key}";
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], pattern, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return args[i + 1];
            }

            return "true";
        }

        return null;
    }
}
