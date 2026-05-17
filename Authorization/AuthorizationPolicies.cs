using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Abs.FixedAssets.Authorization;

// ADR-014 D5 — Resource-based authorization policies.
//
// Every action surfaced by the future voice-AI layer must run through
// IAuthorizationService.AuthorizeAsync(user, resource, policyName).
// This file is the central registration of all policy names + their
// requirements. Adding a new action = adding a policy here + an
// AuthorizeAsync call in the service method.
//
// Why centralized:
//   - One file to grep for "what can the AI invoke?" — auditors love it
//   - One place to enforce new cross-cutting policies (tenant isolation,
//     after-hours-only, etc.) without scattering changes
//
// AI never gets its own identity. The MCP server validates the user's
// bearer token, materializes their ClaimsPrincipal, and the policy
// check runs against the user — not against the AI.
//
// Phase F first-wave policies are stubs (RequireAuthenticatedUser).
// They get tightened to resource-based handlers (Owner-or-Admin,
// Tenant-scoped, Role-required) as Phase F screens ship.
//
// Reference: ADR-014 §"Decisions" D5.
public static class AuthorizationPolicies
{
    /// <summary>
    /// Register all Phase F authorization policies. Called from
    /// Program.cs via AddAuthorization.
    /// </summary>
    public static void Configure(AuthorizationOptions options)
    {
        // === MaterialMaster admin ===
        options.AddPolicy("MaterialMaster.View", p => p.RequireAuthenticatedUser());
        options.AddPolicy("MaterialMaster.Edit", p => p.RequireAuthenticatedUser());

        // === RegulatoryProfile admin ===
        options.AddPolicy("RegulatoryProfile.View", p => p.RequireAuthenticatedUser());
        options.AddPolicy("RegulatoryProfile.Edit", p => p.RequireAuthenticatedUser());

        // === Vendor extension ===
        options.AddPolicy("Vendor.Edit", p => p.RequireAuthenticatedUser());

        // === StockReceipt ===
        options.AddPolicy("StockReceipt.View", p => p.RequireAuthenticatedUser());
        options.AddPolicy("StockReceipt.Create", p => p.RequireAuthenticatedUser());
        options.AddPolicy("StockReceipt.Edit", p => p.RequireAuthenticatedUser());

        // === Remnant ===
        options.AddPolicy("Remnant.View", p => p.RequireAuthenticatedUser());
        options.AddPolicy("Remnant.Edit", p => p.RequireAuthenticatedUser());

        // === MaterialStructure ===
        options.AddPolicy("MaterialStructure.View", p => p.RequireAuthenticatedUser());
        options.AddPolicy("MaterialStructure.Edit", p => p.RequireAuthenticatedUser());
        options.AddPolicy("MaterialStructure.Approve", p => p.RequireAuthenticatedUser());

        // === ProductionOrder ===
        options.AddPolicy("ProductionOrder.View", p => p.RequireAuthenticatedUser());
        options.AddPolicy("ProductionOrder.Create", p => p.RequireAuthenticatedUser());
        options.AddPolicy("ProductionOrder.Release", p => p.RequireAuthenticatedUser());
        options.AddPolicy("ProductionOrder.Cancel", p => p.RequireAuthenticatedUser());

        // === CutListLine ===
        options.AddPolicy("CutListLine.View", p => p.RequireAuthenticatedUser());
        options.AddPolicy("CutListLine.Edit", p => p.RequireAuthenticatedUser());

        // === ProductionBatch ===
        options.AddPolicy("ProductionBatch.View", p => p.RequireAuthenticatedUser());
        options.AddPolicy("ProductionBatch.Create", p => p.RequireAuthenticatedUser());
        options.AddPolicy("ProductionBatch.Release", p => p.RequireAuthenticatedUser());
        options.AddPolicy("ProductionBatch.Hold", p => p.RequireAuthenticatedUser());
        options.AddPolicy("ProductionBatch.Quarantine", p => p.RequireAuthenticatedUser());
        options.AddPolicy("ProductionBatch.ReleaseAfterReview", p => p.RequireAuthenticatedUser());

        // === PurchaseOrder (D9/D10 voice-AI target) ===
        options.AddPolicy("PurchaseOrder.View", p => p.RequireAuthenticatedUser());
        options.AddPolicy("PurchaseOrder.Place", p => p.RequireAuthenticatedUser());
        options.AddPolicy("PurchaseRequisition.Create", p => p.RequireAuthenticatedUser());

        // === MRB disposition ===
        options.AddPolicy("MrbDisposition.Create", p => p.RequireAuthenticatedUser());
        options.AddPolicy("MrbDisposition.Approve", p => p.RequireAuthenticatedUser());
    }
}
