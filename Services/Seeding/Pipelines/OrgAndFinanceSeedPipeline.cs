using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Seeding.Pipelines
{
    public class OrgAndFinanceSeedPipeline : ISeedPipeline
    {
        public string Name => "OrgAndFinanceSeed";
        public string Version => "1.0.0";
        public string Description => "Organizational hierarchy and financial masters: GL accounts, sites, departments, cost centers, asset categories";
        public bool IsDevOnly => false;

        private readonly List<ISeedStep> _steps;
        public IReadOnlyList<ISeedStep> Steps => _steps;

        public OrgAndFinanceSeedPipeline(AppDbContext context, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<OrgAndFinanceSeedPipeline>();
            _steps = new List<ISeedStep>
            {
                new GlAccountsSeedStep(context, logger),
                new SitesSeedStep(context, logger),
                new DepartmentsSeedStep(context, logger),
                new CostCentersSeedStep(context, logger),
                new AssetCategoriesSeedStep(context, logger),
                new ApprovalWorkflowsSeedStep(context, logger)
            };
        }
    }

    #region GlAccounts
    public class GlAccountsSeedStep : BaseSeedStep<GlAccount>
    {
        public override string StepName => "GlAccounts";
        public override string DomainName => "GlAccounts";
        public override string NaturalKeyDescription => "AccountNumber";

        public GlAccountsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<GlAccount> GetSeedData() => new[]
        {
            new GlAccount { AccountNumber = "1000", Name = "Cash - Operating", AccountType = GlAccountType.Asset, Category = GlAccountCategory.CashAndReceivables, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 1 },
            new GlAccount { AccountNumber = "1100", Name = "Accounts Receivable", AccountType = GlAccountType.Asset, Category = GlAccountCategory.CashAndReceivables, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 2 },
            new GlAccount { AccountNumber = "1300", Name = "MRO Inventory", AccountType = GlAccountType.Asset, Category = GlAccountCategory.MroInventory, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 3 },
            new GlAccount { AccountNumber = "1350", Name = "Spare Parts Inventory", AccountType = GlAccountType.Asset, Category = GlAccountCategory.MroInventory, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 4 },
            new GlAccount { AccountNumber = "1400", Name = "Work in Progress", AccountType = GlAccountType.Asset, Category = GlAccountCategory.WorkInProgress, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 5 },
            new GlAccount { AccountNumber = "1500", Name = "Buildings", AccountType = GlAccountType.Asset, Category = GlAccountCategory.FixedAssetsLandBuildings, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 10 },
            new GlAccount { AccountNumber = "1510", Name = "Accum Depr - Buildings", AccountType = GlAccountType.ContraAsset, Category = GlAccountCategory.AccumulatedDepreciation, NormalBalance = NormalBalance.Credit, IsActive = true, SortOrder = 11 },
            new GlAccount { AccountNumber = "1600", Name = "Machinery & Equipment", AccountType = GlAccountType.Asset, Category = GlAccountCategory.FixedAssetsMachinery, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 20 },
            new GlAccount { AccountNumber = "1610", Name = "Accum Depr - Machinery", AccountType = GlAccountType.ContraAsset, Category = GlAccountCategory.AccumulatedDepreciation, NormalBalance = NormalBalance.Credit, IsActive = true, SortOrder = 21 },
            new GlAccount { AccountNumber = "1700", Name = "Vehicles & Mobile Equip", AccountType = GlAccountType.Asset, Category = GlAccountCategory.FixedAssetsVehicles, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 30 },
            new GlAccount { AccountNumber = "1710", Name = "Accum Depr - Vehicles", AccountType = GlAccountType.ContraAsset, Category = GlAccountCategory.AccumulatedDepreciation, NormalBalance = NormalBalance.Credit, IsActive = true, SortOrder = 31 },
            new GlAccount { AccountNumber = "1800", Name = "Computer Equipment", AccountType = GlAccountType.Asset, Category = GlAccountCategory.FixedAssetsTechnology, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 40 },
            new GlAccount { AccountNumber = "1810", Name = "Accum Depr - Computers", AccountType = GlAccountType.ContraAsset, Category = GlAccountCategory.AccumulatedDepreciation, NormalBalance = NormalBalance.Credit, IsActive = true, SortOrder = 41 },
            new GlAccount { AccountNumber = "1900", Name = "Tooling & Dies", AccountType = GlAccountType.Asset, Category = GlAccountCategory.FixedAssetsTooling, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 50 },
            new GlAccount { AccountNumber = "1910", Name = "Accum Depr - Tooling", AccountType = GlAccountType.ContraAsset, Category = GlAccountCategory.AccumulatedDepreciation, NormalBalance = NormalBalance.Credit, IsActive = true, SortOrder = 51 },
            new GlAccount { AccountNumber = "2000", Name = "Accounts Payable", AccountType = GlAccountType.Liability, Category = GlAccountCategory.CurrentLiabilities, NormalBalance = NormalBalance.Credit, IsActive = true, SortOrder = 100 },
            new GlAccount { AccountNumber = "2100", Name = "Accrued Liabilities", AccountType = GlAccountType.Liability, Category = GlAccountCategory.CurrentLiabilities, NormalBalance = NormalBalance.Credit, IsActive = true, SortOrder = 101 },
            new GlAccount { AccountNumber = "3000", Name = "Retained Earnings", AccountType = GlAccountType.Equity, Category = GlAccountCategory.Equity, NormalBalance = NormalBalance.Credit, IsActive = true, SortOrder = 200 },
            new GlAccount { AccountNumber = "4000", Name = "Sales Revenue", AccountType = GlAccountType.Revenue, Category = GlAccountCategory.RevenueAndGains, NormalBalance = NormalBalance.Credit, IsActive = true, SortOrder = 300 },
            new GlAccount { AccountNumber = "4500", Name = "Gain on Asset Disposal", AccountType = GlAccountType.Revenue, Category = GlAccountCategory.RevenueAndGains, NormalBalance = NormalBalance.Credit, IsActive = true, SortOrder = 310 },
            new GlAccount { AccountNumber = "5000", Name = "Cost of Goods Sold", AccountType = GlAccountType.Expense, Category = GlAccountCategory.CostOfSales, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 400 },
            new GlAccount { AccountNumber = "6000", Name = "Depreciation Expense", AccountType = GlAccountType.Expense, Category = GlAccountCategory.DepreciationExpense, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 500 },
            new GlAccount { AccountNumber = "6100", Name = "Maintenance Labor", AccountType = GlAccountType.Expense, Category = GlAccountCategory.MaintenanceLabor, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 510 },
            new GlAccount { AccountNumber = "6200", Name = "Repair Parts & Materials", AccountType = GlAccountType.Expense, Category = GlAccountCategory.RepairParts, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 520 },
            new GlAccount { AccountNumber = "6300", Name = "Contract Maintenance", AccountType = GlAccountType.Expense, Category = GlAccountCategory.MaintenanceLabor, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 530 },
            new GlAccount { AccountNumber = "6400", Name = "Preventive Maintenance", AccountType = GlAccountType.Expense, Category = GlAccountCategory.PreventiveMaintenance, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 540 },
            new GlAccount { AccountNumber = "6500", Name = "Equipment Lease/Rental", AccountType = GlAccountType.Expense, Category = GlAccountCategory.EquipmentLeaseRental, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 550 },
            new GlAccount { AccountNumber = "7000", Name = "Utilities", AccountType = GlAccountType.Expense, Category = GlAccountCategory.UtilitiesInfrastructure, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 600 },
            new GlAccount { AccountNumber = "7500", Name = "Insurance", AccountType = GlAccountType.Expense, Category = GlAccountCategory.OperatingExpenses, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 610 },
            new GlAccount { AccountNumber = "8000", Name = "Loss on Asset Disposal", AccountType = GlAccountType.Expense, Category = GlAccountCategory.AssetLosses, NormalBalance = NormalBalance.Debit, IsActive = true, SortOrder = 700 }
        };

        protected override async Task<GlAccount?> FindByNaturalKeyAsync(GlAccount item, CancellationToken ct)
            => await Context.GlAccounts.FirstOrDefaultAsync(x => x.AccountNumber == item.AccountNumber, ct);
        protected override string GetNaturalKeyValue(GlAccount item) => item.AccountNumber;
        protected override bool ShouldUpdate(GlAccount existing, GlAccount incoming)
            => !StringEquals(existing.Name, incoming.Name) || existing.AccountType != incoming.AccountType
               || existing.Category != incoming.Category || existing.NormalBalance != incoming.NormalBalance
               || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(GlAccount existing, GlAccount incoming)
        {
            existing.Name = incoming.Name;
            existing.AccountType = incoming.AccountType;
            existing.Category = incoming.Category;
            existing.NormalBalance = incoming.NormalBalance;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region Sites
    public class SitesSeedStep : BaseSeedStep<Site>
    {
        private int _defaultCompanyId = 1;
        public override string StepName => "Sites";
        public override string DomainName => "Sites";
        public override string NaturalKeyDescription => "SiteCode";

        public SitesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override async Task OnBeforeExecuteAsync(CancellationToken cancellationToken)
        {
            var company = await Context.Companies.FirstOrDefaultAsync(cancellationToken);
            _defaultCompanyId = company?.Id ?? 1;
        }

        protected override IEnumerable<Site> GetSeedData() => new[]
        {
            new Site { SiteCode = "HQ", Name = "Corporate Headquarters", Description = "Main corporate office", Address1 = "100 Corporate Drive", City = "Chicago", Country = "USA", Status = SiteStatus.Active, CompanyId = _defaultCompanyId },
            new Site { SiteCode = "MFG1", Name = "Manufacturing Plant 1", Description = "Main manufacturing facility", Address1 = "200 Industrial Blvd", City = "Detroit", Country = "USA", Status = SiteStatus.Active, CompanyId = _defaultCompanyId },
            new Site { SiteCode = "MFG2", Name = "Manufacturing Plant 2", Description = "Secondary manufacturing", Address1 = "300 Factory Lane", City = "Cleveland", Country = "USA", Status = SiteStatus.Active, CompanyId = _defaultCompanyId },
            new Site { SiteCode = "DIST", Name = "Distribution Center", Description = "Main distribution hub", Address1 = "400 Logistics Way", City = "Indianapolis", Country = "USA", Status = SiteStatus.Active, CompanyId = _defaultCompanyId },
            new Site { SiteCode = "SERV", Name = "Service Center", Description = "Field service operations", Address1 = "500 Service Road", City = "Columbus", Country = "USA", Status = SiteStatus.Active, CompanyId = _defaultCompanyId }
        };

        protected override async Task<Site?> FindByNaturalKeyAsync(Site item, CancellationToken ct)
            => await Context.Sites.FirstOrDefaultAsync(x => x.SiteCode == item.SiteCode, ct);
        protected override string GetNaturalKeyValue(Site item) => item.SiteCode;
        protected override bool ShouldUpdate(Site existing, Site incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || !StringEquals(existing.Address1, incoming.Address1) || !StringEquals(existing.City, incoming.City);
        protected override void UpdateEntity(Site existing, Site incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.Address1 = incoming.Address1;
            existing.City = incoming.City;
        }
    }
    #endregion

    #region Departments
    public class DepartmentsSeedStep : BaseSeedStep<Department>
    {
        public override string StepName => "Departments";
        public override string DomainName => "Departments";
        public override string NaturalKeyDescription => "Code";

        public DepartmentsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Department> GetSeedData() => new[]
        {
            new Department { Code = "MAINT", Name = "Maintenance", Description = "Maintenance department", IsActive = true, SortOrder = 1 },
            new Department { Code = "PROD", Name = "Production", Description = "Production operations", IsActive = true, SortOrder = 2 },
            new Department { Code = "QA", Name = "Quality Assurance", Description = "Quality control", IsActive = true, SortOrder = 3 },
            new Department { Code = "ENGR", Name = "Engineering", Description = "Engineering department", IsActive = true, SortOrder = 4 },
            new Department { Code = "WHSE", Name = "Warehouse", Description = "Warehouse operations", IsActive = true, SortOrder = 5 },
            new Department { Code = "FAC", Name = "Facilities", Description = "Facilities management", IsActive = true, SortOrder = 6 },
            new Department { Code = "ADMIN", Name = "Administration", Description = "Administrative", IsActive = true, SortOrder = 7 },
            new Department { Code = "FIN", Name = "Finance", Description = "Finance and accounting", IsActive = true, SortOrder = 8 }
        };

        protected override async Task<Department?> FindByNaturalKeyAsync(Department item, CancellationToken ct)
            => await Context.Departments.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(Department item) => item.Code;
        protected override bool ShouldUpdate(Department existing, Department incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(Department existing, Department incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region CostCenters
    public class CostCentersSeedStep : BaseSeedStep<CostCenter>
    {
        public override string StepName => "CostCenters";
        public override string DomainName => "CostCenters";
        public override string NaturalKeyDescription => "Code";

        public CostCentersSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<CostCenter> GetSeedData() => new[]
        {
            new CostCenter { Code = "CC100", Name = "Maintenance Operations", Description = "Maintenance cost center", IsActive = true, SortOrder = 1 },
            new CostCenter { Code = "CC200", Name = "Production Line 1", Description = "Production line 1", IsActive = true, SortOrder = 2 },
            new CostCenter { Code = "CC210", Name = "Production Line 2", Description = "Production line 2", IsActive = true, SortOrder = 3 },
            new CostCenter { Code = "CC300", Name = "Quality Control", Description = "QA/QC operations", IsActive = true, SortOrder = 4 },
            new CostCenter { Code = "CC400", Name = "Warehouse Operations", Description = "Warehouse cost center", IsActive = true, SortOrder = 5 },
            new CostCenter { Code = "CC500", Name = "Facilities & Utilities", Description = "Facilities cost center", IsActive = true, SortOrder = 6 },
            new CostCenter { Code = "CC600", Name = "General & Admin", Description = "G&A cost center", IsActive = true, SortOrder = 7 }
        };

        protected override async Task<CostCenter?> FindByNaturalKeyAsync(CostCenter item, CancellationToken ct)
            => await Context.CostCenters.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(CostCenter item) => item.Code;
        protected override bool ShouldUpdate(CostCenter existing, CostCenter incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(CostCenter existing, CostCenter incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region AssetCategories
    public class AssetCategoriesSeedStep : BaseSeedStep<AssetCategory>
    {
        public override string StepName => "AssetCategories";
        public override string DomainName => "AssetCategories";
        public override string NaturalKeyDescription => "Code";

        public AssetCategoriesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<AssetCategory> GetSeedData() => new[]
        {
            new AssetCategory { Code = "BLDG", Name = "Buildings", Description = "Buildings and structures", DefaultUsefulLifeMonths = 480 },
            new AssetCategory { Code = "MACH", Name = "Machinery", Description = "Production machinery", DefaultUsefulLifeMonths = 120 },
            new AssetCategory { Code = "EQUIP", Name = "Equipment", Description = "General equipment", DefaultUsefulLifeMonths = 84 },
            new AssetCategory { Code = "VEHI", Name = "Vehicles", Description = "Vehicles and mobile equipment", DefaultUsefulLifeMonths = 60 },
            new AssetCategory { Code = "COMP", Name = "Computers", Description = "IT and computer equipment", DefaultUsefulLifeMonths = 36 },
            new AssetCategory { Code = "FURN", Name = "Furniture", Description = "Office furniture", DefaultUsefulLifeMonths = 84 },
            new AssetCategory { Code = "TOOL", Name = "Tooling", Description = "Tooling and dies", DefaultUsefulLifeMonths = 48 },
            new AssetCategory { Code = "HVAC", Name = "HVAC Systems", Description = "Heating, ventilation, AC", DefaultUsefulLifeMonths = 180 }
        };

        protected override async Task<AssetCategory?> FindByNaturalKeyAsync(AssetCategory item, CancellationToken ct)
            => await Context.AssetCategories.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(AssetCategory item) => item.Code;
        protected override bool ShouldUpdate(AssetCategory existing, AssetCategory incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.DefaultUsefulLifeMonths != incoming.DefaultUsefulLifeMonths;
        protected override void UpdateEntity(AssetCategory existing, AssetCategory incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.DefaultUsefulLifeMonths = incoming.DefaultUsefulLifeMonths;
        }
    }
    #endregion

    #region ApprovalWorkflows
    public class ApprovalWorkflowsSeedStep : BaseSeedStep<ApprovalWorkflow>
    {
        public override string StepName => "ApprovalWorkflows";
        public override string DomainName => "ApprovalWorkflows";
        public override string NaturalKeyDescription => "Code";

        public ApprovalWorkflowsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<ApprovalWorkflow> GetSeedData() => new[]
        {
            new ApprovalWorkflow { Code = "WO_STD", Name = "Standard Work Order", Description = "Standard work order approval", Type = WorkflowType.WorkOrder, ThresholdAmount = 5000m, RequiredApprovals = 1 },
            new ApprovalWorkflow { Code = "WO_HIGH", Name = "High Value Work Order", Description = "High value work order approval", Type = WorkflowType.WorkOrder, ThresholdAmount = 25000m, RequiredApprovals = 2 },
            new ApprovalWorkflow { Code = "PO_STD", Name = "Standard Purchase Order", Description = "Standard PO approval", Type = WorkflowType.PurchaseOrder, ThresholdAmount = 10000m, RequiredApprovals = 1 },
            new ApprovalWorkflow { Code = "PO_HIGH", Name = "High Value Purchase Order", Description = "High value PO approval", Type = WorkflowType.PurchaseOrder, ThresholdAmount = 50000m, RequiredApprovals = 2 },
            new ApprovalWorkflow { Code = "DISP", Name = "Asset Disposal", Description = "Asset disposal approval", Type = WorkflowType.AssetDisposal, ThresholdAmount = 1000m, RequiredApprovals = 2 },
            new ApprovalWorkflow { Code = "TRANS", Name = "Asset Transfer", Description = "Asset transfer approval", Type = WorkflowType.AssetTransfer, ThresholdAmount = 0m, RequiredApprovals = 1 }
        };

        protected override async Task<ApprovalWorkflow?> FindByNaturalKeyAsync(ApprovalWorkflow item, CancellationToken ct)
            => await Context.ApprovalWorkflows.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(ApprovalWorkflow item) => item.Code;
        protected override bool ShouldUpdate(ApprovalWorkflow existing, ApprovalWorkflow incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.ThresholdAmount != incoming.ThresholdAmount || existing.RequiredApprovals != incoming.RequiredApprovals;
        protected override void UpdateEntity(ApprovalWorkflow existing, ApprovalWorkflow incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.ThresholdAmount = incoming.ThresholdAmount;
            existing.RequiredApprovals = incoming.RequiredApprovals;
        }
    }
    #endregion
}
