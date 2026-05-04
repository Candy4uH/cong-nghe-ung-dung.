using Microsoft.AspNetCore.Http;

namespace Object_Detection.Api.Contracts.Requests;

public sealed class DetectImageRequest
{
    public IFormFile? Image { get; set; }
}
