using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Abs.FixedAssets.TagHelpers;

// ADR-014 D7 — <voice-action> Tag Helper.
//
// Wraps a button (or any clickable element) and emits data-voice-*
// attributes the in-page voice client (Sprint 5) reads to know what's
// invocable on this page.
//
// Usage:
//
//   <voice-action
//       service="IWorkOrderService"
//       method="CloseAsync"
//       policy="WorkOrder.Close"
//       label="Close work order"
//       entity-id="@Model.Wo.Id">
//       <button type="submit" asp-page-handler="Close">Close</button>
//   </voice-action>
//
// Renders as:
//
//   <div data-voice-service="IWorkOrderService"
//        data-voice-method="CloseAsync"
//        data-voice-policy="WorkOrder.Close"
//        data-voice-label="Close work order"
//        data-voice-entity-id="42">
//       <button type="submit" ...>Close</button>
//   </div>
//
// CRITICAL: the server NEVER trusts these data-* attributes. The voice
// client uses them to discover invocable actions; on the inbound call,
// the service method re-validates `policy` against the resource using
// IAuthorizationService.AuthorizeAsync. This tag helper is a
// discoverability surface, NOT a security boundary.
//
// Reference: ADR-014 §"Decisions" D7.
[HtmlTargetElement("voice-action")]
public class VoiceActionTagHelper : TagHelper
{
    public string Service { get; set; } = "";
    public string Method { get; set; } = "";
    public string Policy { get; set; } = "";
    public string Label { get; set; } = "";

    [HtmlAttributeName("entity-id")]
    public string? EntityId { get; set; }

    [HtmlAttributeName("entity-type")]
    public string? EntityType { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // Render as a <div> wrapper. Inner content is the button.
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;

        output.Attributes.SetAttribute("data-voice-service", Service);
        output.Attributes.SetAttribute("data-voice-method", Method);
        output.Attributes.SetAttribute("data-voice-policy", Policy);
        output.Attributes.SetAttribute("data-voice-label", Label);

        if (!string.IsNullOrEmpty(EntityId))
        {
            output.Attributes.SetAttribute("data-voice-entity-id", EntityId);
        }
        if (!string.IsNullOrEmpty(EntityType))
        {
            output.Attributes.SetAttribute("data-voice-entity-type", EntityType);
        }

        // Add a CSS class so the in-page voice client can enumerate
        // them via document.querySelectorAll(".voice-action").
        var existingClass = output.Attributes.ContainsName("class")
            ? output.Attributes["class"].Value?.ToString() ?? ""
            : "";
        var newClass = string.IsNullOrEmpty(existingClass)
            ? "voice-action"
            : $"{existingClass} voice-action";
        output.Attributes.SetAttribute("class", newClass);
    }
}
