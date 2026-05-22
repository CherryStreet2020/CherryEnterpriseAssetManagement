using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Voice;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Endpoints;

// Sprint 11 Voice MVP — POST /_voice/invoke endpoint.
//
// First production voice surface. The in-page voice client (wwwroot/js/voice-client.js)
// captures speech via webkitSpeechRecognition, POSTs the transcript to this
// endpoint, and the response is narrated back via SpeechSynthesisUtterance.
//
// Intent routing (Phase 1) is keyword-based. Three read-only intents:
//   1. EXPECTED_ARRIVALS — "what's arriving today" → IReceiptVoiceTools.ListExpectedArrivalsAsync
//   2. LOOKUP_RECEIPT    — "find receipt X"        → IReceiptVoiceTools.LookupReceiptAsync
//   3. EXPLAIN_EXCEPTION — "explain receipt X"     → LookupReceipt then ExplainExceptionAsync
//
// Mutating intents (ReceiveByVoice, QuarantineByVoice) and OCR (OcrParseMillCert)
// are deferred to Sprint 5 — they need additional UX (idempotency-key minting,
// confirm-before-commit flow) that's out of scope for the MVP.
//
// Every call writes an AuditLog row with ActorKind=AiOnBehalfOf and the
// full set of AI columns (AiSessionId, AiCommandText, AiModelVersion,
// AiToolName, AiConfidence). The tools themselves also write their own
// audit rows internally; this top-level row captures the *intent* and
// the human-language utterance.
//
// Reference: ADR-014 D1/D2/D3 + ADR-015 D10 + ADR-016 D8.
public static class VoiceInvokeEndpoint
{
    private const string ModelVersion = "voice-mvp/2026-05-19";

    public static IEndpointConventionBuilder MapVoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/_voice");

        // POST /_voice/invoke
        // Body: { transcript, aiSessionId, confidence?, voiceContext? }
        // Returns: { spoken, displayed?, actionLinks?, intent, ok }
        grp.MapPost("/invoke", InvokeAsync)
           .WithName("VoiceInvoke");

