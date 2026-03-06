using Microsoft.AspNetCore.Mvc.Rendering;

namespace MaintenanceSandbox.Models.Production
{
    public class ProductionIndexVm
    {
        // Dropdown lists
        public List<SelectListItem> Sites { get; set; } = new();
        public List<SelectListItem> Areas { get; set; } = new();
        public List<SelectListItem> WorkCenters { get; set; } = new();
        public List<SelectListItem> Equipment { get; set; } = new();

        // Selected values (posted back)
        public string? SelectedSite { get; set; }
        public string? SelectedArea { get; set; }
        public string? SelectedWorkCenter { get; set; }
        public string? SelectedEquipment { get; set; }

        // KPI
        public int CurrentCount { get; set; }
        public int TargetCount { get; set; }
        public int ScrapCount { get; set; }
        public int DowntimeMinutes { get; set; }
        public int Oee { get; set; } // %
        public string ProductTier { get; set; } = "Standard";
    }
}

