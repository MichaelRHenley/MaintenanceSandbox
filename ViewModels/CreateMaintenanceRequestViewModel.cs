using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace MaintenanceSandbox.ViewModels
{
    public sealed class CreateMaintenanceRequestViewModel
    {
        public int? SiteId { get; set; }   // ✅ not Guid?

        [Required]
        public int? AreaId { get; set; }

        [Required]
        public int? WorkCenterId { get; set; }

        [Required]
        public int? EquipmentId { get; set; }

        public List<SelectListItem> AreaOptions { get; set; } = new();
        public List<SelectListItem> WorkCenterOptions { get; set; } = new();
        public List<SelectListItem> EquipmentOptions { get; set; } = new();

        public string Priority { get; set; } = "Medium";

        [Required]
        public string? Description { get; set; }

        public string RequestedBy { get; set; } = "";

        // Optional: keep for legacy/debug (not shown)
        public string Site { get; set; } = "";
        public string Area { get; set; } = "";
        public string WorkCenter { get; set; } = "";
        public string Equipment { get; set; } = "";
    }
}
