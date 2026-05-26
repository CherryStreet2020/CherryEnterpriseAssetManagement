using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Controller;

// Sprint 12.7 PR #5 — CFO motion walkthrough page.
//
// Static script. The 8 steps live in code (no CMS surface — this is a
// teleprompter, not a content-managed page). All content is tenant-generic
// and pitched against the seeded demo placeholder (PWH MANUFACTURING
// CANADA). NO real customer names, locations, or people anywhere.
//
// Auth: any signed-in user can view.
[Authorize]
public sealed class WalkthroughModel : PageModel
{
    public IReadOnlyList<WalkthroughStep> Steps { get; } = new[]
    {
        new WalkthroughStep(
            Number:        1,
            Title:         "Open the Controller Cockpit",
            WhatYouSee:  "The Controller Control Center loads with the 4-tile KPI band live and the 4 tabs anchored. Cash position around $6M, AP danger-tone, 120+ open POs, ~$2.4M WIP — assuming the demo seeder has been run on this tenant.",
            WhatYouClick:  "Navigate via the side menu → Finance → Controller. OR paste /Controller into the address bar.",
            CherryLine:    "Welcome to the Controller Control Center. You're looking at the four numbers a CFO cares about most — cash, AP, committed spend, and work-in-progress — all live, all click-through to source.",
            OpenUrl:       "/Controller",
            OpenLabel:     "/Controller"),

        new WalkthroughStep(
            Number:        2,
            Title:         "Read the KPI band",
            WhatYouSee:  "Four hero tiles. Tone escalation on AP — danger red because outstanding is above the $200K threshold.",
            WhatYouClick:  "Point at the AP tile. Let the audience read it.",
            CherryLine:    "The AP-due-this-week tile turns red above $200,000 — same trigger you'd see in any enterprise-grade ledger, one click instead of three.",
            OpenUrl:       null,
            OpenLabel:     null),

        new WalkthroughStep(
            Number:        3,
            Title:         "Open the Drilldown tab",
            WhatYouSee:  "Tab shell switches to Drilldown. Empty queue + an asset/JE search box on the right.",
            WhatYouClick:  "Click the Drilldown tab in the Cockpit tab bar.",
            CherryLine:    "When a number looks wrong, the Drilldown tab walks you from any GL line back to the asset, project, or invoice that created it.",
            OpenUrl:       "/Controller?tab=drilldown",
            OpenLabel:     "Drilldown tab"),

        new WalkthroughStep(
            Number:        4,
            Title:         "Search for a sample asset",
            WhatYouSee:  "Drilldown chain trace renders: Asset → CipCapitalization → CipProject → CipCosts, with depreciation history at the bottom. Sample asset shows net book value, original cost, and the capital project that created it.",
            WhatYouClick:  "Type an asset reference like ASSET-1 into the search box (any asset that exists on this tenant). Hit Enter.",
            CherryLine:    "Pick any asset. Net book value, acquisition cost, capital project of origin, depreciation history — every monthly entry stamped with an AccountingKey since the cleanup-pass shipped, every one queryable by voice.",
            OpenUrl:       "/Controller?tab=drilldown",
            OpenLabel:     "Drilldown tab"),

        new WalkthroughStep(
            Number:        5,
            Title:         "Voice — \"Why is NBV on this asset?\"",
            WhatYouSee:  "Cherry Bar opens. Push-to-talk lights up. Cherry narrates the same chain trace aloud.",
            WhatYouClick:  "Hold Space (or click the Cherry button). Say \"Why is NBV on asset [number]\".",
            CherryLine:    "Cherry narrates net book value, accumulated depreciation, original cost, and the capital project lineage — naturally, in sentences, the way you'd brief a board member.",
            OpenUrl:       null,
            OpenLabel:     null),

        new WalkthroughStep(
            Number:        6,
            Title:         "Audit Trail tab",
            WhatYouSee:  "Audit Trail tab shows the depreciation journal entries with timestamps + user + before/after values.",
            WhatYouClick:  "Click the Audit Trail tab.",
            CherryLine:    "Every value Cherry just narrated is in the audit trail — who, when, before, after. The auditor's version of “show your work,” automated.",
            OpenUrl:       "/Controller?tab=audit-trail",
            OpenLabel:     "Audit Trail tab"),

        new WalkthroughStep(
            Number:        7,
            Title:         "Close-prep teaser",
            WhatYouSee:  "Close Prep tab — what's left to do this month. Period close gates, sub-ledger reconciliation status, JE approval queue.",
            WhatYouClick:  "Click the Close Prep tab. Don't dwell.",
            CherryLine:    "Month-end close, the checklist a controller actually keeps in their head. Two weeks from now this is where the CFO lives.",
            OpenUrl:       "/Controller?tab=close-prep",
            OpenLabel:     "Close Prep tab"),

        new WalkthroughStep(
            Number:        8,
            Title:         "Close — the offer",
            WhatYouSee:  "Back to /Controller. KPI band still red on AP.",
            WhatYouClick:  "Navigate back to /Controller home. Pause.",
            CherryLine:    "One screen, four numbers, voice-driven drill-down to source. Replaces three weeks of Excel pivots and four ad-hoc questions to your accounting team.",
            OpenUrl:       "/Controller",
            OpenLabel:     "/Controller"),
    };
}

public sealed record WalkthroughStep(
    int Number,
    string Title,
    string WhatYouSee,
    string WhatYouClick,
    string CherryLine,
    string? OpenUrl,
    string? OpenLabel);
