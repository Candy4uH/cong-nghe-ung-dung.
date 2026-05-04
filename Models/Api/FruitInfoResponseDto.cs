using System.Text.Json.Serialization;

namespace Object_Detection_ASP.NETMVC.Models.Api
{
    public class FruitInfoResponseDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("family")]
        public string? Family { get; set; }

        [JsonPropertyName("order")]
        public string? Order { get; set; }

        [JsonPropertyName("genus")]
        public string? Genus { get; set; }

        [JsonPropertyName("nutritions")]
        public FruitNutritionResponseDto? Nutritions { get; set; }
    }

    public class FruitNutritionResponseDto
    {
        [JsonPropertyName("calories")]
        public double Calories { get; set; }

        [JsonPropertyName("fat")]
        public double Fat { get; set; }

        [JsonPropertyName("sugar")]
        public double Sugar { get; set; }

        [JsonPropertyName("carbohydrates")]
        public double Carbohydrates { get; set; }

        [JsonPropertyName("protein")]
        public double Protein { get; set; }
    }
}
