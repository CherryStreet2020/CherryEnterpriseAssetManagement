using System;
using System.Linq;
using System.Security.Claims;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Shared;

// ADR-014 D1 — Base PageModel exposing per-page voice-AI context.
//
// Every new Phase F page inherits from this. Override BuildContextPayload
// to add entity-specific fields:
//
//   public class WorkOrderDetailsModel : VoiceReadyPageModel
//   {
//       public WorkOrder Wo { get; private set; } = default!;
//
//       public override VoiceContextPayload BuildContextPayload()
//       {
//           var ctx = base.BuildContextPayload();
//           ctx.EntityType = nameof(WorkOrder);
//           ctx.EntityId   = Wo.Id.ToString();
//           ctx.RelatedIds = Wo.Operations.Select(o => o.Id.ToString()).ToArray();
//           return ctx;
//       }
//   }
//
// The IVoiceContextEmitter page filter calls BuildContextPayload()
// post-handler and stores the result in HttpContext.Items["voice.ctx"]
// for the voice client / MCP server to read.
//
// Legacy pages don't have to migrate immediately. New Phase F pages
// inherit this; legacy pages can opt in when touched.
//
// Reference: ADR-014 §"Decisions" D1.
public abstract class VoiceReadyPageModel : PageModel
{
    /// <summary>
    /// Build the voice-AI context payload for this page. Base
    /// implementation fills route + user + role + tenant. Pages
    /// override to add EntityType / EntityId / RelatedIds / Tab /
    /// FocusedField.
    /// </summary>
    public virtual VoiceContextPayload BuildContextPayload()
    {
        var user = User;

        return new VoiceContextPayload
        {
            Route        = HttpContext.Request.Path,
            UserId       = user.FindFirstValue(ClaimTypes.NameIdentifier),
            Roles        = user.Claims
                              .Where(c => c.Type == ClaimTypes.Role)
                              .Select(c => c.Value)
                              .ToArray(),
            TenantId     = user.FindFirstValue("tenant_id"),
            EntityType   = null,
            EntityId     = null,
            RelatedIds   = Array.Empty<string>(),
            FocusedField = HttpContext.Request.Query["focus"].ToString() is var f && f.Length > 0 ? f : null,
            Tab          = HttpContext.Request.Query["tab"].ToString() is var t && t.Length > 0 ? t : null,
            BuiltAt      = DateTime.UtcNow,
        };
    }
}
