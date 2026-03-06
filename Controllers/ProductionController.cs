using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models.Production;
using MaintenanceSandbox.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Controllers
{
    [ServiceFilter(typeof(RequireTenantFilter))]
    public class ProductionController : Controller
    {
        private readonly AppDbContext _db;

        public ProductionController(AppDbContext db)
        {
            _db = db;
        }


        [HttpGet]
        public async Task<IActionResult> Index(
    string? selectedSite,
    string? selectedArea,
    string? selectedWorkCenter,
    string? selectedEquipment)
        {
            var vm = new ProductionIndexVm
            {
                Sites = new()
        {
            new SelectListItem("Site Alpha", "Site Alpha"),
            new SelectListItem("Site Beta", "Site Beta"),
            new SelectListItem("Site Gamma", "Site Gamma"),
        },
                Areas = new()
        {
            new SelectListItem("Area 1", "Area 1"),
            new SelectListItem("Area 2", "Area 2"),
            new SelectListItem("Area 3", "Area 3"),
        },
                WorkCenters = new()
        {
            new SelectListItem("WC-01 · Packaging Line A", "WC-01"),
            new SelectListItem("WC-02 · Assembly Station", "WC-02"),
            new SelectListItem("WC-03 · Quality Check", "WC-03"),
        },
                Equipment = new()
        {
            new SelectListItem("EQ-1001 · Conveyor Belt", "EQ-1001"),
            new SelectListItem("EQ-1002 · Sealing Machine", "EQ-1002"),
            new SelectListItem("EQ-1003 · Label Applicator", "EQ-1003"),
        }
            };

            vm.SelectedSite = selectedSite ?? vm.Sites.First().Value!;
            vm.SelectedArea = selectedArea ?? vm.Areas.First().Value!;
            vm.SelectedWorkCenter = selectedWorkCenter ?? vm.WorkCenters.First().Value!;
            vm.SelectedEquipment = selectedEquipment ?? vm.Equipment.First().Value!;

            foreach (var i in vm.Sites) i.Selected = (i.Value == vm.SelectedSite);
            foreach (var i in vm.Areas) i.Selected = (i.Value == vm.SelectedArea);
            foreach (var i in vm.WorkCenters) i.Selected = (i.Value == vm.SelectedWorkCenter);
            foreach (var i in vm.Equipment) i.Selected = (i.Value == vm.SelectedEquipment);

            var todayUtc = DateTime.UtcNow.Date;
            var tomorrowUtc = todayUtc.AddDays(1);

            vm.CurrentCount = await _db.ProductionCountLogs
                .Where(x => x.WorkCenter == vm.SelectedWorkCenter
                         && x.Equipment == vm.SelectedEquipment
                         && x.CreatedUtc >= todayUtc
                         && x.CreatedUtc < tomorrowUtc)
                .SumAsync(x => (int?)x.Units) ?? 0;

            vm.TargetCount = 1200;
            vm.ScrapCount = 23;
            vm.DowntimeMinutes = 45;
            vm.Oee = 82;

            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogProduction(
    string selectedSite,
    string selectedArea,
    string selectedWorkCenter,
    string selectedEquipment,
    int units,
    string? timePeriod,
    string? comments)
        {
            // Guardrails (optional, but helpful)
            if (string.IsNullOrWhiteSpace(selectedWorkCenter) || string.IsNullOrWhiteSpace(selectedEquipment))
            {
                TempData["ToastSuccess"] = "Missing WorkCenter/Equipment selection (hidden fields were blank).";
                return RedirectToAction(nameof(Index));
            }

            var row = new ProductionCountLog
            {
                SelectedSite = selectedSite,
                SelectedArea = selectedArea,
                WorkCenter = selectedWorkCenter,
                Equipment = selectedEquipment,
                Units = units,
                TimePeriod = timePeriod,
                Comments = comments,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = User?.Identity?.Name
            };


            _db.ProductionCountLogs.Add(row);
            await _db.SaveChangesAsync();

            TempData["ToastSuccess"] = $"Production count of {units} units logged.";
            return RedirectToAction(nameof(Index), new { selectedSite, selectedArea, selectedWorkCenter, selectedEquipment });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LogDowntime(string selectedWorkCenter, string selectedEquipment, string eventType, string reason, int? durationMinutes, string description, bool linkToEM)
        {
            TempData["ToastSuccess"] = "Downtime event recorded.";
            return RedirectToAction(nameof(Index), new { selectedWorkCenter, selectedEquipment });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LogScrap(string selectedWorkCenter, string selectedEquipment, int quantity, string reason, string? details)
        {
            TempData["ToastSuccess"] = $"Scrap report submitted: {quantity} units.";
            return RedirectToAction(nameof(Index), new { selectedWorkCenter, selectedEquipment });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddComment(string selectedWorkCenter, string selectedEquipment, string commentType, string comment)
        {
            TempData["ToastSuccess"] = "Comment added.";
            return RedirectToAction(nameof(Index), new { selectedWorkCenter, selectedEquipment });
        }
    }
}
