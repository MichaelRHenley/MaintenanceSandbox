using MaintenanceSandbox.Data;
using MaintenanceSandbox.Services;

public class StubAiAssistantClient : IAiAssistantClient
{
    public Task<string> GetJsonAsync(string prompt, CancellationToken ct = default)
    {
        // Safe default: empty JSON object.
        // If you want, you can add simple routing logic here later based on prompt content.
        return Task.FromResult("{ }");
    }

    public Task<string> GetHelpAsync(AiHelpRequest request, CancellationToken ct = default)
    {
        var moduleKey = request.ModuleKey ?? "Generic";

        string text = moduleKey switch
        {
            // 🔹 EM LIST (Index)
            "EM_List" => request.Intent switch
            {
                AiHelpIntent.ExplainScreen =>
                    "This is the Emergency Maintenance overview. It shows all open EM tickets so you can see what’s live on the floor, filter by area/work center, and pick a job to work on.",

                AiHelpIntent.ExplainFields =>
                    "Key columns:\n\n" +
                    "- Status: where each request is in the workflow (New, In progress, Waiting on parts, Resolved).\n" +
                    "- Priority: how urgent it is relative to other work.\n" +
                    "- Area / Work center / Equipment: where the issue is happening.\n" +
                    "- Created: when the request was raised.\n" +
                    "- Age: how long it has been open.",

                AiHelpIntent.ShowExamples =>
                    "Example uses:\n\n" +
                    "- A supervisor scans this list at shift hand-off to see what’s still open.\n" +
                    "- A maintainer filters to their line and picks the next EM to work.\n" +
                    "- A lead checks for anything high-priority that is older than a target age.",

                AiHelpIntent.WhatFirst =>
                    "Start by:\n\n" +
                    "1) Filter to your area or work center.\n" +
                    "2) Sort or scan for High-priority and oldest Age.\n" +
                    "3) Open the details page for the job you’re going to take.\n" +
                    "4) Leave a quick comment there so others know who owns it.",

                _ => "This page shows the full EM queue. Use filters and sorting to decide what to work on next."
            },

            // 🔹 EM DETAILS
            "EM_Details" => request.Intent switch
            {
                AiHelpIntent.ExplainScreen =>
                    "This is the detail page for a single emergency maintenance request. It shows the asset, status, priority, who raised it, timestamps, AI suggestions, and the live comment thread.",

                AiHelpIntent.ExplainFields =>
                    "Fields on this screen:\n\n" +
                    "- Site / Area / Work center / Equipment: location of the issue.\n" +
                    "- Status: current workflow state (New, In progress, Waiting on parts, Resolved).\n" +
                    "- Priority: impact/urgency.\n" +
                    "- Requested by / Created at / Resolved at: who raised it and timing.\n" +
                    "- Description: original problem description from the operator.\n" +
                    "- Comments: conversation between operator and maintenance.\n" +
                    "- Workflow controls: status/priority pickers for leads.",

                AiHelpIntent.ShowExamples =>
                    "Example uses:\n\n" +
                    "- A maintainer reads description + comments to understand what has already been tried.\n" +
                    "- The lead updates Status and Priority as work progresses.\n" +
                    "- The team records root cause and fix steps in the comment thread for future reference.",

                AiHelpIntent.WhatFirst =>
                    "On this screen, start by:\n\n" +
                    "1) Confirm the correct equipment and work center.\n" +
                    "2) Read the most recent comments to see current status.\n" +
                    "3) If you’re taking the job, add a comment so others know who owns it.\n" +
                    "4) If you’re a lead, update Status/Priority to match reality.",

                _ => "This page is the detailed view of a single EM ticket, including workflow controls and the conversation history."
            },


            // 🔹 PARTS – index/search
            "Parts_Search" or "Parts_Index" => request.Intent switch
            {
                AiHelpIntent.ExplainScreen =>
                    "This is the parts catalog screen. It lets you browse and search parts, see which are active, and get a quick feel for stock levels.",

                AiHelpIntent.ExplainFields =>
                    "Key elements on this screen:\n\n" +
                    "- Search: text box to find parts by number, description, or AI description.\n" +
                    "- Status filter: show all, only active, or only inactive parts.\n" +
                    "- Parts table: shows part number, descriptions, manufacturer info, and on-hand quantity.\n" +
                    "- New Part: button to create a brand new catalog record.",

                AiHelpIntent.ShowExamples =>
                    "Example uses:\n\n" +
                    "- Type a part number fragment or keyword (e.g. 'idler', 'vacuum hose') and filter to Active only.\n" +
                    "- Scan the list to see which parts are inactive before cleaning up the catalog.\n" +
                    "- Look at On Hand = 0 with Active status to see parts that might need attention.",

                AiHelpIntent.WhatFirst =>
                    "To get started:\n\n" +
                    "1) Decide if you want to see all parts or just active ones.\n" +
                    "2) Use the Search box to narrow down to the part or family you care about.\n" +
                    "3) Check the On Hand column to see stock health.\n" +
                    "4) Use New Part if you don’t see the item you need in the catalog.",

                _ => "Use this page to browse and manage your parts catalog, including search, filters, and creation of new parts."
            },

            "Part_Details" => request.Intent switch
            {
                AiHelpIntent.ExplainScreen =>
                    "This is the Part Details screen. It shows everything known about a specific part — descriptions, manufacturer info, inventory levels across locations, BOM usage, and relationships to other similar parts.",

                AiHelpIntent.ExplainFields =>
                    "Fields on this screen:\n\n" +
                    "- Short / Long Description: the main text identifying the part.\n" +
                    "- AI Description: an optional enhanced text summary.\n" +
                    "- Manufacturer + Mfg Part #: supplier information.\n" +
                    "- Inventory Summary: total stock, location count, and activity status.\n" +
                    "- Inventory by Location: detailed stock levels, reorder points, and targets.\n" +
                    "- Used On Assets (BOM): which assets require this part and in what quantity.\n" +
                    "- Similar Parts: suggestions based on description/manufacturer similarity.",

                AiHelpIntent.ShowExamples =>
                    "Example uses:\n\n" +
                    "- Check stock levels across multiple stores or sites.\n" +
                    "- See which assets depend on this part before making provisioning decisions.\n" +
                    "- Review reorder thresholds and stockout warnings.\n" +
                    "- Compare with similar parts to eliminate duplicates or vendor conflicts.",

                AiHelpIntent.WhatFirst =>
                    "Start here:\n\n" +
                    "1) Look at the Inventory Summary to understand on-hand quantity.\n" +
                    "2) Review where this part is used to gauge operational risk.\n" +
                    "3) Scan the enhanced AI Description for clarity.\n" +
                    "4) Check reorder points for upcoming shortages.",

                _ => "This page shows detailed information for a single part, covering inventory, BOM usage, and metadata."
            },

            "Part_Edit" => request.Intent switch
            {
                AiHelpIntent.ExplainScreen =>
                    "This is the Edit Part screen. It lets you update the core catalog information for a part, including descriptions, manufacturer details, and whether the part is active.",

                AiHelpIntent.ExplainFields =>
                    "Fields on this form:\n\n" +
                    "- Part Number: the internal catalog identifier used across the system.\n" +
                    "- Short Description: a concise label that shows up in lists and searches.\n" +
                    "- Long Description: a more detailed explanation of the part, specs, or usage.\n" +
                    "- Manufacturer: supplier name.\n" +
                    "- Mfg Part #: the supplier’s own part number.\n" +
                    "- Is Active: whether the part is available to be used on new work.",

                AiHelpIntent.ShowExamples =>
                    "Example edits:\n\n" +
                    "- Clean up Short and Long descriptions so searches are more reliable.\n" +
                    "- Add the manufacturer and Mfg Part # when you standardize on a vendor.\n" +
                    "- Flip Is Active to false when a part is obsolete or replaced.",

                AiHelpIntent.WhatFirst =>
                    "When editing a part:\n\n" +
                    "1) Confirm the Part Number is correct — changing it can affect lookups.\n" +
                    "2) Make sure the Short Description is clear and operator-friendly.\n" +
                    "3) Update manufacturer details if you’ve changed suppliers.\n" +
                    "4) Only mark a part inactive if you’re sure it shouldn’t be used on new jobs.",

                _ => "Use this form to safely update catalog information for a part without losing its history."
            },

            "Part_Create" => request.Intent switch
            {
                AiHelpIntent.ExplainScreen =>
                    "This is the Part Creation Wizard. It walks you through creating a new catalog part in three steps: basic identity, manufacturer details, and a final review before saving.",

                AiHelpIntent.ExplainFields =>
                    "Wizard steps and fields:\n\n" +
                    "Step 1 – Basic Info:\n" +
                    "- Part Number: the internal ID used everywhere.\n" +
                    "- Short Description: the main label operators will see.\n" +
                    "- Long Description: optional extra detail or specs.\n\n" +
                    "Step 2 – Manufacturer:\n" +
                    "- Manufacturer: supplier name.\n" +
                    "- Mfg Part #: the supplier’s own part number.\n" +
                    "- Active in catalog: whether this part should be available for new work.\n\n" +
                    "Step 3 – Review & Save:\n" +
                    "- Shows a summary of all fields so you can double-check before creating.",

                AiHelpIntent.ShowExamples =>
                    "Example uses:\n\n" +
                    "- Adding a brand-new spare that maintenance has just approved.\n" +
                    "- Cleaning up legacy parts by re-creating them with clearer descriptions and vendor info.\n" +
                    "- Onboarding a new vendor’s standard part numbers into your catalog.",

                AiHelpIntent.WhatFirst =>
                    "To use this wizard effectively:\n\n" +
                    "1) Start on Step 1 by choosing a clear, stable Part Number and a short, operator-friendly description.\n" +
                    "2) On Step 2, fill in manufacturer details so purchasing and stores can match the right vendor.\n" +
                    "3) On Step 3, read the review cards and fix anything that looks off before you hit Create Part.\n" +
                    "4) After saving, go to Part Details to add inventory locations, BOM links, and an AI description.",

                _ => "Use this wizard to safely add a new part to the catalog, with a quick review step before it goes live."
            },

            "Inventory_Add" => request.Intent switch
            {
                AiHelpIntent.ExplainScreen =>
                    "This screen lets you add or adjust an inventory record for a specific part at a specific location/bin, including quantity on hand and basic stocking thresholds.",

                AiHelpIntent.ExplainFields =>
                    "Fields on this form:\n\n" +
                    "- Location/bin: which store or bin this stock belongs to.\n" +
                    "- Quantity on hand: the current physical count for this part at that location.\n" +
                    "- Reorder point: the level at which you want to trigger a replenishment.\n" +
                    "- Target quantity: the ideal stocked quantity when the location is topped up.",

                AiHelpIntent.ShowExamples =>
                    "Example uses:\n\n" +
                    "- After a cycle count, updating the quantity on hand to match reality.\n" +
                    "- Setting a reorder point so planners or systems can flag low stock.\n" +
                    "- Adding a new location when you start storing the part in a second crib or line-side bin.",

                AiHelpIntent.WhatFirst =>
                    "To use this form safely:\n\n" +
                    "1) Make sure you pick the correct location/bin for this part.\n" +
                    "2) Enter the true Quantity on hand based on your latest count.\n" +
                    "3) Choose a reasonable Reorder point and Target quantity that match how fast the part is used.\n" +
                    "4) Save, then review the Part Details page to confirm stock looks right across all locations.",

                _ => "Use this form to keep location-specific inventory accurate for this part."
            },

            "Bom_Add" => request.Intent switch
            {
                AiHelpIntent.ExplainScreen =>
                    "This screen links a part to an asset’s BOM. You choose which asset uses this part and how many units are required per asset.",

                AiHelpIntent.ExplainFields =>
                    "Fields on this form:\n\n" +
                    "- Asset: the equipment or asset that consumes this part.\n" +
                    "- Quantity per asset: how many units of this part are required for one asset (for example, 4 bearings per roller).",

                AiHelpIntent.ShowExamples =>
                    "Example uses:\n\n" +
                    "- Linking a standard bearing to every motor that uses it, with Quantity per asset = 2.\n" +
                    "- Adding a filter element to a compressor BOM with Quantity per asset = 1.\n" +
                    "- Building out BOMs for critical lines so you can see which assets are impacted if this part is out of stock.",

                AiHelpIntent.WhatFirst =>
                    "To use this safely:\n\n" +
                    "1) Pick the correct asset from the list — double-check the code and name.\n" +
                    "2) Set a realistic Quantity per asset that matches how the part is installed.\n" +
                    "3) Save, then review the Part Details → Used On Assets section to confirm the link looks correct.\n" +
                    "4) Repeat for other assets that share this part so usage and risk are visible.",

                _ => "Use this form to connect a part to the assets that depend on it, including how many units each asset needs."
            },

            "EM_Create" => request.Intent switch
            {
                AiHelpIntent.ExplainScreen =>
                    "This screen lets operators or users raise a new maintenance request. You capture where the problem is, how urgent it is, and a clear description so maintenance can respond effectively.",

                AiHelpIntent.ExplainFields =>
                    "Fields on this form:\n\n" +
                    "- Site: which site or location this request belongs to.\n" +
                    "- Area: the plant area, line, or department where the issue is.\n" +
                    "- Work Center: the specific line or cell affected.\n" +
                    "- Equipment: the machine or asset that has the problem.\n" +
                    "- Priority: how urgent it is (Low, Medium, High).\n" +
                    "- Requested by: who is raising the ticket (auto-filled).\n" +
                    "- Description: clear explanation of the issue, symptoms, and any steps already tried.",

                AiHelpIntent.ShowExamples =>
                    "Example good requests:\n\n" +
                    "- 'High – WF1 – Coater drive motor making grinding noise, louder at higher speeds, started this shift.'\n" +
                    "- 'Medium – Packaging line – printer skipping labels intermittently; power-cycled once, no change.'\n" +
                    "- 'Low – Guard hinge loose on inspection table; no impact yet but getting worse.'",

                AiHelpIntent.WhatFirst =>
                    "When raising a request:\n\n" +
                    "1) Fill in Site, Area, Work Center, and Equipment so maintenance can find the problem fast.\n" +
                    "2) Choose a Priority that reflects safety, quality, and downtime risk.\n" +
                    "3) In Description, write what you see, hear, or feel, plus any troubleshooting you already did.\n" +
                    "4) Submit once you’re confident someone else could find and understand the issue from your notes.",

                _ => "Use this form to submit a clear, actionable maintenance request so the right people can respond quickly."
            },


            // 🔹 Default / fallback for other pages
            _ => request.Intent switch
            {
                AiHelpIntent.ExplainScreen =>
                    $"This is the **{moduleKey}** screen. It shows information and actions related to this module.",

                AiHelpIntent.ExplainFields =>
                    "Each field captures a specific detail for this screen – look for names, statuses, timestamps, and any filters or actions along the top.",

                AiHelpIntent.ShowExamples =>
                    "Use this screen to review data, drill into a specific record, and trigger the main actions for this module (create, update, or resolve items).",

                AiHelpIntent.WhatFirst =>
                    "Start by scanning the top-left title and filters, then look at any badges or status indicators to see what needs attention first.",

                _ => "AI help is not configured for this page yet."
            }
        };

        return Task.FromResult(text);
    }
}