        return grp;
    }

    private static async Task<IResult> InvokeAsync(
        VoiceInvokeRequest req,
        HttpContext httpContext,
        IReceiptVoiceTools voiceTools,
        IHybridIntentRouter router,
        ITenantContext tenantContext,
        AppDbContext db,
        Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService chainOfCustody,
        ILogger<VoiceInvokeRequest> logger,
        CancellationToken ct)
    {
        // ---------- Input validation ----------
        if (req is null || string.IsNullOrWhiteSpace(req.Transcript))
        {
            return Results.BadRequest(VoiceInvokeResponse.Error(
                "I didn't catch that. Try again."));
        }

        var transcript = req.Transcript.Trim();
        if (transcript.Length > 500)
        {
            transcript = transcript[..500];
        }

        var aiSessionId = req.AiSessionId == Guid.Empty
            ? Guid.NewGuid()
            : req.AiSessionId;

        // ---------- Resolve user ----------
        // Per ADR-014 D6, the human is always the principal actor. If the
        // request is unauthenticated, refuse — voice can't act on its own.
        int? actorUserId = TryGetUserId(httpContext.User);
        var username = httpContext.User?.Identity?.Name ?? "anonymous";

        // ---------- Classify intent (HYBRID router — PR #3) ----------
        // Layer 1 keyword fast-path + Layer 2 vector cosine fallback.
        // See Services/Voice/HybridIntentRouter.cs for the design.
        //
        // Unauthenticated callers (tenantContext.TenantId == null) fall back to
        // TenantId=0 (the system tenant for intent prototypes). The OR-clause
        // in the vector query (TenantId == caller OR TenantId == 0) means the
        // worst case is matching the system prototypes twice, which is fine.
        var routed = await router.RouteAsync(transcript, tenantContext.TenantId ?? IntentPrototypes.SystemTenantId, ct);
        var intent = routed.Intent;

        // ---------- Build VoiceContext for audit trail ----------
        // Per-call confidence comes from the router, not the speech client —
        // the speech-recognition confidence on req.Confidence is unreliable.
        // We persist the router confidence so AuditLog can drive threshold
        // tuning later.
        var voiceContext = new VoiceContext(
            AiSessionId: aiSessionId,
            CommandText: transcript,
            ModelVersion: ModelVersion,
            Confidence: (decimal)Math.Round(routed.Confidence, 3));

        // ---------- Route to the right voice tool ----------
        VoiceInvokeResponse response;
        try
        {
            response = intent.Kind switch
            {
                IntentKind.ExpectedArrivals      => await HandleExpectedArrivalsAsync(voiceTools, intent, ct),
                IntentKind.LookupReceipt         => await HandleLookupReceiptAsync(voiceTools, intent, ct),
                IntentKind.ExplainException      => await HandleExplainExceptionAsync(voiceTools, intent, ct),
                IntentKind.ExplainChainOfCustody => await HandleExplainChainOfCustodyAsync(voiceTools, chainOfCustody, intent, ct),
                IntentKind.Help                  => HandleHelp(),
                _                                => HandleUnknown(transcript),
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Voice invoke failed for intent {Intent} (session {SessionId})",
                intent.Kind, aiSessionId);
            response = VoiceInvokeResponse.Error(
                "Something went wrong on my end. Try again in a moment.");
        }

        response = response with
        {
            Intent = intent.Kind.ToString(),
            AiSessionId = aiSessionId,
        };

        // ---------- Audit-log the invocation ----------
        // Tool methods write their own audit rows; this top-level row
        // captures the intent + the raw utterance + which router layer
        // settled the routing (Keyword/Vector/Fallback) (Purview pattern).
        await WriteAiAuditAsync(
            db, logger,
            action: $"Voice.Invoke.{intent.Kind}",
            actorUserId: actorUserId,
            username: username,
            voiceContext: voiceContext,
            description: TruncateForAudit(response.Spoken),
            // toolName carries router source AND reason (cosine distance for
            // vector hits, "keyword match" for Layer 1, the specific Fallback
            // cause when neither layer settled). Used for threshold tuning
            // from AuditLog. ~80 chars max — fits in AiToolName(255).
            toolName: TruncateToolName($"router:{routed.Source}:{routed.Reason ?? "n/a"}"),
            ct);

        return Results.Ok(response);
    }

    // ====================================================================
    // Intent handlers
    // ====================================================================

    private static async Task<VoiceInvokeResponse> HandleExpectedArrivalsAsync(
        IReceiptVoiceTools tools, ParsedIntent intent, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var result = await tools.ListExpectedArrivalsAsync(
            siteCode: null,
            windowStartUtc: now,
            windowEndUtc: now.AddDays(1),
            ct);

        if (result.IsFailure || result.Value is null)
        {
            return VoiceInvokeResponse.Error(
                $"I couldn't look up arrivals: {result.Error ?? "unknown error"}.");
        }

        var arrivals = result.Value;
        if (arrivals.Count == 0)
        {
            return new VoiceInvokeResponse
            {
                Ok = true,
                Spoken = "Nothing is expected to arrive in the next 24 hours.",
                Displayed = new VoiceDisplayed
                {
                    Title = "Expected arrivals — next 24h",
                    Lines = new[] { "No arrivals predicted." },
                },
            };
        }

        var top = arrivals.Take(3).ToList();
        var spokenLines = new List<string>
        {
            $"{arrivals.Count} arrival{(arrivals.Count == 1 ? "" : "s")} expected in the next 24 hours."
        };
        var displayedLines = new List<string>();
        foreach (var a in top)
        {
            var qty = a.ExpectedQuantity.ToString("0.##", CultureInfo.InvariantCulture);
            var uom = string.IsNullOrEmpty(a.Uom) ? "" : " " + a.Uom;
            var vendor = string.IsNullOrEmpty(a.VendorName) ? "an unknown vendor" : a.VendorName;
            var item = string.IsNullOrEmpty(a.ItemDescription) ? "(item TBD)" : a.ItemDescription;
            spokenLines.Add($"{a.Reference}: {qty}{uom} of {item} from {vendor}.");
            displayedLines.Add($"{a.Reference} • {qty}{uom} • {item} • {vendor}");
        }

        return new VoiceInvokeResponse
        {
            Ok = true,
            Spoken = string.Join(" ", spokenLines),
            Displayed = new VoiceDisplayed
            {
                Title = $"Expected arrivals — next 24h ({arrivals.Count})",
                Lines = displayedLines.ToArray(),
            },
            ActionLinks = new[]
            {
                new VoiceActionLink("Open PO Queue", "/Receiving?tab=po-queue"),
                new VoiceActionLink("Open ASN Queue", "/Receiving?tab=asn-queue"),
            },
        };
    }

    private static async Task<VoiceInvokeResponse> HandleLookupReceiptAsync(
        IReceiptVoiceTools tools, ParsedIntent intent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(intent.NaturalKey))
        {
            return new VoiceInvokeResponse
            {
                Ok = false,
                Spoken = "I need a receipt number, lot number, or serial number to look up.",
            };
        }

        var result = await tools.LookupReceiptAsync(intent.NaturalKey, null, ct);
        if (result.IsFailure || result.Value is null || result.Value.Count == 0)
        {
            return new VoiceInvokeResponse
            {
                Ok = false,
                Spoken = $"I couldn't find anything matching '{intent.NaturalKey}'.",
            };
        }

        var receipts = result.Value;
        var top = receipts[0];

        if (receipts.Count == 1)
        {
            var qty = top.QuantityReceived.ToString("0.##", CultureInfo.InvariantCulture);
            var uom = string.IsNullOrEmpty(top.Uom) ? "" : " " + top.Uom;
            return new VoiceInvokeResponse
            {
                Ok = true,
                Spoken = $"Found {top.ReceiptNumber}. " +
                         $"{qty}{uom}, status {top.Status}, received {top.ReceivedAt:MMM d}.",
                Displayed = new VoiceDisplayed
                {
                    Title = top.ReceiptNumber,
                    Lines = new[]
                    {
                        $"Quantity: {qty}{uom}",
                        $"Status: {top.Status}",
                        $"Received: {top.ReceivedAt:yyyy-MM-dd HH:mm}",
                        string.IsNullOrEmpty(top.SourcePoNumber) ? "Source PO: (orphan)" : $"Source PO: {top.SourcePoNumber}",
                    },
                },
                ActionLinks = new[]
                {
                    new VoiceActionLink("Open in Receipts admin", $"/Admin/StockReceipts/Edit/{top.Id}"),
                },
            };
        }

        var displayed = receipts.Take(5).Select(r =>
            $"{r.ReceiptNumber} • {r.Status} • {r.ReceivedAt:MMM d}").ToArray();

        return new VoiceInvokeResponse
        {
            Ok = true,
            Spoken = $"Found {receipts.Count} matches for '{intent.NaturalKey}'. The first is {top.ReceiptNumber}.",
            Displayed = new VoiceDisplayed
            {
                Title = $"{receipts.Count} matches for '{intent.NaturalKey}'",
                Lines = displayed,
            },
        };
    }

    private static async Task<VoiceInvokeResponse> HandleExplainExceptionAsync(
        IReceiptVoiceTools tools, ParsedIntent intent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(intent.NaturalKey))
        {
            return new VoiceInvokeResponse
            {
                Ok = false,
                Spoken = "Which receipt should I explain? Say its number or lot.",
            };
        }

        // Resolve receipt first.
        var lookup = await tools.LookupReceiptAsync(intent.NaturalKey, null, ct);
        if (lookup.IsFailure || lookup.Value is null || lookup.Value.Count == 0)
        {
            return new VoiceInvokeResponse
            {
                Ok = false,
                Spoken = $"I couldn't find a receipt matching '{intent.NaturalKey}'.",
            };
        }

        var receipt = lookup.Value[0];
        var result = await tools.ExplainExceptionAsync(receipt.Id, ct);
        if (result.IsFailure || result.Value is null)
        {
            return VoiceInvokeResponse.Error(
                result.Error ?? "I couldn't load the exception detail for that receipt.");
        }

        var ex = result.Value;
        var spokenParts = new List<string>
        {
            ex.Headline,
            ex.DetailedNarrative
        };
        if (ex.SuggestedActions.Count > 0)
        {
            spokenParts.Add("You can: " + string.Join(", or, ", ex.SuggestedActions) + ".");
        }

        return new VoiceInvokeResponse
        {
            Ok = true,
            Spoken = string.Join(" ", spokenParts),
            Displayed = new VoiceDisplayed
            {
                Title = ex.Headline,
                Lines = new[] { ex.DetailedNarrative }
                    .Concat(ex.SuggestedActions.Select(a => "→ " + a))
                    .ToArray(),
            },
            ActionLinks = new[]
            {
                new VoiceActionLink("Open receipt", $"/Admin/StockReceipts/Edit/{receipt.Id}"),
            },
        };
    }

    // Sprint 12D PR #5 / ADR-022 §D4 — chain-of-custody voice narration.
    //
    // EVS demo headline. Voice query "trace this receipt back to its source"
    // OR "chain of custody for RCPT-XXX" → AI narrates the chain via the
    // IChainOfCustodyService recursive-CTE traversal, paired visually with
    // the cytoscape.js viz (PR #4) on /Receiving/Details/{id}.
    //
    // Narration shape (template-driven, no LLM needed for demo headline —
    // a future PR can route the same data through an LLM for richer prose):
    //
    //   "Receipt RCPT-2026-1234 traces back through 4 nodes:
    //    received under PO-555, supplied by Vendor-7, contains item PART-A."
    //
    // Failure modes:
    //   - No natural key in utterance → ask the user to specify a receipt
    //   - Receipt not found via LookupReceiptAsync → friendly error
    //   - Chain query fails → friendly error + log the exception
    //   - Empty chain (0 hops) → "RCPT-XXX has no chain edges yet — try
    //     posting it via Receiving first"
    private static async Task<VoiceInvokeResponse> HandleExplainChainOfCustodyAsync(
        IReceiptVoiceTools tools,
        Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService chainOfCustody,
        ParsedIntent intent,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(intent.NaturalKey))
        {
            return new VoiceInvokeResponse
            {
                Ok = false,
                Spoken = "Which receipt should I trace? Say its number or lot.",
            };
        }

        // Resolve the receipt via the existing voice-tool lookup so we get
        // tenant-scoped resolution + the same "didn't find it" message shape
        // as the other intents.
        var lookup = await tools.LookupReceiptAsync(intent.NaturalKey, null, ct);
        if (lookup.IsFailure || lookup.Value is null || lookup.Value.Count == 0)
        {
            return new VoiceInvokeResponse
            {
                Ok = false,
                Spoken = $"I couldn't find a receipt matching '{intent.NaturalKey}'.",
            };
        }

        var receipt = lookup.Value[0];
        var chainResult = await chainOfCustody.GetUpstreamChainAsync(
            Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.Receipt,
            receipt.Id,
            maxDepth: 6,
            ct);

        if (chainResult.IsFailure || chainResult.Value is null)
        {
            return VoiceInvokeResponse.Error(
                chainResult.Error ?? "I couldn't load the chain of custody for that receipt.");
        }

        var chain = chainResult.Value;
        if (chain.Hops.Count == 0)
        {
            return new VoiceInvokeResponse
            {
                Ok = true,
                Spoken = $"Receipt {receipt.ReceiptNumber} has no chain of custody edges yet. " +
                         "The chain populates when the receipt posts through Receiving.",
                Displayed = new VoiceDisplayed
                {
                    Title = $"Chain of custody — {receipt.ReceiptNumber}",
                    Lines = new[] { "No chain edges recorded yet." },
                },
            };
        }

        // Build a natural-language narration from the hops. The root node
        // (depth=0) is the receipt itself; each subsequent hop describes a
        // relationship (RECEIVED_AT, SUPPLIED_BY, CONTAINS_ITEM, etc.).
        var nonRootHops = chain.Hops.Where(h => h.Depth > 0).ToList();
        var spokenParts = new List<string>
        {
            $"Receipt {receipt.ReceiptNumber} traces back through {nonRootHops.Count} " +
            $"node{(nonRootHops.Count == 1 ? "" : "s")}."
        };
        var displayLines = new List<string>
        {
            $"Receipt {receipt.ReceiptNumber}"
        };

        foreach (var hop in nonRootHops)
        {
            var phrase = NarrateEdge(hop.IncomingEdgeType, hop.NodeType, hop.Label);
            spokenParts.Add(phrase);
            displayLines.Add($"  → {hop.IncomingEdgeType ?? "linked to"}: {hop.NodeType} {hop.Label}");
        }

        return new VoiceInvokeResponse
        {
            Ok = true,
            Spoken = string.Join(" ", spokenParts),
            Displayed = new VoiceDisplayed
            {
                Title = $"Chain of custody — {receipt.ReceiptNumber}",
                Lines = displayLines.ToArray(),
            },
            ActionLinks = new[]
            {
                new VoiceActionLink("View graph", $"/Receiving/Details/{receipt.Id}#chain-of-custody"),
            },
        };
    }

    private static string NarrateEdge(string? edgeType, string nodeType, string label) =>
        edgeType switch
        {
            "RECEIVED_AT"    => $"Received under {nodeType.ToLower()} {label}.",
            "SUPPLIED_BY"    => $"Supplied by {nodeType.ToLower()} {label}.",
            "CONTAINS_ITEM"  => $"Contains item {label}.",
            "INSPECTED_BY"   => $"Inspected by IQC {label}.",
            "CERTIFIED_BY"   => $"Certified by {label}.",
            "MELTED_FROM"    => $"Melted from heat {label}.",
            "OF_MATERIAL"    => $"Of material master {label}.",
            "CARRIED_BY"     => $"Carried by {label}.",
            "PRODUCED_BY"    => $"Produced by {nodeType.ToLower()} {label}.",
            "CAPITALIZED_TO" => $"Capitalized to asset {label}.",
            "APPROVED_BY"    => $"Approved by {label}.",
            "POSTED_TO"      => $"Posted to {nodeType.ToLower()} {label}.",
            "INVOICES_FOR"   => $"Invoices for {nodeType.ToLower()} {label}.",
            "REVISION_OF"    => $"Revision of {label}.",
            _                => $"Linked to {nodeType.ToLower()} {label}.",
        };

    private static VoiceInvokeResponse HandleHelp() => new()
    {
        Ok = true,
        Spoken = "Try saying: what's arriving today, find receipt RCPT-2026-1234, explain receipt RCPT-2026-1234, or trace receipt RCPT-2026-1234.",
        Displayed = new VoiceDisplayed
        {
            Title = "Voice commands I understand today",
            Lines = new[]
            {
                "what's arriving today",
                "find receipt RCPT-YYYY-####",
                "explain receipt RCPT-YYYY-####",
                "trace receipt RCPT-YYYY-####  (chain of custody)",
            },
        },
    };

    private static VoiceInvokeResponse HandleUnknown(string transcript) => new()
    {
        Ok = false,
        Spoken = "I'm not set up for that yet. Try: what's arriving today, find receipt, explain a receipt, or trace a receipt.",
        Displayed = new VoiceDisplayed
        {
            Title = "I didn't understand that",
            Lines = new[]
            {
                $"Heard: \"{transcript}\"",
                "Try: what's arriving today",
                "Try: find receipt RCPT-...",
                "Try: explain receipt RCPT-...",
                "Try: trace receipt RCPT-...",
            },
        },
    };

    // ====================================================================
    // Helpers
    // ====================================================================

    private static int? TryGetUserId(ClaimsPrincipal? user)
    {
        if (user is null) return null;
        var v = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(v, out var id) ? id : null;
    }

    private static string TruncateForAudit(string? s, int max = 480)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s[..max];
    }

    /// <summary>
    /// Clamp the router-emitted toolName to fit AuditLog.AiToolName's column
    /// width (255 chars). Should always be well under; defensive against
    /// future Reason strings that grow.
    /// </summary>
    private static string TruncateToolName(string s) =>
        s.Length <= 200 ? s : s[..200];

    /// <summary>
    /// Voice-flavored audit logger. Wrapped in try/catch so an audit
    /// failure never blocks the user's voice round-trip.
    /// </summary>
    private static async Task WriteAiAuditAsync(
        AppDbContext db,
        ILogger logger,
        string action,
        int? actorUserId,
        string username,
        VoiceContext voiceContext,
        string description,
        string toolName,
        CancellationToken ct)
    {
        try
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Voice",
                EntityId = null,
                Action = action,
                Username = username,
                Timestamp = DateTime.UtcNow,
                Description = description,
                ActorKind = ActorKind.AiOnBehalfOf,
                OnBehalfOfUserId = actorUserId,
                AiSessionId = voiceContext.AiSessionId,
                AiCommandText = voiceContext.CommandText,
                AiModelVersion = voiceContext.ModelVersion,
                AiToolName = toolName,
                AiConfidence = voiceContext.Confidence,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Voice invoke audit write failed for action {Action}", action);
        }
    }
}

