using System.ComponentModel.DataAnnotations;

namespace Object_Detection_ASP.NETMVC.Models.Api
{
    public class ObjectDetectionApiOptions
    {
        public const string SectionName = "ObjectDetectionApi";

        [Required]
        public string BaseUrl { get; set; } = string.Empty;

        [Range(1, 300)]
        public int TimeoutSeconds { get; set; } = 30;

        [Range(1, 50 * 1024 * 1024)]
        public long MaxUploadBytes { get; set; } = 5 * 1024 * 1024;

        [MinLength(1)]
        public List<string> AllowedExtensions { get; set; } = [];
    }
}
