using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class GlAccount
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        [Display(Name = "Account Number")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required, StringLength(100)]
        [Display(Name = "Account Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public GlAccountType AccountType { get; set; }
        public int? AccountTypeLookupValueId { get; set; }

        public GlAccountCategory Category { get; set; }

        public GlAccountSubCategory SubCategory { get; set; }

        [Display(Name = "Normal Balance")]
        public NormalBalance NormalBalance { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsSystemAccount { get; set; } = false;

        public bool AllowManualEntry { get; set; } = true;

        [Display(Name = "Requires Cost Center")]
        public bool RequiresCostCenter { get; set; } = false;

        [Display(Name = "Requires Department")]
        public bool RequiresDepartment { get; set; } = false;

        [Display(Name = "Requires Asset Category")]
        public bool RequiresAssetCategory { get; set; } = false;

        public int? ParentAccountId { get; set; }
        public GlAccount? ParentAccount { get; set; }

        public int SortOrder { get; set; } = 0;

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<GlAccount> ChildAccounts { get; set; } = new List<GlAccount>();
    }

    public enum GlAccountType
    {
        Asset = 1,
        Liability = 2,
        Equity = 3,
        Revenue = 4,
        Expense = 5,
        ContraAsset = 6,
        ContraRevenue = 7,
        ContraExpense = 8
    }

    public enum GlAccountCategory
    {
        CashAndReceivables = 100,
        MroInventory = 110,
        WorkInProgress = 120,
        PrepaidAndDeposits = 130,
        FixedAssetsLandBuildings = 140,
        FixedAssetsMachinery = 150,
        FixedAssetsVehicles = 160,
        FixedAssetsTechnology = 170,
        FixedAssetsTooling = 180,
        AccumulatedDepreciation = 190,
        IntercompanyReceivables = 195,
        CurrentLiabilities = 200,
        IntercompanyPayables = 205,
        LongTermLiabilities = 210,
        InvestmentInSubsidiaries = 215,
        Equity = 300,
        IntercompanyEliminations = 310,
        CurrencyTranslation = 320,
        RevenueAndGains = 400,
        CostOfSales = 500,
        DepreciationExpense = 600,
        MaintenanceLabor = 610,
        RepairParts = 620,
        CalibrationCertification = 630,
        PreventiveMaintenance = 640,
        EquipmentLeaseRental = 650,
        UtilitiesInfrastructure = 660,
        SafetyEnvironmental = 670,
        AssetLosses = 680,
        OperatingExpenses = 690
    }

    public enum GlAccountSubCategory
    {
        None = 0,
        Buildings = 1,
        LandImprovements = 2,
        MachineryProduction = 3,
        MachineryCnc = 4,
        MachineryCranes = 5,
        MachineryWelding = 6,
        VehiclesForklifts = 7,
        VehiclesFleet = 8,
        ComputersServers = 9,
        IoTSensors = 10,
        ToolingDies = 11,
        ToolingFixtures = 12,
        SpareParts = 13,
        Consumables = 14,
        InternalLabor = 15,
        ContractLabor = 16,
        CalibrationServices = 17,
        CertificationFees = 18,
        ScheduledPm = 19,
        ConditionBased = 20,
        EmergencyRepairs = 21,
        CorrectiveMaintenance = 22
    }

    public enum NormalBalance
    {
        Debit = 1,
        Credit = 2
    }

    public class CostCenter
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(50)]
        public string? StateProvince { get; set; }

        [StringLength(20)]
        public string? PostalCode { get; set; }

        [StringLength(50)]
        public string? Country { get; set; }

        public CostCenterType Type { get; set; } = CostCenterType.Plant;
        public int? TypeLookupValueId { get; set; }

        public bool IsActive { get; set; } = true;

        public int? ParentCostCenterId { get; set; }
        public CostCenter? ParentCostCenter { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<CostCenter> ChildCostCenters { get; set; } = new List<CostCenter>();
        public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }

    public enum CostCenterType
    {
        Corporate = 0,
        Region = 1,
        Plant = 2,
        Building = 3,
        ProductionLine = 4,
        WorkCell = 5
    }

    public class Department
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        public DepartmentType Type { get; set; } = DepartmentType.Operations;
        public int? TypeLookupValueId { get; set; }

        public bool IsActive { get; set; } = true;

        public int? ManagerId { get; set; }

        public int? CostCenterId { get; set; }
        public CostCenter? CostCenter { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }

    public enum DepartmentType
    {
        Executive = 0,
        Finance = 1,
        Operations = 2,
        Production = 3,
        Maintenance = 4,
        Quality = 5,
        Engineering = 6,
        Facilities = 7,
        IT = 8,
        HR = 9,
        Safety = 10,
        Warehouse = 11,
        Shipping = 12,
        Purchasing = 13
    }

    public class Location
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        public LocationType Type { get; set; } = LocationType.Building;
        public int? TypeLookupValueId { get; set; }

        public int? SiteId { get; set; }
        [Display(Name = "Site")]
        public Site? Site { get; set; }

        [StringLength(100)]
        public string? Building { get; set; }

        [StringLength(50)]
        public string? Floor { get; set; }

        [StringLength(50)]
        public string? Bay { get; set; }

        [StringLength(50)]
        public string? Station { get; set; }

        [StringLength(50)]
        public string? Aisle { get; set; }

        [StringLength(50)]
        public string? Rack { get; set; }

        [StringLength(50)]
        public string? Shelf { get; set; }

        [StringLength(50)]
        public string? Bin { get; set; }

        public bool IsActive { get; set; } = true;

        public int? ParentLocationId { get; set; }
        public Location? ParentLocation { get; set; }

        [Display(Name = "Hierarchy Path")]
        [StringLength(500)]
        public string? HierarchyPath { get; set; }

        [Display(Name = "Hierarchy Level")]
        public int HierarchyLevel { get; set; } = 0;

        public int? CostCenterId { get; set; }
        public CostCenter? CostCenter { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public int SortOrder { get; set; } = 0;

        public LocationCriticality Criticality { get; set; } = LocationCriticality.Low;

        public SafetyZone SafetyZone { get; set; } = SafetyZone.None;

        [StringLength(100)]
        [Display(Name = "Safety Requirements")]
        public string? SafetyRequirements { get; set; }

        [Display(Name = "Max Asset Capacity")]
        public int? MaxAssetCapacity { get; set; }

        [Display(Name = "Current Asset Count")]
        public int CurrentAssetCount { get; set; } = 0;

        [Display(Name = "Square Footage")]
        public int? SquareFootage { get; set; }

        [Display(Name = "Height (ft)")]
        public decimal? HeightFeet { get; set; }

        [Display(Name = "GPS Latitude")]
        public decimal? Latitude { get; set; }

        [Display(Name = "GPS Longitude")]
        public decimal? Longitude { get; set; }

        [Display(Name = "Is Operational")]
        public bool IsOperational { get; set; } = true;

        [Display(Name = "Allows Asset Installation")]
        public bool AllowsAssetInstallation { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string? ModifiedBy { get; set; }

        public ICollection<Location> ChildLocations { get; set; } = new List<Location>();
        public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }

    public enum LocationType
    {
        Building = 0,
        Floor = 1,
        Area = 2,
        Room = 3,
        Bay = 4,
        Cell = 5,
        Station = 6,
        Dock = 7,
        Yard = 8,
        Storage = 9
    }

    public enum LocationCriticality
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public enum SafetyZone
    {
        None = 0,
        General = 1,
        Caution = 2,
        Warning = 3,
        Danger = 4,
        Confined = 5,
        Hazmat = 6,
        Radiation = 7
    }

    public class AssetCategory
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        public MacrsPropertyClass DefaultMacrsClass { get; set; } = MacrsPropertyClass.SevenYear;

        public int? DefaultCcaClassId { get; set; }

        public int DefaultUsefulLifeMonths { get; set; } = 84;

        [Column(TypeName = "decimal(5,2)")]
        public decimal DefaultSalvagePercent { get; set; } = 0;

        public int? AssetGlAccountId { get; set; }
        public GlAccount? AssetGlAccount { get; set; }

        public int? AccumDepGlAccountId { get; set; }
        public GlAccount? AccumDepGlAccount { get; set; }

        public int? DepExpGlAccountId { get; set; }
        public GlAccount? DepExpGlAccount { get; set; }

        public bool IsActive { get; set; } = true;

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }
}
