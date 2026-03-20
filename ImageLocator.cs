namespace Object_Detection;

// Vai tro: Tim duong dan thuc te cua file anh dua tren ten file trong annotation.
internal sealed class ImageLocator
{
    private readonly string _rootDir;
    private Dictionary<string, string>? _fileIndex;

    public ImageLocator(string rootDir)
    {
        _rootDir = rootDir;
    }

    public string? Find(string annotationDir, string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(annotationDir, fileName),
            Path.Combine(_rootDir, fileName),
            Path.Combine(annotationDir, Path.ChangeExtension(fileName, ".jpg")),
            Path.Combine(annotationDir, Path.ChangeExtension(fileName, ".png")),
            Path.Combine(annotationDir, Path.ChangeExtension(fileName, ".jpeg"))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        _fileIndex ??= BuildFileIndex(_rootDir);

        return _fileIndex.TryGetValue(Path.GetFileName(fileName).ToLowerInvariant(), out var found)
            ? found
            : null;
    }

    private static Dictionary<string, string> BuildFileIndex(string rootDir)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp"
        };

        foreach (var file in Directory.EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories))
        {
            if (!extensions.Contains(Path.GetExtension(file)))
            {
                continue;
            }

            var key = Path.GetFileName(file).ToLowerInvariant();
            map.TryAdd(key, file);
        }

        return map;
    }
}
