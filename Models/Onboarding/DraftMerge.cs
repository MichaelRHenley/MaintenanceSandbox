using System;
using System.Linq;
using System.Collections.Generic;

namespace MaintenanceSandbox.Models.Onboarding;

public static class DraftMerge
{
    public static void EnsureSite(OnboardingDraft draft, string siteName)
    {
        siteName = (siteName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(siteName))
            throw new InvalidOperationException("Site name required.");

        if (draft.Sites.Count == 0)
        {
            draft.Sites.Add(new OnboardingDraftSite { Name = siteName });
            return;
        }

        // For now: single-site onboarding. Keep it deterministic.
        draft.Sites[0].Name = siteName;
    }

    public static void MergeAreas(OnboardingDraft draft, IEnumerable<AreaItem> areas)
    {
        if (draft.Sites.Count == 0)
            throw new InvalidOperationException("Draft has no site. Call EnsureSite first.");

        var site = draft.Sites[0];

        foreach (var a in areas)
        {
            var name = (a.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var existing = site.Areas.FirstOrDefault(x =>
                string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
                site.Areas.Add(new OnboardingDraftArea { Name = name });
        }
    }

    // ✅ FIXED: accepts WorkCenterItem and maps Code/DisplayName into your draft type
    public static void MergeWorkCenters(OnboardingDraft draft, string areaName, List<WorkCenterItem> workCenters)
    {
        areaName = (areaName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(areaName))
            throw new InvalidOperationException("Area name required.");

        if (draft.Sites == null) draft.Sites = new();
        if (draft.Sites.Count == 0)
            throw new InvalidOperationException("Draft has no site. Call EnsureSite first.");

        var site = draft.Sites[0];
        site.Areas ??= new();

        // Ensure area exists
        var area = site.Areas.FirstOrDefault(a =>
            string.Equals(a.Name, areaName, StringComparison.OrdinalIgnoreCase));

        if (area == null)
        {
            area = new OnboardingDraftArea { Name = areaName };
            site.Areas.Add(area);
        }

        area.WorkCenters ??= new();

        foreach (var wc in workCenters ?? new List<WorkCenterItem>())
        {
            var code = (wc.Code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var existing = area.WorkCenters.FirstOrDefault(x =>
                string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                area.WorkCenters.Add(new OnboardingDraftWorkCenter
                {
                    Code = code,
                    DisplayName = string.IsNullOrWhiteSpace(wc.DisplayName) ? code : wc.DisplayName.Trim(),
                    Equipment = new List<string>() // keep empty; not part of AI response yet
                });
            }
            else
            {
                // Keep existing equipment; update display name if provided
                if (!string.IsNullOrWhiteSpace(wc.DisplayName))
                    existing.DisplayName = wc.DisplayName.Trim();
            }
        }
    }
}
