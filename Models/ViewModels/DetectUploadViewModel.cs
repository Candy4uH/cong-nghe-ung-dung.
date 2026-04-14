using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Object_Detection_ASP.NETMVC.Models.ViewModels
{
    public class DetectUploadViewModel
    {
        [Required(ErrorMessage = "Please select an image file.")]
        public IFormFile? Image { get; set; }

        public long MaxUploadBytes { get; set; }

        public string AllowedExtensionsDisplay { get; set; } = string.Empty;
    }
}
