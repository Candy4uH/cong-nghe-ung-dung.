using System.Text.Json.Serialization;

namespace Object_Detection_ASP.NETMVC.Models.Api
{
    public class DetectionResponseDto
    {
        [JsonPropertyName("imageWidth")]
        public int ImageWidth { get; set; }

        [JsonPropertyName("imageHeight")]
        public int ImageHeight { get; set; }

        [JsonPropertyName("processingTimeMs")]
        public long ProcessingTimeMs { get; set; }

        [JsonPropertyName("detections")]
        public List<DetectionDto> Detections { get; set; } = [];
    }

    public class DetectionDto
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("boundingBox")]
        public BoundingBoxDto BoundingBox { get; set; } = new();
    }

    public class BoundingBoxDto
    {
        [JsonPropertyName("x1")]
        public int X1 { get; set; }

        [JsonPropertyName("y1")]
        public int Y1 { get; set; }

        [JsonPropertyName("x2")]
        public int X2 { get; set; }

        [JsonPropertyName("y2")]
        public int Y2 { get; set; }
    }
}
