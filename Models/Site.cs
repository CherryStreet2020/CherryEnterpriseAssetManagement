using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models
{
    public enum SiteType
    {
        Manufacturing,
        Warehouse,
        Office,
        Distribution,
        ServiceCenter,
        DataCenter,
        RAndD,
        Mixed
    }

    public enum SiteStatus
    {
        Active,
        Inactive,
        UnderConstruction,
        Decommissioned
    }

    public class Site
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        [Display(Name = "Site Code")]
        public string SiteCode { get; set; } = string.Empty;

        [Required, StringLength(100)]
        [Display(Name = "Site Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public SiteType Type { get; set; } = SiteType.Manufacturing;
        public int? TypeLookupValueId { get; set; }
        public LookupValue? TypeLookupValue { get; set; }

        public SiteStatus Status { get; set; } = SiteStatus.Active;
        public int? StatusLookupValueId { get; set; }
        public LookupValue? StatusLookupValue { get; set; }

        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        [StringLength(200)]
        [Display(Name = "Address Line 1")]
        public string? Address1 { get; set; }

        [StringLength(200)]
        [Display(Name = "Address Line 2")]
        public string? Address2 { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        [Display(Name = "State/Province")]
        public string? StateProvince { get; set; }

        [StringLength(20)]
        [Display(Name = "Postal Code")]
        public string? PostalCode { get; set; }

        [StringLength(100)]
        public string? Country { get; set; } = "United States";

        [StringLength(50)]
        [Display(Name = "Time Zone")]
        public string? TimeZone { get; set; } = "America/New_York";

        [StringLength(100)]
        [Display(Name = "Site Manager")]
        public string? SiteManager { get; set; }

        [StringLength(50)]
        [Display(Name = "Manager Email")]
        [DataType(DataType.EmailAddress)]
        public string? ManagerEmail { get; set; }

        [StringLength(30)]
        [Display(Name = "Manager Phone")]
        [DataType(DataType.PhoneNumber)]
        public string? ManagerPhone { get; set; }

        [StringLength(50)]
        [Display(Name = "Main Phone")]
        [DataType(DataType.PhoneNumber)]
        public string? MainPhone { get; set; }

        [StringLength(50)]
        [DataType(DataType.PhoneNumber)]
        public string? Fax { get; set; }

        [Display(Name = "Square Footage")]
        public int? SquareFootage { get; set; }

        [Display(Name = "Number of Buildings")]
        public int? NumberOfBuildings { get; set; }

        [Display(Name = "Employee Count")]
        public int? EmployeeCount { get; set; }

        [Display(Name = "Is Primary Site")]
        public bool IsPrimarySite { get; set; } = false;

        [Display(Name = "GPS Latitude")]
        public decimal? Latitude { get; set; }

        [Display(Name = "GPS Longitude")]
        public decimal? Longitude { get; set; }

        [Display(Name = "Operating Hours")]
        [StringLength(100)]
        public string? OperatingHours { get; set; }

        [Display(Name = "Number of Shifts")]
        public int NumberOfShifts { get; set; } = 1;

        [StringLength(200)]
        [Display(Name = "Shift Pattern")]
        public string? ShiftPattern { get; set; }

        [Display(Name = "24/7 Operation")]
        public bool Is24x7 { get; set; } = false;

        [Display(Name = "Asset Capacity")]
        public int? AssetCapacity { get; set; }

        [Display(Name = "Current Asset Count")]
        public int CurrentAssetCount { get; set; } = 0;

        [Display(Name = "Production Capacity")]
        [StringLength(100)]
        public string? ProductionCapacity { get; set; }

        [Display(Name = "Loading Docks")]
        public int? LoadingDocks { get; set; }

        [Display(Name = "Parking Spaces")]
        public int? ParkingSpaces { get; set; }

        [StringLength(200)]
        [Display(Name = "Emergency Contact")]
        public string? EmergencyContact { get; set; }

        [StringLength(50)]
        [Display(Name = "Emergency Phone")]
        public string? EmergencyPhone { get; set; }

        [Display(Name = "Has Fire Suppression")]
        public bool HasFireSuppression { get; set; } = false;

        [Display(Name = "Has Security System")]
        public bool HasSecuritySystem { get; set; } = false;

        [Display(Name = "Has Climate Control")]
        public bool HasClimateControl { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string? ModifiedBy { get; set; }

        public ICollection<Location>? Locations { get; set; }
        public ICollection<Asset>? Assets { get; set; }
    }
}
