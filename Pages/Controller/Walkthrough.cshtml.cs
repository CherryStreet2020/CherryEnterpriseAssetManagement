using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Controller;

// Sprint 12.7 PR #5 — CFO motion walkthrough page.
//
// Static script. The steps live in code (not config) because the demo
// content is itself the deliverable — there's no need for a CMS-style
// editing surface for an 8-step teleprompter that runs at most once a
// week. If we end up running this demo 50+ times we can promote to a
// JSON config; today inline keeps the script under version control with
// the rest of the demo PR.
//
// Auth: any signed-in user can view. Production access is gated by the
// /Account/Login redirect at the framework level.
[Authorize]
public sealed class WalkthroughModel : PageModel
{
    public IReadOnlyList<WalkthroughStep> Steps { get; } = new[]
    {
        new WalkthroughStep(
            Number:        1,
            Title:         "Open the Controller Cockpit",
            WhatPaulSees:  "The Controller Control Center loads with the 4-tile KPI band live and the 4 tabs anchored. Cash position around $6M, AP danger-tone, 120+ open POs, ~$2.4M WIP.",
            WhatYouClick:  "Navigate via the side menu → Finance → Controller. OR paste /Controller into the address bar.",
            CherryLine:    "Welcome to the Controller Control Center, Paul. You're looking at the four numbers a CFO cares about most — cash, AP, committed spend, and work-in-progress — all live, all click-through to source.",
            OpenUrl:       "/Controller",
            OpenLabel:     "/Controller"),

        new WalkthroughStep(
            Number:        2,
            Title:         "Read the KPI band",
            WhatPaulSees:  "Four hero tiles. Tone escalation on AP — danger red because outstanding is above the $200K threshold.",
            WhatYouClick:  "Point at the AP tile. Let Paul read it.",
            CherryLine:    "The AP-due-this-week tile turns red above $200,000 — same trigger you'd see in NetSuite or D365, just one click instead of three.",
            OpenUrl:       null,
            OpenLabel:     null),

        new WalkthroughStep(
            Number:        3,
            Title:         "Open the Drilldown tab",
            WhatPaulSees:  "Tab shell switches to Drilldown. Empty queue + an asset/JE search box on the right.",
            WhatYouClick:  "Click the Drilldown tab in the Cockpit tab bar.",
            CherryLine:    "When a number looks wrong, the Drilldown tab walks you from any GL line back to the asset, project, or invoice that created it. Let me show you on Paul's favorite question.",
            OpenUrl:       "/Controller?tab=drilldown",
            OpenLabel:     "Drilldown tab"),

        new WalkthroughStep(
            Number:        4,
            Title:         "Search the hero asset",
            WhatPaulSees:  "Drilldown chain trace renders: Asset → CipCapitalization → CipProject → CipCosts, with depreciation history at the bottom.",
            WhatYouClick:  "Type ASSET-1116 in the search box. Hit Enter.",
            CherryLine:    "Asset 1116 — the Mazak. Net book value, $60,152. Acquired at $87,500 from a $250K capital project. Depreciation history below shows the monthly straight-line entries — every one stamped with an AccountingKey since PRA-5g, every one queryable by Cherry.",
            OpenUrl:       "/Controller?tab=drilldown&q=ASSET-1116",
            OpenLabel:     "Drilldown ASSET-1116"),

        new WalkthroughStep(
            Number:        5,
            Title:         "Voice — \"Why is NBV on asset 1116?\"",
            WhatPaulSees:  "Cherry Bar opens. Push-to-talk lights up. Cherry narrates the same chain trace aloud.",
            WhatYouClick:  "Hold Space (or click the Cherry button). Say \"Why is NBV on asset 1116\".",
            CherryLine:    "Asset 1116, the Mazak Integrex i-200. Net book value today is sixty thousand one hundred fifty-two dollars and eighty-six cents. That's eighty-seven thousand five hundred in original cost, less twenty-seven thousand three hundred forty-seven in accumulated depreciation. The asset traces back to capital project DEMO-CFO-CIP-001 at the Mississauga site.",
            OpenUrl:       null,
            OpenLabel:     null),

        new WalkthroughStep(
            Number:        6,
            Title:         "Audit Trail tab",
            WhatPaulSees:  "Audit Trail tab shows the depreciation journal entries with timestamps + user + before/after values.",
            WhatYouClick:  "Click the Audit Trail tab.",
            CherryLine:    "Every value Cherry just narrated is in the audit trail — who, when, before, after. The auditor's version of \"show your work,\" automated.",
            OpenUrl:       "/Controller?tab=audit-trail",
            OpenLabel:     "Audit Trail tab"),

        new WalkthroughStep(
            Number:        7,
            Title:         "Close-prep teaser",
            WhatPaulSees:  "Close Prep tab — what's left to do this month. Period close gates, sub-ledger reconciliation status, JE approval queue.",
            WhatYouClick:  "Click the Close Prep tab. Don't dwell; Paul knows the shape.",
            CherryLine:    "Month-end close, the checklist a controller actually keeps in their head. Two weeks from now this is where Paul lives.",
            OpenUrl:       "/Controller?tab=close-prep",
            OpenLabel:     "Close Prep tab"),

        new WalkthroughStep(
            Number:        8,
            Title:         "Close — the offer",
            WhatPaulSees:  "Back to /Controller. KPI band still red on AP.",
            WhatYouClick:  "Navigate back to /Controller home. Pause.",
            CherryLine:    "Paul — one screen, four numbers, voice-driven drill-down to source. Replaces three weeks of Excel pivots and four ad-hoc questions to your accounting team. Want to put this on your books next quarter?",
            OpenUrl:       "/Controller",
            OpenLabel:     "/Controller"),
    };
}

public sealed record WalkthroughStep(
    int Number,
    string Title,
    string WhatPaulSees,
    string WhatYouClick,
    string CherryLine,
    string? OpenUrl,
    string? OpenLabel);
