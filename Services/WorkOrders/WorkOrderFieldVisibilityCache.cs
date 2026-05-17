using System.Collections.Concurrent;
using System.Collections.Generic;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.WorkOrders;

namespace Abs.FixedAssets.Services.WorkOrders
{
    // ADR-012 v0.2 / PR #119.2 — Process-wide cache for resolved
    // WorkOrderFieldVisibility layouts.
    //
    // Why a separate class? The service that resolves layouts depends on
    // AppDbContext (Scoped). If the service itself were Singleton, DI
    // would refuse to inject a Scoped DbContext into it. Splitting the
    // cache out into a thin Singleton lets the Scoped service share
    // cached layouts across requests without DI-scope-mismatch errors.
    //
    // Cache key is (Classification, TenantId-or--1). Null TenantId encodes
    // as -1 to keep "global defaults" and "tenant 0" disjoint.
    public class WorkOrderFieldVisibilityCache
    {
        public ConcurrentDictionary<(WorkOrderClassification, int), IReadOnlyList<WorkOrderFieldVisibility>> Layouts { get; }
            = new();
    }
}
