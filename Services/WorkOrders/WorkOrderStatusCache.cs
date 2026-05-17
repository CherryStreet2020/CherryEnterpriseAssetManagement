using System.Collections.Concurrent;
using System.Collections.Generic;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.WorkOrders;

namespace Abs.FixedAssets.Services.WorkOrders
{
    // ADR-012 v0.2 / PR #119.3 — Process-wide cache for resolved status
    // profiles / labels / transitions.
    //
    // Same Singleton-cache + Scoped-service split as
    // WorkOrderFieldVisibilityCache. Lets the engine share resolved
    // graphs across requests without DI lifetime conflict on AppDbContext.
    public class WorkOrderStatusCache
    {
        // (Classification → list of labels for that classification,
        //  ordered by DisplayOrder).
        public ConcurrentDictionary<WorkOrderClassification, IReadOnlyList<WorkOrderStatusLabel>> LabelsByClassification { get; }
            = new();

        // (Classification, FromStatusCode) → list of transitions out
        // of that status, ordered by DisplayOrder.
        public ConcurrentDictionary<(WorkOrderClassification, short), IReadOnlyList<WorkOrderStatusTransition>> TransitionsByFromStatus { get; }
            = new();

        // Classification → profile.
        public ConcurrentDictionary<WorkOrderClassification, WorkOrderStatusProfile> ProfileByClassification { get; }
            = new();
    }
}
