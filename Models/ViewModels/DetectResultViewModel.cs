namespace Object_Detection_ASP.NETMVC.Models.ViewModels
{
    public class DetectResultViewModel
    {
        public string FileName { get; set; } = string.Empty;

        public string ImageDataUrl { get; set; } = string.Empty;

        public int ImageWidth { get; set; }

        public int ImageHeight { get; set; }

        public long ProcessingTimeMs { get; set; }

        public List<DetectionItemViewModel> Detections { get; set; } = [];
    }

    public class DetectionItemViewModel
    {
        public string Label { get; set; } = string.Empty;

        public double Confidence { get; set; }

        public BoundingBoxViewModel BoundingBox { get; set; } = new();
    }

    public class BoundingBoxViewModel
    {
        public int X1 { get; set; }

        public int Y1 { get; set; }

        public int X2 { get; set; }

        public int Y2 { get; set; }
    }
}
