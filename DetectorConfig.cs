using System.Globalization;

namespace Object_Detection;

// Vai tro: Chua cau hinh detector va parse tham so dong lenh (--trainDir, --scoreThreshold, ...).
internal sealed record DetectorConfig(
    string TrainDir,
    string TestDir,
    string? PredictImagePath,
    int Epochs,
    int MaxTemplatesPerClass,
    int MaxTrainImages,
    double[] Scales,
    double StrideRatio,
    double ScoreThreshold,
    double NmsIouThreshold)
{
    public static DetectorConfig FromArgs(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";
            map[key[2..]] = value;
        }

        var trainDir = map.TryGetValue("trainDir", out var trainArg)
            ? Path.GetFullPath(trainArg)
            : ResolveDefaultDataDir("train");
        var testDir = map.TryGetValue("testDir", out var testArg)
            ? Path.GetFullPath(testArg)
            : ResolveDefaultDataDir("test");
        var scalesText = GetString(map, "scales", "0.8,1.0,1.2");
        var scales = scalesText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 1.0)
            .Where(v => v > 0.1)
            .Distinct()
            .OrderBy(v => v)
            .ToArray();

        if (scales.Length == 0)
        {
            scales = [1.0];
        }

        return new DetectorConfig(
            TrainDir: trainDir,
            TestDir: testDir,
            PredictImagePath: map.TryGetValue("predictImage", out var predictImage) ? Path.GetFullPath(predictImage) : null,
            Epochs: Math.Max(1, GetInt(map, "epochs", 4)),
            MaxTemplatesPerClass: Math.Max(20, GetInt(map, "maxTemplatesPerClass", 150)),
            MaxTrainImages: GetInt(map, "maxTrainImages", 300),
            Scales: scales,
            StrideRatio: GetDouble(map, "strideRatio", 0.2),
            ScoreThreshold: GetDouble(map, "scoreThreshold", 0.70),
            NmsIouThreshold: GetDouble(map, "nmsIou", 0.35));
    }

    public override string ToString()
    {
        return string.Join(Environment.NewLine,
            $"trainDir           : {TrainDir}",
            $"testDir            : {TestDir}",
            $"predictImage       : {PredictImagePath ?? "(none)"}",
            $"epochs             : {Epochs}",
            $"maxTemplates/class : {MaxTemplatesPerClass}",
            $"maxTrainImages     : {MaxTrainImages}",
            $"scales             : {string.Join(',', Scales.Select(s => s.ToString("0.##", CultureInfo.InvariantCulture)))}",
            $"strideRatio        : {StrideRatio:0.###}",
            $"scoreThreshold     : {ScoreThreshold:0.###}",
            $"nmsIou             : {NmsIouThreshold:0.###}");
    }

    private static string GetString(Dictionary<string, string> map, string key, string fallback)
        => map.TryGetValue(key, out var value) ? value : fallback;

    private static int GetInt(Dictionary<string, string> map, string key, int fallback)
        => map.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static double GetDouble(Dictionary<string, string> map, string key, double fallback)
        => map.TryGetValue(key, out var value) &&
           double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string ResolveDefaultDataDir(string folderName)
    {
        var probes = new List<string>
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var startPath in probes)
        {
            var found = FindFolderInParents(startPath, folderName);
            if (found is not null)
            {
                return found;
            }
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), folderName));
    }

    private static string? FindFolderInParents(string startPath, string folderName)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startPath));
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, folderName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
