using System.Text.Json;
using System.Text.Json.Serialization;

namespace Object_Detection_ASP.NETMVC.Models.Api
{
    public class HealthResponseDto
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("modelLoaded")]
        public bool ModelLoaded { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("modelFilePath")]
        public string? ModelFilePath { get; set; }

        [JsonPropertyName("modelLoadedAtUtc")]
        public DateTimeOffset? ModelLoadedAtUtc { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }
}
