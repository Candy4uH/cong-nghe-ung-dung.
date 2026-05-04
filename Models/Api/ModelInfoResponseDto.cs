using System.Text.Json;
using System.Text.Json.Serialization;

namespace Object_Detection_ASP.NETMVC.Models.Api
{
    public class ModelInfoResponseDto
    {
        [JsonPropertyName("modelName")]
        public string? ModelName { get; set; }

        [JsonPropertyName("modelVersion")]
        public string? ModelVersion { get; set; }

        [JsonPropertyName("templatesByLabel")]
        public Dictionary<string, int>? TemplatesByLabel { get; set; }

        [JsonPropertyName("scoreThreshold")]
        public double? ScoreThreshold { get; set; }

        [JsonPropertyName("scales")]
        public List<double>? Scales { get; set; }

        [JsonPropertyName("thresholdByLabel")]
        public Dictionary<string, double>? ThresholdByLabel { get; set; }

        [JsonPropertyName("labels")]
        public List<string>? Labels { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }
}