// ====================================================================
// DTOs
// ====================================================================

public sealed class VoiceInvokeRequest
{
    [Required]
    public string Transcript { get; set; } = string.Empty;

    public Guid AiSessionId { get; set; }

    /// <summary>
    /// 0..1 confidence from webkitSpeechRecognition's final result.
    /// Stored as decimal(4,3) on AuditLog.AiConfidence.
    /// </summary>
    public decimal? Confidence { get; set; }

    /// <summary>
    /// Optional voice context echoed back from the page meta tags
    /// (route, entityType, entityId, tab). Used for entity-scoped
    /// disambiguation in later sprints.
    /// </summary>
    public VoiceContextHint? VoiceContext { get; set; }
}

public sealed class VoiceContextHint
{
    public string? Route { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Tab { get; set; }
    public string? FocusedField { get; set; }
}

public sealed record VoiceInvokeResponse
{
    public bool Ok { get; init; }
    public string Spoken { get; init; } = string.Empty;
    public VoiceDisplayed? Displayed { get; init; }
    public VoiceActionLink[]? ActionLinks { get; init; }
    public string? Intent { get; init; }
    public Guid AiSessionId { get; init; }

    public static VoiceInvokeResponse Error(string spoken) => new()
    {
        Ok = false,
        Spoken = spoken,
    };
}

public sealed record VoiceDisplayed
{
    public string Title { get; init; } = string.Empty;
    public string[] Lines { get; init; } = Array.Empty<string>();
}

public sealed record VoiceActionLink(string Label, string Href);

// ====================================================================
// IntentKind, ParsedIntent, IntentClassifier MOVED 2026-05-21 to
// Services/Voice/IntentClassifier.cs so the new HybridIntentRouter can
// consume them from the Services namespace without crossing into
// Endpoints internals. PR #3 / ADR-021.
// ====================================================================
