using System;

namespace MaintenanceSandbox.Models.Production
{
    public class ProductionCountLog
    {
        public int Id { get; set; }

        // If you’re capturing these (you are), store them
        public string SelectedSite { get; set; } = "";
        public string SelectedArea { get; set; } = "";

        // These are the missing properties causing your build failure
        public string WorkCenter { get; set; } = "";
        public string Equipment { get; set; } = "";

        public int Units { get; set; }
        public string? TimePeriod { get; set; }
        public string? Comments { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
    }
}
