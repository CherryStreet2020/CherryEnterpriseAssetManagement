// File: Models/Asset.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Models
{
    [Index(nameof(CompanyId), nameof(AssetNumber), IsUnique = true, Name = "IX_Assets_CompanyId_AssetNumber_Unique")]
    public class Asset
    {
        public int Id { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Asset Number is required.")]
        [StringLength(50, MinimumLength = 1)]
        [RegularExpression(@"^\S+(?:.*\S)?$", ErrorMessage = "Asset Number cannot be only whitespace.")]
        public string AssetNumber { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false, ErrorMessage = "Description is required.")]
        [StringLength(200, MinimumLength = 1)]
        [RegularExpression(@"^\S+(?:.*\S)?$", ErrorMessage = "Description cannot be only whitespace.")]
        public string Description { get; set; } = string.Empty;

        [StringLength(500)]
        public string? LongDescription { get; set; }

        [StringLength(200)]
        public string? Model { get; set; }

        [StringLength(100)]
        public string? SerialNumber { get; set; }

        [StringLength(50)]
        public string? TagNumber { get; set; }

        // Asset Image
        [StringLength(500)]
        public string? ImageUrl { get; set; }

        // Asset Hierarchy (Maximo-style parent/child)
        public int? ParentAssetId { get; set; }
        public Asset? ParentAsset { get; set; }
        public ICollection<Asset>? ChildAssets { get; set; }

        // Classification
        [StringLength(50)]
        public string? AssetType { get; set; }
        public int? AssetTypeLookupValueId { get; set; }
        public LookupValue? AssetTypeLookupValue { get; set; }

        [StringLength(50)]
        public string? ClassificationCode { get; set; }

        public int Priority { get; set; } = 3;
        public int? AssetPriorityLookupValueId { get; set; }
        public LookupValue? AssetPriorityLookupValue { get; set; }
        public AssetCondition Condition { get; set; } = AssetCondition.Good;
        public int? ConditionLookupValueId { get; set; }
        public LookupValue? ConditionLookupValue { get; set; }

        // Criticality
        public bool IsCritical { get; set; } = false;
        public bool IsRotating { get; set; } = false;
        public bool IsLinear { get; set; } = false;

        // Dates
        [DataType(DataType.Date)]
        public DateTime? PurchaseDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? InstallDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime InServiceDate { get; set; }

        public int? FiscalPurchaseYear { get; set; }

        // Warranty
        [DataType(DataType.Date)]
        public DateTime? WarrantyStartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? WarrantyEndDate { get; set; }

        public int? WarrantyVendorId { get; set; }

        [StringLength(100)]
        public string? WarrantyContractNumber { get; set; }

        // Purchase Info
        [StringLength(50)]
        public string? PurchaseOrderNumber { get; set; }

        [StringLength(50)]
        public string? InvoiceNumber { get; set; }

        // Financial
        [Column(TypeName = "decimal(18,2)")]
        public decimal AcquisitionCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ReplacementCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AccumulatedDepreciation { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SalvageValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? BookValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? FairMarketValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? InsuredValue { get; set; }

        // GL Accounts (for direct posting)
        [StringLength(20)]
        public string? GLAssetAccount { get; set; }

        [StringLength(20)]
        public string? GLAccumDepAccount { get; set; }

        [StringLength(20)]
        public string? GLDepExpenseAccount { get; set; }

        // Depreciation
        public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;
        public int? DepreciationMethodLookupValueId { get; set; }
        public LookupValue? DepreciationMethodLookupValue { get; set; }

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal? DepreciationRate { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "USD";

        [DataType(DataType.Date)]
        public DateTime? LastDepreciationDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? NextDepreciationDate { get; set; }

        public int UsefulLifeMonths { get; set; }

        // Technical Specifications
        [Column(TypeName = "decimal(10,2)")]
        public decimal? Horsepower { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? KilowattRating { get; set; }

        public int? Voltage { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? Amperage { get; set; }

        public int? RPM { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? Capacity { get; set; }

        [StringLength(20)]
        public string? CapacityUOM { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? Weight { get; set; }

        [StringLength(10)]
        public string? WeightUOM { get; set; }

        [StringLength(100)]
        public string? Dimensions { get; set; }

        // Meter Tracking
        public bool HasMeter { get; set; } = false;

        [StringLength(20)]
        public string? MeterType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CurrentMeterReading { get; set; }

        [DataType(DataType.Date)]
        public DateTime? LastMeterReadingDate { get; set; }

        // Location Fields
        public string? Bay { get; set; }

        [StringLength(50)]
        public string? Row { get; set; }

        [StringLength(50)]
        public string? Aisle { get; set; }

        [StringLength(50)]
        public string? Position { get; set; }

        // Legacy text fields (for migration compatibility)
        public string? Department { get; set; }

        // Foreign Keys
        public int? VendorId { get; set; }
        public Vendor? VendorRef { get; set; }

        public int? ManufacturerId { get; set; }
        public Manufacturer? Manufacturer { get; set; }

        public int? CostCenterId { get; set; }
        public CostCenter? CostCenterRef { get; set; }

        public int? DepartmentId { get; set; }
        public Department? DepartmentRef { get; set; }

        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        public int? LocationId { get; set; }
        public Location? LocationRef { get; set; }

        public int? AssetCategoryId { get; set; }
        public AssetCategory? AssetCategory { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        // Failure Class for CMMS
        public int? FailureClassId { get; set; }

        // Status
        public bool Active { get; set; } = true;
        public AssetStatus Status { get; set; } = AssetStatus.Active;
        public int? StatusLookupValueId { get; set; }
        public LookupValue? StatusLookupValue { get; set; }

        // Disposal
        [DataType(DataType.Date)]
        public DateTime? DisposalDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DisposalProceeds { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? GainLossOnDisposal { get; set; }

        [StringLength(50)]
        public string? DisposalMethod { get; set; }

        [StringLength(500)]
        public string? DisposalReason { get; set; }

        // Audit Fields
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        // Notes
        [StringLength(2000)]
        public string? Notes { get; set; }

        // ========== MES (Manufacturing Execution System) Integration ==========
        
        // Work Center / Production Line Assignment
        [StringLength(50)]
        public string? WorkCenterId { get; set; }
        
        [StringLength(100)]
        public string? WorkCenterName { get; set; }
        
        [StringLength(50)]
        public string? ProductionLineId { get; set; }
        
        [StringLength(100)]
        public string? ProductionLineName { get; set; }
        
        [StringLength(50)]
        public string? CellId { get; set; }
        
        // Process/Routing
        [StringLength(50)]
        public string? ProcessId { get; set; }
        
        [StringLength(50)]
        public string? OperationId { get; set; }
        
        public int? RoutingSequence { get; set; }
        
        // Shift Configuration
        [StringLength(50)]
        public string? ShiftCalendarId { get; set; }
        
        public int? PlannedProductionHoursPerDay { get; set; }

        // ========== IoT (Internet of Things) Integration ==========
        
        public bool IoTEnabled { get; set; } = false;
        
        [StringLength(100)]
        public string? IoTDeviceId { get; set; }
        
        [StringLength(100)]
        public string? IoTGatewayId { get; set; }
        
        // Protocol: OPC-UA, MQTT, Modbus, REST, PROFINET, EtherNet/IP
        [StringLength(30)]
        public string? IoTProtocol { get; set; }
        
        [StringLength(100)]
        public string? IoTEndpointUrl { get; set; }
        
        [StringLength(45)]
        public string? IPAddress { get; set; }
        
        [StringLength(17)]
        public string? MACAddress { get; set; }
        
        // Connection Status: Online, Offline, Degraded, Unknown
        [StringLength(20)]
        public string? IoTConnectionStatus { get; set; }
        
        public DateTime? LastIoTCommunication { get; set; }
        
        public int? IoTPollingIntervalSeconds { get; set; }
        
        [StringLength(200)]
        public string? DataHistorianTag { get; set; }
        
        [StringLength(200)]
        public string? SCADATag { get; set; }

        // ========== OEE (Overall Equipment Effectiveness) ==========
        
        public bool OEETracked { get; set; } = false;
        
        // Standard Run Rate (units per hour at full efficiency)
        [Column(TypeName = "decimal(12,4)")]
        public decimal? StandardRunRate { get; set; }
        
        [StringLength(20)]
        public string? StandardRunRateUOM { get; set; }
        
        // Ideal Cycle Time (seconds per unit)
        [Column(TypeName = "decimal(10,4)")]
        public decimal? IdealCycleTimeSeconds { get; set; }
        
        // OEE Targets (percentage 0-100)
        [Column(TypeName = "decimal(5,2)")]
        public decimal? TargetAvailability { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? TargetPerformance { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? TargetQuality { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? TargetOEE { get; set; }
        
        // Current OEE Values (calculated, stored for quick access)
        [Column(TypeName = "decimal(5,2)")]
        public decimal? CurrentAvailability { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? CurrentPerformance { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? CurrentQuality { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? CurrentOEE { get; set; }
        
        public DateTime? OEELastCalculated { get; set; }

        // ========== Calibration Tracking ==========
        
        public bool CalibrationRequired { get; set; } = false;
        
        [StringLength(50)]
        public string? CalibrationType { get; set; }
        
        public int? CalibrationFrequencyDays { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? LastCalibrationDate { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? NextCalibrationDue { get; set; }
        
        [StringLength(100)]
        public string? CalibrationCertificateNumber { get; set; }
        
        [StringLength(100)]
        public string? CalibrationVendor { get; set; }
        
        // Calibration Status: Current, Due, Overdue, Not Applicable
        [StringLength(20)]
        public string? CalibrationStatus { get; set; }

        // ========== Safety & Compliance ==========
        
        // Safety Classification: Critical, High, Medium, Low
        [StringLength(20)]
        public string? SafetyClassification { get; set; }
        
        public bool LockoutTagoutRequired { get; set; } = false;
        
        [StringLength(50)]
        public string? LOTOProcedureId { get; set; }
        
        public bool ConfinedSpaceEntry { get; set; } = false;
        
        public bool HotWorkPermitRequired { get; set; } = false;
        
        public bool HighVoltage { get; set; } = false;
        
        [StringLength(500)]
        public string? SafetyNotes { get; set; }
        
        // Regulatory/Environmental
        [StringLength(50)]
        public string? EnvironmentalClass { get; set; }
        
        public bool EmissionsMonitored { get; set; } = false;
        
        [StringLength(100)]
        public string? EPAPermitNumber { get; set; }
        
        [StringLength(100)]
        public string? OSHAClassification { get; set; }

        // ========== Energy Management ==========
        
        // Energy Efficiency Class: A+++, A++, A+, A, B, C, D, E, F, G
        [StringLength(10)]
        public string? EnergyClass { get; set; }
        
        [Column(TypeName = "decimal(12,2)")]
        public decimal? RatedPowerConsumptionKW { get; set; }
        
        [Column(TypeName = "decimal(12,2)")]
        public decimal? IdlePowerConsumptionKW { get; set; }
        
        [Column(TypeName = "decimal(12,2)")]
        public decimal? StandbyPowerConsumptionKW { get; set; }
        
        public bool HasStandbyMode { get; set; } = false;
        
        [Column(TypeName = "decimal(12,2)")]
        public decimal? AnnualEnergyConsumptionKWH { get; set; }
        
        [StringLength(50)]
        public string? EnergyMeterId { get; set; }

        // ========== Predictive Maintenance Thresholds ==========
        
        // Vibration Monitoring (mm/s RMS)
        [Column(TypeName = "decimal(8,3)")]
        public decimal? VibrationWarningThreshold { get; set; }
        
        [Column(TypeName = "decimal(8,3)")]
        public decimal? VibrationAlarmThreshold { get; set; }
        
        // Temperature Monitoring (Celsius)
        [Column(TypeName = "decimal(6,2)")]
        public decimal? TemperatureWarningThreshold { get; set; }
        
        [Column(TypeName = "decimal(6,2)")]
        public decimal? TemperatureAlarmThreshold { get; set; }
        
        // Pressure Monitoring (PSI or Bar)
        [Column(TypeName = "decimal(8,2)")]
        public decimal? PressureWarningThreshold { get; set; }
        
        [Column(TypeName = "decimal(8,2)")]
        public decimal? PressureAlarmThreshold { get; set; }
        
        [StringLength(10)]
        public string? PressureUOM { get; set; }
        
        // Current Sensor Readings (cached from IoT)
        [Column(TypeName = "decimal(8,3)")]
        public decimal? CurrentVibration { get; set; }
        
        [Column(TypeName = "decimal(6,2)")]
        public decimal? CurrentTemperature { get; set; }
        
        [Column(TypeName = "decimal(8,2)")]
        public decimal? CurrentPressure { get; set; }
        
        public DateTime? SensorReadingsLastUpdated { get; set; }
        
        // Predictive Health Score (0-100, calculated by ML model)
        [Column(TypeName = "decimal(5,2)")]
        public decimal? PredictiveHealthScore { get; set; }
        
        public DateTime? HealthScoreLastCalculated { get; set; }
        
        // Predicted Failure Date (from ML model)
        [DataType(DataType.Date)]
        public DateTime? PredictedFailureDate { get; set; }
        
        [StringLength(200)]
        public string? PredictedFailureReason { get; set; }

        // Optimistic concurrency token. Mapped to PostgreSQL system column `xmin`.
        // Configured in AppDbContext to be read-only and used as the concurrency token,
        // so EF will generate UPDATE/DELETE WHERE xmin = @original and throw
        // DbUpdateConcurrencyException when another transaction has modified the row.
        [Column("xmin", TypeName = "xid")]
        [ConcurrencyCheck]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public uint RowVersion { get; set; }

        // Related Entities
        public AssetTaxSettings? TaxSettings { get; set; }
        public UsTaxSettings? UsTaxSettings { get; set; }
        public AssetInventory? Inventory { get; set; }
        public ICollection<MaintenanceEvent>? MaintenanceEvents { get; set; }
        public ICollection<Attachment>? Attachments { get; set; }
        public MachineSpecification? MachineSpecification { get; set; }
    }

    public enum AssetCondition
    {
        Excellent = 0,
        Good = 1,
        Fair = 2,
        Poor = 3,
        Damaged = 4,
        NeedsRepair = 5,
        Obsolete = 6,
        Critical = 7
    }
}
