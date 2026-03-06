using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MaintenanceSandbox.Models.Parts;

public class AddInventoryViewModel
{
    public int PartId { get; set; }
    public string PartNumber { get; set; } = "";

    [Display(Name = "Location / Bin")]
    [Required]
    public int LocationBinId { get; set; }

    [Display(Name = "Quantity On Hand")]
    [Range(0, int.MaxValue)]
    public int QuantityOnHand { get; set; }

    [Display(Name = "Reorder Point")]
    public int? ReorderPoint { get; set; }

    [Display(Name = "Target Quantity")]
    public int? TargetQuantity { get; set; }

    // For dropdown
    public IEnumerable<SelectListItem> LocationOptions { get; set; }
        = Enumerable.Empty<SelectListItem>();
}

