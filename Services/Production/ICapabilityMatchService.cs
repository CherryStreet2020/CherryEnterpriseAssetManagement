// Theme B11 Wave R3-9 (2026-05-29) — Capability-match resolver (CLOSES Wave R3).
//
// THE PAYOFF of the capability model. R3-7 gave us what a resource CAN do
// (Capability + ResourceCapability); R3-8 gave us what an operation REQUIRES
// (OperationCapabilityRequirement). This service joins them: given a routing
// operation, return the resources ELIGIBLE to run it — those holding ALL the
// op's mandatory capabilities, each via a qualification that is current (active
// + not expired) at proficiency ≥ the requirement, satisfying any parametric
// envelope bound, on an Active resource. Ranked by proficiency (+ a bump for
// resources already on the op's work center).
//
// This is the function R4's finite scheduler calls to know WHO CAN RUN THIS OP
// — the disruption vs. Epicor/MIE/Plex, which pin a single machine on the
// routing step. The op says WHAT must be true; this resolver finds everyone who
// satisfies it, so the scheduler can pick by load / cost / health.
//
// Tenant scope: the op's OWN company (via Routing.CompanyId) — only resources in
// that company are candidates. No cross-company match.

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production
{
    /// <summary>One requirement's verdict for a candidate resource.</summary>
    public sealed record RequirementMatch(
        int RequirementId,
        string CapabilityCode,
        CapabilityRequirementType RequirementType,
        bool IsMandatory,
        bool Satisfied,
        string Detail);

    /// <summary>A candidate resource's eligibility for the operation.</summary>
    public sealed record ResourceEligibility(
        int ResourceId,
        string ResourceCode,
        string ResourceName,
        ResourceKind Kind,
        bool IsEligible,
        int Score,
        IReadOnlyList<RequirementMatch> Matches,
        string? IneligibleReason);

    /// <summary>Full match result for an operation: eligible (ranked) + ineligible (with reasons).</summary>
    public sealed record CapabilityMatchResult(
        int RoutingOperationId,
        string OperationDescription,
        DateTime AsOfUtc,
        int MandatoryRequirementCount,
        int CandidateResourceCount,
        IReadOnlyList<ResourceEligibility> Eligible,
        IReadOnlyList<ResourceEligibility> Ineligible);

    public interface ICapabilityMatchService
    {
        /// <summary>
        /// Return the resources eligible to run <paramref name="routingOperationId"/> —
        /// those satisfying ALL mandatory capability requirements (capability held,
        /// current as of <paramref name="asOfUtc"/>, proficiency ≥ required, envelope
        /// satisfied) on an Active resource — ranked by proficiency. Non-mandatory
        /// requirements rank/prefer but do not gate. Ineligible candidates are returned
        /// too, each with the first unmet mandatory requirement as the reason.
        /// </summary>
        Task<Result<CapabilityMatchResult>> GetEligibleResourcesAsync(
            int routingOperationId, DateTime? asOfUtc = null, CancellationToken ct = default);
    }
}
