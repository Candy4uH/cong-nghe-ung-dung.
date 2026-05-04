using System.ComponentModel.DataAnnotations;

namespace Object_Detection_ASP.NETMVC.Models.Api
{
    public class FruitInfoApiOptions
    {
        public const string SectionName = "FruitInfoApi";

        [Required]
        public string BaseUrl { get; set; } = string.Empty;

        [Range(1, 120)]
        public int TimeoutSeconds { get; set; } = 15;

        [MinLength(1)]
        public List<string> FeaturedFruits { get; set; } =
        [
            "apple",
            "banana",
            "orange"
        ];
    }
}
