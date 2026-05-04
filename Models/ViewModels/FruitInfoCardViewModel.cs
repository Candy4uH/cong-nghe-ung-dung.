namespace Object_Detection_ASP.NETMVC.Models.ViewModels
{
    public class FruitInfoCardViewModel
    {
        public string Name { get; set; } = string.Empty;

        public string? Family { get; set; }

        public string? Order { get; set; }

        public string? Genus { get; set; }

        public double Calories { get; set; }

        public double Fat { get; set; }

        public double Sugar { get; set; }

        public double Carbohydrates { get; set; }

        public double Protein { get; set; }
    }
}
