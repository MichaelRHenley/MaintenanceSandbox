using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MaintenanceSandbox.Models.Parts;

public class AddBomItemViewModel
{
    public int PartId { get; set; }
    public string PartNumber { get; set; } = "";

    [Display(Name = "Asset")]
    [Required]
    public int AssetId { get; set; }

    [Display(Name = "Quantity Per Asset")]
    [Range(0.0001, double.MaxValue)]
    public decimal QuantityPerAsset { get; set; }

    public IEnumerable<SelectListItem> AssetOptions { get; set; }
        = Enumerable.Empty<SelectListItem>();
}
