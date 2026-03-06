using MaintenanceSandbox.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace MaintenanceSandbox.Controllers;
[ServiceFilter(typeof(RequireTenantFilter))]
public class SettingsController : Controller
{
    private readonly ITierProvider _tierProvider;
    private readonly IStringLocalizer<SharedResource> _sr;

    public SettingsController(ITierProvider tierProvider,
                              IStringLocalizer<SharedResource> sr)
    {
        _tierProvider = tierProvider;
        _sr = sr;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var model = new TierSettingsViewModel
        {
            SelectedTier = _tierProvider.CurrentTier.ToString()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(TierSettingsViewModel model)
    {
        if (!Enum.TryParse<ProductTier>(model.SelectedTier, out var tier))
        {
            // Localized validation error
            ModelState.AddModelError(string.Empty, _sr["Settings_InvalidTier"]);
            return View(model);
        }

        _tierProvider.SetTier(tier);

        // Localize the tier name for the banner
        var tierKey = tier switch
        {
            ProductTier.Enhanced => "Tier_Label_Enhanced",
            ProductTier.Premium => "Tier_Label_Premium",
            _ => "Tier_Label_Standard"
        };

        var localizedTierName = _sr[tierKey];

        // "Tier changed to {0}." / "Niveau modifié en {0}." / "Nivel cambiado a {0}."
        TempData["TierChanged"] = string.Format(
            _sr["Settings_TierChanged_Message"],
            localizedTierName
        );

        return RedirectToAction("Index", "Maintenance");
    }
}

public class TierSettingsViewModel
{
    public string SelectedTier { get; set; } = "Standard";
}
