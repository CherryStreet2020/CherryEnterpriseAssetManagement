// ADR-025 D5 / Sprint 13.5 PR #3 — ProductionOrderService implementation.
//
// Five mutation methods. Greenfield service — no PageModels mutate
// ProductionOrders today, so PR #3 establishes the canonical write paths
// before PR #4 builds the /Production UI shell.
//
//   1. CreateAsync                — new order (+chain node, audit stamp)
//   2. UpdateHeaderAsync          — editable header fields
//   3. UpdateStatusAsync          — legal-transition map w/ ActualStart/End stamps
//   4. AssignToProjectAsync       — delegates to ICustomerProjectService.LinkProductionOrderAsync
//   5. UnassignFromProjectAsync   — nulls the project FK trio (no chain teardown)
//
// Chain emit on Create is failure-isolated (try/catch + LogWarning) matching
// CustomerProjectService.CreateAsync. Status transitions don't emit a new
// edge — the status is just metadata on the ProductionOrder node which
// downstream cytoscape rendering reads via the read-path EnsureNodeAsync.
//
// Tenant scoping: ProductionOrder has no direct CompanyId. We scope through
// Location.CompanyId first, fall back to Customer.CompanyId. An order with
// neither set is unscopable and rejected on mutating ops.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public sealed class ProductionOrderService : IProductionOrderService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICustomerProjectService _customerProjectService;
    private readonly Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService _chainOfCustody;
    private readonly ILogger<ProductionOrderService> _logger;

    public ProductionOrderService(
        AppDbContext db,
        ITenantContext tenantContext,
        ICustomerProjectService customerProjectService,
        Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService chainOfCustody,
        ILogger<ProductionOrderService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _customerProjectService = customerProjectService;
        _chainOfCustody = chainOfCustody;
        _logger = logger;
    }

    // ----------------------------------------------------------------
    // 1. CreateAsync — new ProductionOrder in Planned status.
    // ----------------------------------------------------------------
    public async Task<Result<ProductionOrder>> CreateAsync(
        CreateProductionOrderRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.OrderNumber))
            return Result.Failure<ProductionOrder>("OrderNumber is required.");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result.Failure<ProductionOrder>("Title is required.");
        if (request.QuantityOrdered < 0)
            return Result.Failure<ProductionOrder>("QuantityOrdered cannot be negative.");

        // Tenant scope must be resolvable from either Location or Customer
        // (matches the ICustomerProjectService.LinkProductionOrderAsync rule).
        int? scopedCompanyId = null;

        if (request.LocationId.HasValue)
        {
            var loc = await _db.Locations
                .Where(l => l.Id == request.LocationId.Value)
                .Select(l => new { l.Id, l.CompanyId })
                .FirstOrDefaultAsync(ct);
            if (loc == null)
                return Result.Failure<ProductionOrder>($"Location {request.LocationId} not found.");
            scopedCompanyId = loc.CompanyId;
        }

        if (scopedCompanyId == null && request.CustomerId.HasValue)
        {
            var cust = await _db.Customers
                .Where(c => c.Id == request.CustomerId.Value)
                .Select(c => new { c.Id, c.CompanyId })
                .FirstOrDefaultAsync(ct);
            if (cust == null)
                return Result.Failure<ProductionOrder>($"Customer {request.CustomerId} not found.");
            scopedCompanyId = cust.CompanyId;
        }

        if (scopedCompanyId == null)
            return Result.Failure<ProductionOrder>(
                "Provide either LocationId or CustomerId so the order can be tenant-scoped.");

        if (!_tenantContext.VisibleCompanyIds.Contains(scopedCompanyId.Value))
            return Result.Failure<ProductionOrder>(
                $"Resolved company {scopedCompanyId.Value} is not visible to the current tenant.");

        // Cross-FK sanity: if both Location and Customer are set, their
        // companies should match (an order can't physically run at company
        // A while being billed to a customer of company B).
        if (request.LocationId.HasValue && request.CustomerId.HasValue)
        {
            var custCo = await _db.Customers
                .Where(c => c.Id == request.CustomerId.Value)
                .Select(c => (int?)c.CompanyId)
                .FirstOrDefaultAsync(ct);
            if (custCo.HasValue && custCo.Value != scopedCompanyId.Value)
                return Result.Failure<ProductionOrder>(
                    "Location and Customer belong to different companies.");
        }

        // If an Item is specified, it must be visible to the same company.
        if (request.ItemId.HasValue)
        {
            var itemOk = await _db.Items
                .Where(i => i.Id == request.ItemId.Value
                         && i.CompanyId == scopedCompanyId.Value)
                .AnyAsync(ct);
            if (!itemOk)
                return Result.Failure<ProductionOrder>(
                    $"Item {request.ItemId} does not belong to company {scopedCompanyId.Value}.");
        }

        // PR #5c.2 — Soft-uniqueness pre-check is now tenant-scoped (OrderNumber
        // UNIQUE became composite (CompanyId, OrderNumber) per the migration).
        // Two tenants can each have their own "PRO-2026-00042" without colliding.
        var orderNumberTaken = await _db.ProductionOrders
            .Where(p => p.CompanyId == scopedCompanyId.Value
                     && p.OrderNumber == request.OrderNumber)
            .AnyAsync(ct);
        if (orderNumberTaken)
            return Result.Failure<ProductionOrder>(
                $"Production order number '{request.OrderNumber}' is already in use for this company.");

        var order = new ProductionOrder
        {
            // PR #5c.2 — Stamp CompanyId directly on the row. The migration
            // backfilled existing rows via Location/CustomerProject joins; new
            // rows stamp it explicitly here so the UNIQUE index works correctly
            // and tenant filters skip the Location JOIN.
            CompanyId               = scopedCompanyId.Value,
            OrderNumber             = request.OrderNumber,
            Type                    = request.Type,
            Status                  = ProductionOrderStatus.Planned,
            Title                   = request.Title,
            Description             = request.Description,
            ItemId                  = request.ItemId,
            LocationId              = request.LocationId,
            CustomerId              = request.CustomerId,
            QuantityOrdered         = request.QuantityOrdered,
            Uom                     = request.Uom,
            ScheduledStart          = request.ScheduledStart,
            ScheduledEnd            = request.ScheduledEnd,
            Priority                = request.Priority,
            MasterProductionOrderId = request.MasterProductionOrderId,
            MaterialStructureId     = request.MaterialStructureId,
            CreatedAt               = DateTime.UtcNow,
            CreatedBy               = request.CreatedBy
        };

        _db.ProductionOrders.Add(order);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _chainOfCustody.EnsureNodeAsync(
                new Abs.FixedAssets.Services.ChainOfCustody.EnsureNodeRequest(
                    NodeType: Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.ProductionOrder,
                    EntityId: order.Id,
                    Label:    order.OrderNumber),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Chain node emit failed for ProductionOrder {OrderId}. Backfill recovers.",
                order.Id);
        }

        return Result.Success(order);
    }

    // ----------------------------------------------------------------
    // 2. UpdateHeaderAsync — editable fields only. Project link goes
    //    through AssignToProjectAsync.
    // ----------------------------------------------------------------
    public async Task<Result<ProductionOrder>> UpdateHeaderAsync(
        UpdateProductionOrderHeaderRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result.Failure<ProductionOrder>("Title is required.");
        if (request.QuantityOrdered < 0)
            return Result.Failure<ProductionOrder>("QuantityOrdered cannot be negative.");

        var orderScope = await _db.ProductionOrders
            .Where(p => p.Id == request.ProductionOrderId)
            .Select(p => new
            {
                Order      = p,
                CompanyVia = (int?)(p.Location != null ? p.Location.CompanyId : null)
                             ?? (int?)(p.Customer != null ? p.Customer.CompanyId : null)
            })
            .FirstOrDefaultAsync(ct);
        if (orderScope == null)
            return Result.Failure<ProductionOrder>(
                $"Production order {request.ProductionOrderId} not found.");

        if (orderScope.CompanyVia == null
            || !_tenantContext.VisibleCompanyIds.Contains(orderScope.CompanyVia.Value))
            return Result.Failure<ProductionOrder>(
                $"Production order {request.ProductionOrderId} is not visible to the current tenant.");

        var order = orderScope.Order;

        if (order.Status == ProductionOrderStatus.Completed
            || order.Status == ProductionOrderStatus.Cancelled)
            return Result.Failure<ProductionOrder>(
                $"Production order is {order.Status} — header edits are not allowed on terminal-status orders.");

        order.Title                   = request.Title;
        order.Description             = request.Description;
        order.ItemId                  = request.ItemId;
        order.LocationId              = request.LocationId;
        order.CustomerId              = request.CustomerId;
        order.QuantityOrdered         = request.QuantityOrdered;
        order.Uom                     = request.Uom;
        order.ScheduledStart          = request.ScheduledStart;
        order.ScheduledEnd            = request.ScheduledEnd;
        order.Priority                = request.Priority;
        order.MasterProductionOrderId = request.MasterProductionOrderId;
        order.MaterialStructureId     = request.MaterialStructureId;
        order.ModifiedAt              = DateTime.UtcNow;
        order.ModifiedBy              = request.ModifiedBy;

        await _db.SaveChangesAsync(ct);
        return Result.Success(order);
    }

    // ----------------------------------------------------------------
    // 3. UpdateStatusAsync — legal-transition map + actual-date stamps.
    // ----------------------------------------------------------------
    public async Task<Result<ProductionOrder>> UpdateStatusAsync(
        UpdateProductionOrderStatusRequest request,
        CancellationToken ct)
    {
        var orderScope = await _db.ProductionOrders
            .Where(p => p.Id == request.ProductionOrderId)
            .Select(p => new
            {
                Order      = p,
                CompanyVia = (int?)(p.Location != null ? p.Location.CompanyId : null)
                             ?? (int?)(p.Customer != null ? p.Customer.CompanyId : null)
            })
            .FirstOrDefaultAsync(ct);
        if (orderScope == null)
            return Result.Failure<ProductionOrder>(
                $"Production order {request.ProductionOrderId} not found.");

        if (orderScope.CompanyVia == null
            || !_tenantContext.VisibleCompanyIds.Contains(orderScope.CompanyVia.Value))
            return Result.Failure<ProductionOrder>(
                $"Production order {request.ProductionOrderId} is not visible to the current tenant.");

        var order = orderScope.Order;

        if (!IsLegalProductionStatusTransition(order.Status, request.NewStatus))
            return Result.Failure<ProductionOrder>(
                $"Illegal status transition: {order.Status} → {request.NewStatus}.");

        order.Status     = request.NewStatus;
        order.ModifiedAt = DateTime.UtcNow;
        order.ModifiedBy = request.ModifiedBy;

        if (request.NewStatus == ProductionOrderStatus.InProgress && order.ActualStart == null)
            order.ActualStart = DateTime.UtcNow;

        if (request.NewStatus == ProductionOrderStatus.Completed)
            order.ActualEnd = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(order);
    }

    private static bool IsLegalProductionStatusTransition(
        ProductionOrderStatus from,
        ProductionOrderStatus to)
    {
        if (from == to) return true; // idempotent no-op
        return (from, to) switch
        {
            (ProductionOrderStatus.Planned,    ProductionOrderStatus.Released)   => true,
            (ProductionOrderStatus.Planned,    ProductionOrderStatus.Cancelled)  => true,
            (ProductionOrderStatus.Released,   ProductionOrderStatus.InProgress) => true,
            (ProductionOrderStatus.Released,   ProductionOrderStatus.OnHold)     => true,
            (ProductionOrderStatus.Released,   ProductionOrderStatus.Cancelled)  => true,
            (ProductionOrderStatus.InProgress, ProductionOrderStatus.OnHold)     => true,
            (ProductionOrderStatus.InProgress, ProductionOrderStatus.Completed)  => true,
            (ProductionOrderStatus.InProgress, ProductionOrderStatus.Cancelled)  => true,
            (ProductionOrderStatus.OnHold,     ProductionOrderStatus.Released)   => true,
            (ProductionOrderStatus.OnHold,     ProductionOrderStatus.InProgress) => true,
            (ProductionOrderStatus.OnHold,     ProductionOrderStatus.Cancelled)  => true,
            // Completed and Cancelled are terminal.
            _ => false
        };
    }

    // ----------------------------------------------------------------
    // 4. AssignToProjectAsync — delegates to ICustomerProjectService so
    //    the FK mutation + posting-mode validation + tenant-scope check
    //    + chain edge all live in one place.
    // ----------------------------------------------------------------
    public async Task<Result<ProductionOrder>> AssignToProjectAsync(
        AssignToProjectRequest request,
        CancellationToken ct)
    {
        var linkResult = await _customerProjectService.LinkProductionOrderAsync(
            new LinkProductionOrderRequest(
                ProductionOrderId:   request.ProductionOrderId,
                CustomerProjectId:   request.CustomerProjectId,
                ProjectPhaseId:      request.ProjectPhaseId,
                PostingMode:         request.PostingMode,
                ModifiedBy:          request.ModifiedBy),
            ct);

        if (linkResult.IsFailure)
            return Result.Failure<ProductionOrder>(linkResult.Error!);

        // Re-fetch so we return the canonical entity, not a projection.
        var order = await _db.ProductionOrders
            .FirstOrDefaultAsync(p => p.Id == request.ProductionOrderId, ct);
        if (order == null)
            return Result.Failure<ProductionOrder>(
                $"Production order {request.ProductionOrderId} disappeared after link.");

        return Result.Success(order);
    }

    // ----------------------------------------------------------------
    // 5. UnassignFromProjectAsync — null the project FK trio. No chain
    //    teardown by design (graph is append-only; history survives).
    // ----------------------------------------------------------------
    public async Task<Result<ProductionOrder>> UnassignFromProjectAsync(
        UnassignFromProjectRequest request,
        CancellationToken ct)
    {
        var orderScope = await _db.ProductionOrders
            .Include(p => p.CustomerProject)
            .Where(p => p.Id == request.ProductionOrderId)
            .Select(p => new
            {
                Order        = p,
                ProjectStatus = p.CustomerProject != null
                                ? (CustomerProjectStatus?)p.CustomerProject.Status
                                : null,
                CompanyVia   = (int?)(p.Location != null ? p.Location.CompanyId : null)
                               ?? (int?)(p.Customer != null ? p.Customer.CompanyId : null)
            })
            .FirstOrDefaultAsync(ct);
        if (orderScope == null)
            return Result.Failure<ProductionOrder>(
                $"Production order {request.ProductionOrderId} not found.");

        if (orderScope.CompanyVia == null
            || !_tenantContext.VisibleCompanyIds.Contains(orderScope.CompanyVia.Value))
            return Result.Failure<ProductionOrder>(
                $"Production order {request.ProductionOrderId} is not visible to the current tenant.");

        var order = orderScope.Order;

        if (order.CustomerProjectId == null)
            return Result.Failure<ProductionOrder>(
                "Production order is not linked to a project — nothing to unassign.");

        // Pre-Sprint 16 (admin-override): only allow unlink from active /
        // onhold / quote projects. Closed / Cancelled projects' linkages
        // are historical and shouldn't be touched without an admin.
        if (orderScope.ProjectStatus == CustomerProjectStatus.Closed
            || orderScope.ProjectStatus == CustomerProjectStatus.Cancelled)
            return Result.Failure<ProductionOrder>(
                $"Linked project is {orderScope.ProjectStatus} — unassign requires admin override (Sprint 16).");

        order.CustomerProjectId  = null;
        order.ProjectPhaseId     = null;
        order.ProjectPostingMode = null;
        order.ModifiedAt         = DateTime.UtcNow;
        order.ModifiedBy         = request.ModifiedBy;

        await _db.SaveChangesAsync(ct);
        return Result.Success(order);
    }
}
