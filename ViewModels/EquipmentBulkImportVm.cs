using System.ComponentModel.DataAnnotations;

namespace MaintenanceSandbox.ViewModels
{
    public sealed class EquipmentBulkImportVm
    {
        [Required]
        public int WorkCenterId { get; set; }

        [Required]
        public string PasteText { get; set; } = "";
    }
}
