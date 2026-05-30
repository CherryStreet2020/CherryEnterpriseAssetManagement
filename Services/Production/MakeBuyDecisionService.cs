// Theme B7 Wave C PR-8 (2026-05-29) — IMakeBuyDecisionService impl. Design in the interface file.
//
// Loads the item + its policy, the make routing's work centers, and the best valid supplier
// quote; scores F2–F6 in C#; applies the hard gates; aggregates BuyScore; emits an
// explainable rationale; and (optionally) persists a MakeBuyDecision. F2 capacity reads the
// real R4-10 IResourceLoadService Load%. EF discipline: flat projections, math in C#.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production
{
    public sealed class MakeBuyDecisionService : IMakeBuyDecisionService
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly IResourceLoadService _load;
        private readonly ILogger<MakeBuyDecisionService> _log;

        private const decimal FreightReceivingUpliftPct = 5m;   // landed-buy adder on the quoted unit price
        private const int VendorTransitInspectionDays = 3;       // added to the quote lead time for F5

        public MakeBuyDecisionService(AppDbContext db, ITenantContext tenant,
            IResourceLoadService load, ILogger<MakeBuyDecisionService> log)
        {
            _db = db; _tenant = tenant; _load = load; _log = log;
        }

        public async Task<Result<MakeBuyDecisionResult>> DecideAsync(
            int itemId, decimal qty, DateTime? dueDate, int? siteId,
            MakeBuyDecisionContext context, bool persist, CancellationToken ct = default)
        {
            if (qty <= 0m) return Result.Failure<MakeBuyDecisionResult>("Quantity must be positive.");

            var item = await _db.Items
                .Where(i => i.Id == itemId)
                .Select(i => new { i.Id, i.CompanyId, i.MakeBuyCode, i.MakeBuyPolicy, i.IsSourceControlled, i.SourceControlReason, i.PartNumber, i.Description })
                .FirstOrDefaultAsync(ct);
            if (item == null) return Result.Failure<MakeBuyDecisionResult>($"Item #{itemId} not found.");
            if (item.CompanyId != null && !_tenant.VisibleCompanyIds.Contains(item.CompanyId.Value))
                return Result.Failure<MakeBuyDecisionResult>("Item is not in your tenant scope.");
            var companyId = item.CompanyId ?? _tenant.VisibleCompanyIds.FirstOrDefault();

            var policy = await ResolvePolicyAsync(companyId, siteId, ct);
            var (makeAllowed, buyAllowed) = ResolveAllowedPaths(item.MakeBuyCode, item.MakeBuyPolicy);

            var due = dueDate ?? DateTime.UtcNow.Date.AddDays(30);
            var now = DateTime.UtcNow;

            // ── Make side: routing + its work centers ───────────────────────────
            var routing = await _db.Routings
                .Where(r => r.ItemId == itemId)
                .OrderByDescending(r => r.IsDefault).ThenByDescending(r => r.Id)
                .Select(r => new { r.Id, r.CompanyId })
                .FirstOrDefaultAsync(ct);
            var routingOps = routing == null ? new List<RoutingOpInfo>() : await _db.RoutingOperations
                .Where(o => o.RoutingId == routing.Id)
                .Select(o => new RoutingOpInfo(o.WorkCenterId, o.SetupTimeMins, o.RunTimePerUnitMins,
                    o.QueueTimeMins, o.MoveTimeMins, o.WaitTimeMins))
                .ToListAsync(ct);
            var makeFeasible = makeAllowed && routing != null && routingOps.Count > 0;

            // ── Make cost (fully loaded, per unit × qty) ────────────────────────
            // Effective-dated + site cascade: only rows live today; one per ElementType,
            // preferring a site-specific override over the item-level row, newest first.
            var costRows = await _db.Set<ItemStandardCostElement>()
                .Where(c => c.ItemId == itemId && c.IsActive
                    && c.EffectiveFromUtc <= now && (c.EffectiveToUtc == null || c.EffectiveToUtc > now)
                    && (c.SiteId == null || c.SiteId == siteId))
                .Select(c => new { c.ElementType, c.Amount, c.SiteId, c.EffectiveFromUtc })
                .ToListAsync(ct);
            var pickedCost = costRows
                .GroupBy(c => c.ElementType)
                .Select(g => g.OrderByDescending(x => x.SiteId != null).ThenByDescending(x => x.EffectiveFromUtc).First())
                .ToList();
            decimal makeUnitFull = pickedCost.Sum(c => c.Amount);
            decimal? makeCostFull = makeUnitFull > 0m ? makeUnitFull * qty : (decimal?)null;

            // ── Buy side: best valid supplier quote for the item ────────────────
            var bestQuote = await BestQuoteAsync(itemId, now.Date, ct);
            var buyFeasible = buyAllowed && bestQuote != null;
            decimal? buyUnitLanded = bestQuote == null ? (decimal?)null
                : bestQuote.UnitPrice * (1m + FreightReceivingUpliftPct / 100m);
            decimal? buyCostLanded = buyUnitLanded == null ? (decimal?)null : buyUnitLanded.Value * qty;

            // ── F2 capacity — real R4 Load% across the routing's work centers ───
            string? drumCode = null; decimal? drumLoad = null;
            var distinctWcs = routingOps.Select(o => o.WorkCenterId).Where(w => w > 0).Distinct().ToList();
            foreach (var wcId in distinctWcs)
            {
                var lr = await _load.GetProjectedLoadAsync(ResourceLoadTargetKind.WorkCenter, wcId, now, due, ct);
                if (lr.IsSuccess && (drumLoad == null || lr.Value.LoadPct > drumLoad))
                { drumLoad = lr.Value.LoadPct; drumCode = lr.Value.Code; }
            }
            bool routedThroughDrum = drumLoad != null && drumLoad.Value >= policy.CapacityThresholdPct;
            decimal f2 = drumLoad == null ? 0.30m : Clamp01(drumLoad.Value / 130m);
            string f2reason = drumLoad == null
                ? "no routing work centers to load — capacity favors neither"
                : $"bottleneck {drumCode} projected {drumLoad.Value:0.#}% over [today, due]" + (routedThroughDrum ? " (drum)" : "");

            // One-time make setup cost (per ORDER) = Σ routing setup-time × the WC's standard rate.
            // This is the genuine fixed cost the break-even amortizes against (cost elements are per-unit).
            var wcRates = distinctWcs.Count == 0 ? new Dictionary<int, decimal>()
                : (await _db.WorkCenters.Where(w => distinctWcs.Contains(w.Id))
                    .Select(w => new { w.Id, w.StandardCostRatePerHour }).ToListAsync(ct))
                    .ToDictionary(w => w.Id, w => w.StandardCostRatePerHour);
            decimal fixedSetupOrder = routingOps.Sum(o =>
                o.SetupMins / 60m * (wcRates.TryGetValue(o.WorkCenterId, out var rate) ? rate : 0m));

            // ── F3 cost delta ───────────────────────────────────────────────────
            decimal f3; string f3reason;
            if (makeCostFull == null || buyCostLanded == null)
            { f3 = 0.5m; f3reason = makeCostFull == null ? "no standard cost on the item" : "no supplier quote"; }
            else
            {
                var ratio = (makeCostFull.Value - buyCostLanded.Value) / makeCostFull.Value; // + = buy cheaper
                f3 = Clamp01(0.5m + ratio);
                f3reason = $"make ${makeCostFull.Value:N0} vs landed buy ${buyCostLanded.Value:N0} ({(ratio >= 0 ? "buy" : "make")} cheaper by {Math.Abs(ratio) * 100m:0.#}%)";
            }

            // ── F4 break-even (units): one-time make setup ÷ per-unit buy premium ──
            decimal f4; string f4reason;
            if (buyUnitLanded == null || makeUnitFull <= 0m)
            { f4 = 0.5m; f4reason = buyUnitLanded == null ? "no quote to break-even against" : "no standard cost to break-even against"; }
            else if (buyUnitLanded.Value <= makeUnitFull)
            { f4 = 0.9m; f4reason = $"buy unit ${buyUnitLanded.Value:N2} ≤ make unit ${makeUnitFull:N2} — making only adds ${fixedSetupOrder:N0} one-time setup, so buy wins at any volume"; }
            else
            {
                var be = fixedSetupOrder / (buyUnitLanded.Value - makeUnitFull);
                f4 = Clamp01(be / (be + qty));
                f4reason = $"break-even ≈ {be:0.#} units (one-time make setup ${fixedSetupOrder:N0} ÷ per-unit buy premium ${buyUnitLanded.Value - makeUnitFull:N2}); this order {qty:0.#} {(qty >= be ? "≥" : "<")} BE — favors {(qty >= be ? "make" : "buy")}";
            }

            // ── F5 lead-time fit ────────────────────────────────────────────────
            decimal totalMakeMins = routingOps.Sum(o => o.SetupMins + o.RunPerUnitMins * qty + o.QueueMins + o.MoveMins + o.WaitMins);
            DateTime? makeComplete = makeFeasible ? AddBusinessDays(now.Date, (int)Math.Ceiling((double)totalMakeMins / (8.0 * 60.0))) : (DateTime?)null;
            DateTime? vendorDelivery = bestQuote == null ? (DateTime?)null : now.Date.AddDays(bestQuote.LeadTimeDays + VendorTransitInspectionDays);
            decimal f5; string f5reason;
            bool buyMeets = vendorDelivery != null && vendorDelivery <= due;
            bool makeMeets = makeComplete != null && makeComplete <= due;
            if (buyMeets && !makeMeets) { f5 = 0.9m; f5reason = $"buy delivers {vendorDelivery:yyyy-MM-dd} (inside due) but make finishes {makeComplete:yyyy-MM-dd} (late)"; }
            else if (makeMeets && !buyMeets) { f5 = 0.1m; f5reason = $"make finishes {makeComplete:yyyy-MM-dd} (inside due) but buy delivers {vendorDelivery:yyyy-MM-dd} (late)"; }
            else if (vendorDelivery != null && makeComplete != null) { f5 = vendorDelivery <= makeComplete ? 0.6m : 0.4m; f5reason = $"both within reach — buy {vendorDelivery:yyyy-MM-dd}, make {makeComplete:yyyy-MM-dd}"; }
            else { f5 = 0.5m; f5reason = "incomplete lead-time data"; }

            // ── F6 quality / risk ───────────────────────────────────────────────
            decimal f6; string f6reason;
            if (bestQuote == null) { f6 = 0.2m; f6reason = "no approved supplier — buy carries sourcing risk"; }
            else { var otd = bestQuote.OnTimeDeliveryPct ?? 80m; f6 = Clamp01(otd / 100m); f6reason = $"supplier OTD {otd:0.#}%"; }

            // ── Assemble factors + weighted aggregate ───────────────────────────
            var factors = new List<MakeBuyFactor>
            {
                Factor("F2", "Capacity",   f2, policy.WeightCapacity,   f2reason),
                Factor("F3", "Cost delta", f3, policy.WeightCostDelta,  f3reason),
                Factor("F4", "Break-even", f4, policy.WeightBreakEven,  f4reason),
                Factor("F5", "Lead time",  f5, policy.WeightLeadTime,   f5reason),
                Factor("F6", "Quality/risk", f6, policy.WeightQualityRisk, f6reason),
            };
            decimal weightSum = factors.Sum(f => f.Weight);
            decimal buyScore = weightSum > 0m ? factors.Sum(f => f.WeightedImpact) / weightSum : 0.5m;

            // ── Hard gates (override the score) ─────────────────────────────────
            // A forced-MAKE item with no routing is still MADE (policy/source-control demands it),
            // but we flag that it isn't yet producible so a planner adds a routing.
            const string NoRoutingCaveat = " ⚠ No routing/operations yet — make is not currently feasible; add a routing.";
            MakeBuyOutcome outcome; bool gated = false; string? gateReason = null;
            if (item.IsSourceControlled)
            { outcome = MakeBuyOutcome.Make; gated = true; gateReason = $"Source-controlled item — must be made in-house{(string.IsNullOrWhiteSpace(item.SourceControlReason) ? "" : $" ({item.SourceControlReason})")}." + (makeFeasible ? "" : NoRoutingCaveat); }
            else if (!makeAllowed && !buyAllowed)
            { return Result.Failure<MakeBuyDecisionResult>("Item policy allows neither make nor buy."); }
            else if (makeAllowed && !buyAllowed)
            { outcome = MakeBuyOutcome.Make; gated = true; gateReason = "Policy allows make only." + (makeFeasible ? "" : NoRoutingCaveat); }
            else if (buyAllowed && !makeAllowed)
            {
                if (!buyFeasible) return Result.Failure<MakeBuyDecisionResult>("Policy is buy-only but no valid supplier quote exists.");
                outcome = MakeBuyOutcome.Buy; gated = true; gateReason = "Policy allows buy only.";
            }
            else if (makeFeasible && !buyFeasible)
            { outcome = MakeBuyOutcome.Make; gated = true; gateReason = "Buy not feasible (no valid quote) — make is the only path."; }
            else if (buyFeasible && !makeFeasible)
            { outcome = MakeBuyOutcome.Buy; gated = true; gateReason = "Make not feasible (no routing) — buy is the only path."; }
            else if (routedThroughDrum && buyFeasible && WithinDrumTolerance(makeCostFull, buyCostLanded, policy.DrumOffloadCostTolerancePct))
            { outcome = MakeBuyOutcome.Buy; gated = true; gateReason = $"Drum offload — make routes through {drumCode} at {drumLoad:0.#}% (≥ {policy.CapacityThresholdPct:0}%) and buy is within {policy.DrumOffloadCostTolerancePct:0.#}% cost tolerance."; }
            else
            {
                if (buyScore > policy.BuyDecisionScoreThreshold) outcome = MakeBuyOutcome.Buy;
                else if (buyScore < policy.BuyDecisionScoreThreshold) outcome = MakeBuyOutcome.Make;
                else outcome = routedThroughDrum ? MakeBuyOutcome.Buy
                    : (policy.FinalTieBreak == MakeBuyTieBreak.PreferBuy ? MakeBuyOutcome.Buy : MakeBuyOutcome.Make);
            }

            // ── Confidence + rationale ──────────────────────────────────────────
            decimal dataBonus = (makeCostFull != null ? 0.1m : 0m) + (buyCostLanded != null ? 0.1m : 0m) + (drumLoad != null ? 0.1m : 0m);
            decimal confidence = gated ? 0.95m : Clamp01(Math.Abs(buyScore - policy.BuyDecisionScoreThreshold) * 2m + dataBonus);
            var rationale = BuildRationale(outcome, gated, gateReason, factors, buyScore, policy.BuyDecisionScoreThreshold);

            int? chosenSupplier = outcome == MakeBuyOutcome.Buy ? bestQuote?.VendorId : null;
            int? chosenQuote = outcome == MakeBuyOutcome.Buy ? bestQuote?.QuoteId : null;

            int? persistedId = null;
            if (persist)
            {
                var decision = new MakeBuyDecision
                {
                    CompanyId = companyId, SiteIdSnapshot = siteId, ItemId = itemId, Qty = qty, DueDate = due,
                    DecidedAtUtc = now, Context = context, SourceType = "MakeBuyDecisionService",
                    Outcome = outcome, BuyScore = Math.Round(buyScore, 4), Confidence = Math.Round(confidence, 4),
                    WasHardGated = gated, HardGateReason = gateReason, RationaleText = rationale,
                    FactorBreakdown = JsonSerializer.Serialize(factors.Select(f => new {
                        code = f.Code, label = f.Label, score = f.Score, weight = f.Weight,
                        weightedImpact = f.WeightedImpact, reason = f.Reason })),
                    MakeCostFullyLoaded = makeCostFull, BuyCostLanded = buyCostLanded,
                    BottleneckWorkCenterCode = drumCode, BottleneckLoadPct = drumLoad, RoutedThroughDrum = routedThroughDrum,
                    MakeCompletionDate = makeComplete, VendorDeliveryDate = vendorDelivery,
                    ChosenSupplierId = chosenSupplier, ChosenQuoteId = chosenQuote,
                    CreatedBy = "MakeBuyDecisionService",
                };
                _db.MakeBuyDecisions.Add(decision);
                await _db.SaveChangesAsync(ct);
                persistedId = decision.Id;
            }

            return Result.Success(new MakeBuyDecisionResult(
                persistedId, itemId, qty, due, outcome, Math.Round(buyScore, 4), Math.Round(confidence, 4),
                gated, gateReason, rationale, factors,
                makeCostFull, buyCostLanded, drumCode, drumLoad, routedThroughDrum,
                makeComplete, vendorDelivery, chosenSupplier, chosenQuote));
        }

        public async Task<Result<MakeBuyDecisionResult>> ExplainAsync(int decisionId, CancellationToken ct = default)
        {
            var d = await _db.MakeBuyDecisions.FirstOrDefaultAsync(x => x.Id == decisionId, ct);
            if (d == null) return Result.Failure<MakeBuyDecisionResult>($"Decision #{decisionId} not found.");
            if (!_tenant.VisibleCompanyIds.Contains(d.CompanyId))
                return Result.Failure<MakeBuyDecisionResult>("Decision is not in your tenant scope.");

            var factors = new List<MakeBuyFactor>();
            if (!string.IsNullOrWhiteSpace(d.FactorBreakdown))
            {
                try
                {
                    using var doc = JsonDocument.Parse(d.FactorBreakdown);
                    foreach (var e in doc.RootElement.EnumerateArray())
                        factors.Add(new MakeBuyFactor(
                            e.GetProperty("code").GetString() ?? "", e.GetProperty("label").GetString() ?? "",
                            e.GetProperty("score").GetDecimal(), e.GetProperty("weight").GetDecimal(),
                            e.GetProperty("weightedImpact").GetDecimal(), e.GetProperty("reason").GetString() ?? ""));
                }
                catch (JsonException) { /* tolerate legacy/blank breakdowns */ }
            }

            return Result.Success(new MakeBuyDecisionResult(
                d.Id, d.ItemId, d.Qty, d.DueDate, d.Outcome, d.BuyScore, d.Confidence,
                d.WasHardGated, d.HardGateReason, d.RationaleText ?? "", factors,
                d.MakeCostFullyLoaded, d.BuyCostLanded, d.BottleneckWorkCenterCode, d.BottleneckLoadPct,
                d.RoutedThroughDrum, d.MakeCompletionDate, d.VendorDeliveryDate, d.ChosenSupplierId, d.ChosenQuoteId));
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private async Task<PolicyCfg> ResolvePolicyAsync(int companyId, int? siteId, CancellationToken ct)
        {
            var p = await _db.MakeBuyDecisionPolicies
                .Where(x => x.CompanyId == companyId && x.IsActive && (x.SiteId == siteId || x.SiteId == null))
                .OrderByDescending(x => x.SiteId != null).ThenBy(x => x.Id)
                .FirstOrDefaultAsync(ct);
            if (p == null)
                return new PolicyCfg(85m, 8m, 0.50m, 0.25m, 0.30m, 0.10m, 0.20m, 0.15m, MakeBuyTieBreak.PreferMake);
            return new PolicyCfg(p.CapacityThresholdPct, p.DrumOffloadCostTolerancePct, p.BuyDecisionScoreThreshold,
                p.WeightCapacity, p.WeightCostDelta, p.WeightBreakEven, p.WeightLeadTime, p.WeightQualityRisk, p.FinalTieBreak);
        }

        private static (bool make, bool buy) ResolveAllowedPaths(MakeBuyCode code, MakeBuyPolicy policy)
        {
            var effective = policy != MakeBuyPolicy.Inherit ? policy : code switch
            {
                MakeBuyCode.Make => MakeBuyPolicy.MakeOnly,
                MakeBuyCode.Buy => MakeBuyPolicy.BuyOnly,
                MakeBuyCode.MakeOrBuy => MakeBuyPolicy.MakeOrBuy,
                _ => MakeBuyPolicy.MakeOnly, // Phantom → made/exploded
            };
            return effective switch
            {
                MakeBuyPolicy.MakeOnly => (true, false),
                MakeBuyPolicy.BuyOnly => (false, true),
                _ => (true, true), // MakeOrBuy / MakeWithBuyOverflow / BuyWithMakeBackup
            };
        }

        private async Task<QuoteInfo?> BestQuoteAsync(int itemId, DateTime today, CancellationToken ct)
        {
            // RFQ lines for this item → their quote lines (per-unit) on valid, usable quotes.
            var rows = await (
                from rl in _db.Set<SupplierRFQLine>()
                where rl.ItemId == itemId
                join ql in _db.Set<SupplierQuoteLine>() on rl.Id equals ql.SupplierRFQLineId
                join q in _db.Set<SupplierQuote>() on ql.SupplierQuoteId equals q.Id
                where (q.Status == SupplierQuoteStatus.Received || q.Status == SupplierQuoteStatus.Shortlisted || q.Status == SupplierQuoteStatus.Awarded)
                      && (q.ValidUntilDate == null || q.ValidUntilDate >= today)
                      && ql.QuotedUnitPrice > 0m
                select new QuoteInfo(q.Id, q.VendorId, ql.QuotedUnitPrice, ql.LeadTimeDays > 0 ? ql.LeadTimeDays : q.LeadTimeDays, q.SupplierOnTimeDeliveryPct))
                .ToListAsync(ct);
            return rows.OrderBy(r => r.UnitPrice).FirstOrDefault();
        }

        private static bool WithinDrumTolerance(decimal? makeCost, decimal? buyCost, decimal tolerancePct)
        {
            if (makeCost == null || buyCost == null || makeCost.Value <= 0m) return buyCost != null; // no make cost → offload ok
            var premium = (buyCost.Value - makeCost.Value) / makeCost.Value * 100m;
            return premium <= tolerancePct;
        }

        private static MakeBuyFactor Factor(string code, string label, decimal score, decimal weight, string reason) =>
            new(code, label, Math.Round(score, 4), weight, Math.Round(score * weight, 4), reason);

        private static string BuildRationale(MakeBuyOutcome outcome, bool gated, string? gateReason,
            List<MakeBuyFactor> factors, decimal buyScore, decimal threshold)
        {
            var sb = new StringBuilder();
            sb.Append(outcome.ToString().ToUpperInvariant());
            if (gated) { sb.Append(" — ").Append(gateReason); return sb.ToString(); }
            sb.Append($" — buy score {buyScore:0.00} vs threshold {threshold:0.00}. ");
            var top = factors.OrderByDescending(f => Math.Abs(f.Score - 0.5m) * f.Weight).Take(3);
            sb.Append(string.Join("; ", top.Select(f => $"{f.Label}: {f.Reason}")));
            sb.Append('.');
            return sb.ToString();
        }

        private static DateTime AddBusinessDays(DateTime start, int days)
        {
            var d = start;
            while (days > 0)
            {
                d = d.AddDays(1);
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) days--;
            }
            return d;
        }

        private static decimal Clamp01(decimal v) => v < 0m ? 0m : (v > 1m ? 1m : v);

        // B7 Wave D PR-2 — resolve a spoken item reference → latest decision → ExplainAsync.
        // The Cherry Bar voice handler stays thin; all the data work lives here (ADR-025),
        // tenant-scoped to the item's company throughout.
        public async Task<Result<MakeBuyVoiceExplanation>> ExplainLatestForItemAsync(
            string itemRef, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(itemRef))
                return Result.Failure<MakeBuyVoiceExplanation>("No item specified.");
            itemRef = itemRef.Trim();
            var lower = itemRef.ToLower();

            var scoped = _db.Items.Where(i => _tenant.VisibleCompanyIds.Contains(i.CompanyId ?? 0));

            int? itemId = null;
            string? partNumber = null;
            string? description = null;

            // Numeric reference → try item Id first.
            if (int.TryParse(itemRef, out var rawId))
            {
                var byId = await scoped.Where(i => i.Id == rawId)
                    .Select(i => new { i.Id, i.PartNumber, i.Description })
                    .FirstOrDefaultAsync(ct);
                if (byId != null) { itemId = byId.Id; partNumber = byId.PartNumber; description = byId.Description; }
            }

            // Otherwise (or if the numeric id missed) resolve by part number — exact, then prefix.
            if (itemId == null)
            {
                var byPn = await scoped.Where(i => i.PartNumber.ToLower() == lower)
                    .Select(i => new { i.Id, i.PartNumber, i.Description })
                    .FirstOrDefaultAsync(ct)
                    ?? await scoped.Where(i => i.PartNumber.ToLower().StartsWith(lower))
                        .Select(i => new { i.Id, i.PartNumber, i.Description })
                        .FirstOrDefaultAsync(ct);
                if (byPn != null) { itemId = byPn.Id; partNumber = byPn.PartNumber; description = byPn.Description; }
            }

            if (itemId == null)
                return Result.Failure<MakeBuyVoiceExplanation>(
                    $"I couldn't find an item matching '{itemRef}'.");

            var latestId = await _db.MakeBuyDecisions
                .Where(d => d.ItemId == itemId && _tenant.VisibleCompanyIds.Contains(d.CompanyId))
                .OrderByDescending(d => d.DecidedAtUtc).ThenByDescending(d => d.Id)
                .Select(d => (int?)d.Id)
                .FirstOrDefaultAsync(ct);

            if (latestId == null)
                return Result.Failure<MakeBuyVoiceExplanation>(
                    $"No make-or-buy decision has been recorded for {partNumber} yet.");

            var explained = await ExplainAsync(latestId.Value, ct);
            if (explained.IsFailure)
                return Result.Failure<MakeBuyVoiceExplanation>(explained.Error!);
            var r = explained.Value!;

            string? supplierName = null;
            if (r.ChosenSupplierId != null)
                supplierName = await _db.Vendors
                    .Where(v => v.Id == r.ChosenSupplierId
                        && _tenant.VisibleCompanyIds.Contains(v.CompanyId ?? 0))
                    .Select(v => v.Name)
                    .FirstOrDefaultAsync(ct);

            return Result.Success(new MakeBuyVoiceExplanation(
                r, partNumber ?? $"Item #{itemId}", description, supplierName));
        }

        private sealed record PolicyCfg(decimal CapacityThresholdPct, decimal DrumOffloadCostTolerancePct,
            decimal BuyDecisionScoreThreshold, decimal WeightCapacity, decimal WeightCostDelta,
            decimal WeightBreakEven, decimal WeightLeadTime, decimal WeightQualityRisk, MakeBuyTieBreak FinalTieBreak);
        private sealed record RoutingOpInfo(int WorkCenterId, decimal SetupMins, decimal RunPerUnitMins,
            decimal QueueMins, decimal MoveMins, decimal WaitMins);
        private sealed record QuoteInfo(int QuoteId, int VendorId, decimal UnitPrice, int LeadTimeDays, decimal? OnTimeDeliveryPct);
    }
}
