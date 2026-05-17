using System.Threading.Tasks;
using Abs.FixedAssets.Pages.Shared;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Services.Infrastructure;

// ADR-014 D1 — IAsyncPageFilter that emits per-request VoiceContextPayload.
//
// On every Razor page request, after the page handler runs and the
// model is fully populated, this filter calls BuildContextPayload()
// on the page model (if it inherits from VoiceReadyPageModel) and
// stores the result in HttpContext.Items["voice.ctx"].
//
// The future /_voice/context endpoint (Sprint 5) reads HttpContext.Items
// for the current request and serializes the payload to the in-page
// voice client.
//
// Legacy pages that don't inherit from VoiceReadyPageModel are skipped
// silently — no exception, no log spam. They just won't have voice
// context until they opt in.
//
// Registered in Program.cs:
//   builder.Services.AddRazorPages(o =>
//   {
//       o.Conventions.ConfigureFilter(new VoiceContextEmitter());
//   });
//
// Reference: ADR-014 §"Decisions" D1.
public sealed class VoiceContextEmitter : IAsyncPageFilter
{
    public const string HttpContextKey = "voice.ctx";

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        // Run the page handler first so the model is populated with
        // any loaded entity data.
        var executedContext = await next();

        // Only emit context for successful renders, not redirects or
        // exceptions. The voice client doesn't care about transient
        // 302 round-trips.
        if (executedContext.Exception is not null || executedContext.Canceled)
        {
            return;
        }

        // Only for pages inheriting from our base class.
        if (executedContext.HandlerInstance is not VoiceReadyPageModel page)
        {
            return;
        }

        var payload = page.BuildContextPayload();
        executedContext.HttpContext.Items[HttpContextKey] = payload;
    }
}
