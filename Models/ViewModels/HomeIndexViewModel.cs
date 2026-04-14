namespace Object_Detection_ASP.NETMVC.Models.ViewModels
{
    public class HomeIndexViewModel
    {
        public string? HealthStatus { get; set; }

        public string? HealthMessage { get; set; }

        public DateTimeOffset? HealthTimestamp { get; set; }

        public string? HealthErrorMessage { get; set; }

        public string? ModelName { get; set; }

        public string? ModelVersion { get; set; }

        public string? ModelFramework { get; set; }

        public string? ModelDescription { get; set; }

        public List<string> Labels { get; set; } = [];

        public Dictionary<string, string> AdditionalModelInfo { get; set; } = new();

        public string? ModelInfoErrorMessage { get; set; }
    }
}
