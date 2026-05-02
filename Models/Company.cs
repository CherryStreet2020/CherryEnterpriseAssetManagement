using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public enum CompanyType
    {
        Holding = 0,
        Operating = 1,
        Division = 2
    }

    public enum CompanyStructure
    {
        Single = 0,
        MultiCompany = 1
    }

    public enum FinancialMode
    {
        Standalone = 0,
        ERPIntegration = 1
    }

    public enum IntegrationType
    {
        None = 0,
        QuickBooks = 1,
        SAP = 2,
        Oracle = 3,
        NetSuite = 4,
        CustomAPI = 5
    }

    public class Company
    {
        public int Id { get; set; }

        [Display(Name = "Tenant")]
        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? LegalName { get; set; }

        [StringLength(20)]
        public string? CompanyCode { get; set; }

        public CompanyType CompanyType { get; set; } = CompanyType.Operating;

        public CompanyStructure CompanyStructure { get; set; } = CompanyStructure.Single;

        public int? ParentCompanyId { get; set; }
        public Company? ParentCompany { get; set; }
        public ICollection<Company> ChildCompanies { get; set; } = new List<Company>();

        [NotMapped]
        public int HierarchyLevel { get; set; }

        [Required, StringLength(3)]
        public string Currency { get; set; } = "USD";

        [StringLength(50)]
        public string? TaxId { get; set; }

        public AccountingPeriodType PeriodType { get; set; } = AccountingPeriodType.Standard12Month;

        public int FiscalYearStartMonth { get; set; } = 1;

        public int FiscalYearStartDay { get; set; } = 1;

        public bool IsShortYear { get; set; } = false;

        [DataType(DataType.Date)]
        public DateTime? ShortYearStart { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ShortYearEnd { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(50)]
        public string? StateProvince { get; set; }

        [StringLength(20)]
        public string? PostalCode { get; set; }

        [StringLength(50)]
        public string? Country { get; set; } = "United States";

        [StringLength(100)]
        public string? ContactName { get; set; }

        [StringLength(100)]
        public string? ContactEmail { get; set; }

        [StringLength(20)]
        public string? ContactPhone { get; set; }

        public DepreciationMethod DefaultDepMethod { get; set; } = DepreciationMethod.StraightLine;
        public DepreciationConvention DefaultConvention { get; set; } = DepreciationConvention.MidMonth;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(255)]
        public string? LogoPath { get; set; }

        [StringLength(50)]
        public string? GstHstNumber { get; set; }

        [StringLength(50)]
        public string? PstNumber { get; set; }

        [StringLength(50)]
        public string? BusinessNumber { get; set; }

        [StringLength(5)]
        public string DefaultLanguage { get; set; } = "en";

        [StringLength(50)]
        public string TimeZone { get; set; } = "America/New_York";

        public decimal? ApprovalThreshold { get; set; }

        public bool RequireApprovalForDisposals { get; set; } = false;

        public bool RequireApprovalForTransfers { get; set; } = false;

        [Display(Name = "Financial Mode")]
        public FinancialMode FinancialMode { get; set; } = FinancialMode.Standalone;

        [Display(Name = "Integration Type")]
        public IntegrationType IntegrationType { get; set; } = IntegrationType.None;

        [Display(Name = "Enable Work Orders Module")]
        public bool EnableWorkOrders { get; set; } = true;

        [Display(Name = "Enable Purchasing Module")]
        public bool EnablePurchasing { get; set; } = true;

        [Display(Name = "Enable Inventory Module")]
        public bool EnableInventory { get; set; } = true;

        [Display(Name = "Enable Accounts Payable Module")]
        public bool EnableAccountsPayable { get; set; } = true;

        [Display(Name = "Enable Vendor Management")]
        public bool EnableVendors { get; set; } = true;

        [StringLength(500)]
        public string? ERPConnectionString { get; set; }

        [StringLength(100)]
        public string? ERPCompanyCode { get; set; }

        public ICollection<Asset> Assets { get; set; } = new List<Asset>();
        public ICollection<Book> Books { get; set; } = new List<Book>();
    }
}
