using System.Text.Json;
using System.Text.Json.Serialization;

namespace Object_Detection_ASP.NETMVC.Models.Api
{
    public class ModelInfoResponseDto
    {
        [JsonPropertyName("modelName")]
        public string? ModelName { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("framework")]
        public string? Framework { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("labels")]
        public List<string>? Labels { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }
}
