using System.Xml.Linq;

namespace Object_Detection;

// Vai tro: Doc file XML Pascal VOC va chuyen thanh du lieu AnnotatedImage.
internal static class AnnotationLoader
{
    public static List<AnnotatedImage> Load(string annotationDir, ImageLocator imageLocator, int maxImages)
    {
        var files = Directory.EnumerateFiles(annotationDir, "*.xml", SearchOption.TopDirectoryOnly)
            .OrderBy(s => s)
            .Take(maxImages)
            .ToList();

        var output = new List<AnnotatedImage>(files.Count);

        foreach (var file in files)
        {
            var parsed = ParseOne(file, imageLocator);
            if (parsed is not null)
            {
                output.Add(parsed);
            }
        }

        Console.WriteLine($"Loaded {output.Count}/{files.Count} annotations from {annotationDir}");
        return output;
    }

    private static AnnotatedImage? ParseOne(string xmlPath, ImageLocator imageLocator)
    {
        try
        {
            var doc = XDocument.Load(xmlPath);
            var root = doc.Root;
            if (root is null)
            {
                return null;
            }

            var fileName = root.Element("filename")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = Path.GetFileNameWithoutExtension(xmlPath) + ".jpg";
            }

            var imagePath = imageLocator.Find(Path.GetDirectoryName(xmlPath) ?? string.Empty, fileName);
            if (imagePath is null)
            {
                return null;
            }

            var boxes = new List<BoundingBox>();
            foreach (var obj in root.Elements("object"))
            {
                var label = obj.Element("name")?.Value.Trim().ToLowerInvariant();
                var bnd = obj.Element("bndbox");
                if (string.IsNullOrWhiteSpace(label) || bnd is null)
                {
                    continue;
                }

                var xmin = ParseInt(bnd.Element("xmin")?.Value);
                var ymin = ParseInt(bnd.Element("ymin")?.Value);
                var xmax = ParseInt(bnd.Element("xmax")?.Value);
                var ymax = ParseInt(bnd.Element("ymax")?.Value);
                if (xmin >= xmax || ymin >= ymax)
                {
                    continue;
                }

                boxes.Add(new BoundingBox(label, xmin, ymin, xmax, ymax));
            }

            return boxes.Count == 0 ? null : new AnnotatedImage(imagePath, boxes);
        }
        catch
        {
            return null;
        }
    }

    private static int ParseInt(string? raw)
        => int.TryParse(raw, out var value) ? value : 0;
}
