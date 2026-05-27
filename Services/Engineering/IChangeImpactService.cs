using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;

namespace Abs.FixedAssets.Services.Engineering
{
    // ================================================================
    // Sprint 14.3 PR-7 (2026-05-27) — IChangeImpactService.
    //
    // CLOSED-LOOP impact analysis for Engineering Change Orders.
    // Walks the chain of custody:
    //   ECO → EcoLineItem → AffectedItemId
    //        → ProductionOrder (in-flight orders using that item)
    //        → Deviation / Waiver / Concession (active on that item)
    //        → CorrectiveActionRequest (open CARs on that item)
    //        → DocumentVersion (affected revisions)
    //        → FAI re-trigger (when ECO.RequiresFaiRetrigger = true)
    //        → Customer notice (when ECO.RequiresCustomerNotice = true)
    //
    // AS9100 §8.5.6 + IATF 16949 §8.5.6.1.
    // ================================================================

    public interface IChangeImpactService
    {
        /// <summary>
        /// Analyze the full blast radius of an ECO. Creates a ChangeImpactAnalysis
        /// with one ChangeImpactLine per affected entity discovered.
        /// Called automatically from IEcrEcoService.ReleaseEcoAsync when
        /// the ECO has RequiresFaiRetrigger or RequiresCustomerNotice flags.
        /// Can also be called manually from the admin probe.
        /// </summary>
        Task<Result<ChangeImpactAnalysis>> AnalyzeEcoImpactAsync(
            int ecoId,
            string analysisNumber,
            string analyzedBy,
            CancellationToken ct = default);

        /// <summary>
        /// Trigger FAI re-qualification for every affected item in the analysis.
        /// Creates one FaiReport per unique affected item with
        /// Reason = DesignChange or ProcessChange depending on ECR flags.
        /// </summary>
        Task<Result<ChangeImpactAnalysis>> TriggerFaiRetriggerAsync(
            int analysisId,
            string triggeredBy,
            CancellationToken ct = default);

        /// <summary>
        /// Mark an individual impact line as resolved with the action taken.
        /// </summary>
        Task<Result<ChangeImpactLine>> ResolveImpactLineAsync(
            int lineId,
            string actionTaken,
            string resolvedBy,
            CancellationToken ct = default);

        /// <summary>
        /// Get a single analysis by Id.
        /// </summary>
        Task<ChangeImpactAnalysis?> GetAnalysisAsync(
            int analysisId,
            CancellationToken ct = default);

        /// <summary>
        /// Get the analysis for a specific ECO (at most one per ECO).
        /// </summary>
        Task<ChangeImpactAnalysis?> GetAnalysisForEcoAsync(
            int ecoId,
            CancellationToken ct = default);
    }
}
