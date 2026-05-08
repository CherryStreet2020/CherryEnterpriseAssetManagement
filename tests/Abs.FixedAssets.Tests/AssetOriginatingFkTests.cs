using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Lock-in tests for S2-4: explicit FK linkage from Asset back to its
/// financial origin (OriginatingPurchaseOrderId, OriginatingVendorInvoiceId,
/// OriginatingCipProjectId).
///
/// This PR only adds the schema. The FKs get populated by future Sprint 0.5
/// work — S1-1 (Receiving accrual) sets OriginatingPurchaseOrderId, S1-4
/// (CIP capitalization) sets OriginatingCipProjectId, S1-5 (AP posting)
/// sets OriginatingVendorInvoiceId. Tests prove the model accepts the
/// writes today and can read them back, so the dependent PRs can land
/// without schema migration churn.
/// </summary>
public class AssetOriginatingFkTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"asset-origin-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    [Fact]
    public async Task Asset_PersistedWithAllThreeOriginatingFks_ReadsBackUnchanged()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 100, CompanyCode = "C-100", Name = "Co", IsActive = true });
        db.Vendors.Add(new Vendor { Id = 1, Code = "V-1", Name = "V", CompanyId = 100, IsActive = true });
        await db.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            PONumber = "PO-1", VendorId = 1, CompanyId = 100,
            Status = POStatus.Approved, OrderDate = DateTime.UtcNow, Currency = "USD"
        };
        db.PurchaseOrders.Add(po);
        var invoice = new VendorInvoice
        {
            CompanyId = 100, VendorId = 1, InvoiceNumber = "INV-1",
            InvoiceDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(30),
            Currency = "USD", Status = InvoiceStatus.Draft, CreatedAt = DateTime.UtcNow
        };
        db.VendorInvoices.Add(invoice);
        var project = new CipProject
        {
            ProjectNumber = "CIP-1", Name = "Test", CompanyId = 100,
            Status = CipProjectStatus.Active, CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);
        await db.SaveChangesAsync();

        var asset = new Asset
        {
            AssetNumber = "A-ORIGIN", Description = "Asset with FKs",
            CompanyId = 100, AcquisitionCost = 1000m, UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow,
            OriginatingPurchaseOrderId = po.Id,
            OriginatingVendorInvoiceId = invoice.Id,
            OriginatingCipProjectId = project.Id
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var fromDb = await db.Assets.AsNoTracking().FirstAsync(a => a.Id == asset.Id);
        Assert.Equal(po.Id, fromDb.OriginatingPurchaseOrderId);
        Assert.Equal(invoice.Id, fromDb.OriginatingVendorInvoiceId);
        Assert.Equal(project.Id, fromDb.OriginatingCipProjectId);
    }

    [Fact]
    public async Task Asset_WithoutOriginatingFks_PersistsAsNull()
    {
        // Existing assets and manually-created assets have no origin
        // linkage — all three FKs default to null.
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 100, CompanyCode = "C-100", Name = "Co", IsActive = true });
        await db.SaveChangesAsync();

        var asset = new Asset
        {
            AssetNumber = "A-MANUAL", Description = "No origin",
            CompanyId = 100, AcquisitionCost = 500m, UsefulLifeMonths = 36,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var fromDb = await db.Assets.AsNoTracking().FirstAsync(a => a.Id == asset.Id);
        Assert.Null(fromDb.OriginatingPurchaseOrderId);
        Assert.Null(fromDb.OriginatingVendorInvoiceId);
        Assert.Null(fromDb.OriginatingCipProjectId);
    }
}
