using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Seeding.Pipelines
{
    public class DemoPackV1Pipeline : ISeedPipeline
    {
        public string Name => "DemoPackV1";
        public string Version => "1.0.0";
        public string Description => "Demo Data Pack v1: Assets, PM Templates, PM Schedules for LAB environment";
        public bool IsDevOnly => true;

        private readonly List<ISeedStep> _steps;
        public IReadOnlyList<ISeedStep> Steps => _steps;

        public DemoPackV1Pipeline(AppDbContext context, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<DemoPackV1Pipeline>();
            _steps = new List<ISeedStep>
            {
                new DemoPackV1AssetsSeedStep(context, logger),
                new DemoPackV1PMTemplatesSeedStep(context, logger),
                new DemoPackV1PMSchedulesSeedStep(context, logger),
                new DemoPackV1CipSeedStep(context, logger)
            };
        }
    }

    #region DemoPackV1Assets
    public class DemoPackV1AssetsSeedStep : BaseSeedStep<Asset>
    {
        public override string StepName => "DemoPackV1Assets";
        public override string DomainName => "Assets";
        public override string NaturalKeyDescription => "AssetNumber";

        public DemoPackV1AssetsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Asset> GetSeedData() => new[]
        {
            new Asset { AssetNumber = "DEMO-CNC-001", Description = "CNC Vertical Milling Center", Model = "VMC-1100", SerialNumber = "VMC2024001", InServiceDate = new DateTime(2022, 1, 15), AcquisitionCost = 185000m, SalvageValue = 9250m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true, Priority = 1, AssetType = "Production" },
            new Asset { AssetNumber = "DEMO-CNC-002", Description = "CNC Lathe 4-Axis", Model = "LAT-600", SerialNumber = "LAT2023015", InServiceDate = new DateTime(2021, 6, 1), AcquisitionCost = 165000m, SalvageValue = 8250m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true, Priority = 1, AssetType = "Production" },
            new Asset { AssetNumber = "DEMO-CNC-003", Description = "Wire EDM Machine", Model = "EDM-400", SerialNumber = "EDM2024003", InServiceDate = new DateTime(2023, 3, 20), AcquisitionCost = 145000m, SalvageValue = 7250m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 2, AssetType = "Production" },
            new Asset { AssetNumber = "DEMO-PRESS-001", Description = "Hydraulic Press 200T", Model = "HP-200", SerialNumber = "HP2022087", InServiceDate = new DateTime(2020, 9, 10), AcquisitionCost = 95000m, SalvageValue = 4750m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true, Priority = 1, AssetType = "Production" },
            new Asset { AssetNumber = "DEMO-PRESS-002", Description = "Mechanical Press 100T", Model = "MP-100", SerialNumber = "MP2021055", InServiceDate = new DateTime(2019, 4, 5), AcquisitionCost = 75000m, SalvageValue = 3750m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 2, AssetType = "Production" },
            new Asset { AssetNumber = "DEMO-ROBOT-001", Description = "6-Axis Welding Robot", Model = "WR-2000", SerialNumber = "ROB2023012", InServiceDate = new DateTime(2023, 1, 10), AcquisitionCost = 125000m, SalvageValue = 6250m, UsefulLifeMonths = 84, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true, Priority = 1, AssetType = "Production" },
            new Asset { AssetNumber = "DEMO-ROBOT-002", Description = "Palletizing Robot", Model = "PAL-500", SerialNumber = "PAL2022101", InServiceDate = new DateTime(2022, 7, 15), AcquisitionCost = 95000m, SalvageValue = 4750m, UsefulLifeMonths = 84, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 2, AssetType = "Material Handling" },
            new Asset { AssetNumber = "DEMO-COMP-001", Description = "Rotary Screw Compressor 100HP", Model = "RSC-100", SerialNumber = "COMP2021045", InServiceDate = new DateTime(2020, 3, 1), AcquisitionCost = 55000m, SalvageValue = 2750m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true, Priority = 1, AssetType = "Utilities" },
            new Asset { AssetNumber = "DEMO-COMP-002", Description = "Rotary Screw Compressor 50HP", Model = "RSC-50", SerialNumber = "COMP2022078", InServiceDate = new DateTime(2021, 8, 15), AcquisitionCost = 35000m, SalvageValue = 1750m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 2, AssetType = "Utilities" },
            new Asset { AssetNumber = "DEMO-CRANE-001", Description = "Overhead Bridge Crane 20T", Model = "OBC-20", SerialNumber = "CR2019032", InServiceDate = new DateTime(2019, 4, 5), AcquisitionCost = 125000m, SalvageValue = 6250m, UsefulLifeMonths = 240, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true, Priority = 1, AssetType = "Material Handling" },
            new Asset { AssetNumber = "DEMO-CRANE-002", Description = "Jib Crane 5T", Model = "JIB-5", SerialNumber = "JIB2022011", InServiceDate = new DateTime(2021, 2, 28), AcquisitionCost = 25000m, SalvageValue = 1250m, UsefulLifeMonths = 180, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 3, AssetType = "Material Handling" },
            new Asset { AssetNumber = "DEMO-CONV-001", Description = "Powered Roller Conveyor Line A", Model = "PRC-100", SerialNumber = "CONV2021055", InServiceDate = new DateTime(2021, 2, 28), AcquisitionCost = 45000m, SalvageValue = 2250m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 2, AssetType = "Material Handling" },
            new Asset { AssetNumber = "DEMO-CONV-002", Description = "Powered Belt Conveyor Line B", Model = "PBC-50", SerialNumber = "CONV2022089", InServiceDate = new DateTime(2022, 5, 10), AcquisitionCost = 35000m, SalvageValue = 1750m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 3, AssetType = "Material Handling" },
            new Asset { AssetNumber = "DEMO-FORK-001", Description = "Electric Forklift 5000lb", Model = "EF-5000", SerialNumber = "FORK2023022", InServiceDate = new DateTime(2023, 3, 1), AcquisitionCost = 45000m, SalvageValue = 4500m, UsefulLifeMonths = 60, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 2, AssetType = "Material Handling" },
            new Asset { AssetNumber = "DEMO-FORK-002", Description = "Propane Forklift 8000lb", Model = "PF-8000", SerialNumber = "FORK2022044", InServiceDate = new DateTime(2022, 6, 15), AcquisitionCost = 38000m, SalvageValue = 3800m, UsefulLifeMonths = 60, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 3, AssetType = "Material Handling" },
            new Asset { AssetNumber = "DEMO-HVAC-001", Description = "Rooftop HVAC Unit Building A", Model = "RTU-500", SerialNumber = "HVAC2018099", InServiceDate = new DateTime(2018, 8, 10), AcquisitionCost = 85000m, SalvageValue = 4250m, UsefulLifeMonths = 180, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true, Priority = 1, AssetType = "Facilities" },
            new Asset { AssetNumber = "DEMO-HVAC-002", Description = "Split System HVAC Office", Model = "SS-250", SerialNumber = "HVAC2020045", InServiceDate = new DateTime(2020, 3, 20), AcquisitionCost = 35000m, SalvageValue = 1750m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 3, AssetType = "Facilities" },
            new Asset { AssetNumber = "DEMO-GRIND-001", Description = "Surface Grinder Precision", Model = "SG-1224", SerialNumber = "GRIND2022033", InServiceDate = new DateTime(2022, 4, 1), AcquisitionCost = 55000m, SalvageValue = 2750m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 2, AssetType = "Production" },
            new Asset { AssetNumber = "DEMO-SAW-001", Description = "Horizontal Band Saw", Model = "HBS-1216", SerialNumber = "SAW2021077", InServiceDate = new DateTime(2021, 9, 15), AcquisitionCost = 18000m, SalvageValue = 900m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 3, AssetType = "Production" },
            new Asset { AssetNumber = "DEMO-PUMP-001", Description = "Coolant Pump System", Model = "CP-150", SerialNumber = "PUMP2022011", InServiceDate = new DateTime(2022, 1, 20), AcquisitionCost = 12000m, SalvageValue = 600m, UsefulLifeMonths = 84, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false, Priority = 2, AssetType = "Utilities" }
        };

        protected override async Task<Asset?> FindByNaturalKeyAsync(Asset item, CancellationToken ct)
            => await Context.Assets.FirstOrDefaultAsync(x => x.AssetNumber == item.AssetNumber, ct);
        protected override string GetNaturalKeyValue(Asset item) => item.AssetNumber;
        protected override bool ShouldUpdate(Asset existing, Asset incoming)
            => !StringEquals(existing.Description, incoming.Description) || !StringEquals(existing.Model, incoming.Model)
               || !StringEquals(existing.SerialNumber, incoming.SerialNumber) || existing.AcquisitionCost != incoming.AcquisitionCost;
        protected override void UpdateEntity(Asset existing, Asset incoming)
        {
            existing.Description = incoming.Description;
            existing.Model = incoming.Model;
            existing.SerialNumber = incoming.SerialNumber;
            existing.AcquisitionCost = incoming.AcquisitionCost;
            existing.SalvageValue = incoming.SalvageValue;
            existing.IsCritical = incoming.IsCritical;
            existing.Priority = incoming.Priority;
            existing.AssetType = incoming.AssetType;
        }
    }
    #endregion

    #region DemoPackV1PMTemplates
    public class DemoPackV1PMTemplatesSeedStep : BaseSeedStep<PMTemplate>
    {
        public override string StepName => "DemoPackV1PMTemplates";
        public override string DomainName => "PMTemplates";
        public override string NaturalKeyDescription => "Code";

        public DemoPackV1PMTemplatesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<PMTemplate> GetSeedData() => new[]
        {
            new PMTemplate
            {
                Code = "PM-CNC-DAILY",
                Name = "CNC Machine Daily Inspection",
                Description = "Daily safety and operational checks for CNC machines",
                Type = MaintenanceType.Preventative,
                Priority = PMPriority.High,
                TriggerType = PMTriggerType.Calendar,
                CalendarInterval = RecurrenceType.Daily,
                CalendarIntervalValue = 1,
                EstimatedHours = 0.5m,
                EstimatedLaborCost = 35m,
                EstimatedPartsCost = 0m,
                EstimatedTotalCost = 35m,
                RequiresShutdown = false,
                RequiresLOTO = false,
                SkillLevel = "Basic",
                Craft = "Machinist",
                IsActive = true
            },
            new PMTemplate
            {
                Code = "PM-CNC-WEEKLY",
                Name = "CNC Machine Weekly Maintenance",
                Description = "Weekly lubrication, cleaning, and calibration checks for CNC equipment",
                Type = MaintenanceType.Preventative,
                Priority = PMPriority.Medium,
                TriggerType = PMTriggerType.Calendar,
                CalendarInterval = RecurrenceType.Weekly,
                CalendarIntervalValue = 1,
                EstimatedHours = 2.0m,
                EstimatedLaborCost = 140m,
                EstimatedPartsCost = 25m,
                EstimatedTotalCost = 165m,
                RequiresShutdown = true,
                RequiresLOTO = false,
                SkillLevel = "Intermediate",
                Craft = "Maintenance Technician",
                IsActive = true
            },
            new PMTemplate
            {
                Code = "PM-CNC-MONTHLY",
                Name = "CNC Machine Monthly Service",
                Description = "Monthly comprehensive inspection including spindle, coolant system, and axis calibration",
                Type = MaintenanceType.Preventative,
                Priority = PMPriority.Medium,
                TriggerType = PMTriggerType.Calendar,
                CalendarInterval = RecurrenceType.Monthly,
                CalendarIntervalValue = 1,
                EstimatedHours = 4.0m,
                EstimatedLaborCost = 280m,
                EstimatedPartsCost = 150m,
                EstimatedTotalCost = 430m,
                RequiresShutdown = true,
                RequiresLOTO = true,
                SkillLevel = "Advanced",
                Craft = "CNC Technician",
                IsActive = true
            },
            new PMTemplate
            {
                Code = "PM-COMP-WEEKLY",
                Name = "Air Compressor Weekly Inspection",
                Description = "Weekly checks on air compressor including filter, oil level, and belt tension",
                Type = MaintenanceType.Preventative,
                Priority = PMPriority.High,
                TriggerType = PMTriggerType.Calendar,
                CalendarInterval = RecurrenceType.Weekly,
                CalendarIntervalValue = 1,
                EstimatedHours = 1.0m,
                EstimatedLaborCost = 70m,
                EstimatedPartsCost = 10m,
                EstimatedTotalCost = 80m,
                RequiresShutdown = true,
                RequiresLOTO = true,
                SkillLevel = "Intermediate",
                Craft = "Maintenance Technician",
                IsActive = true
            },
            new PMTemplate
            {
                Code = "PM-COMP-QUARTERLY",
                Name = "Air Compressor Quarterly Service",
                Description = "Quarterly oil change, filter replacement, and comprehensive system inspection",
                Type = MaintenanceType.Preventative,
                Priority = PMPriority.Medium,
                TriggerType = PMTriggerType.Calendar,
                CalendarInterval = RecurrenceType.Monthly,
                CalendarIntervalValue = 3,
                EstimatedHours = 3.0m,
                EstimatedLaborCost = 210m,
                EstimatedPartsCost = 200m,
                EstimatedTotalCost = 410m,
                RequiresShutdown = true,
                RequiresLOTO = true,
                SkillLevel = "Advanced",
                Craft = "HVAC Technician",
                IsActive = true
            },
            new PMTemplate
            {
                Code = "PM-CRANE-MONTHLY",
                Name = "Overhead Crane Monthly Inspection",
                Description = "Monthly safety inspection including hoist, trolley, bridge, and limit switches",
                Type = MaintenanceType.Preventative,
                Priority = PMPriority.Critical,
                TriggerType = PMTriggerType.Calendar,
                CalendarInterval = RecurrenceType.Monthly,
                CalendarIntervalValue = 1,
                EstimatedHours = 2.5m,
                EstimatedLaborCost = 175m,
                EstimatedPartsCost = 50m,
                EstimatedTotalCost = 225m,
                RequiresShutdown = true,
                RequiresLOTO = true,
                SkillLevel = "Advanced",
                Craft = "Crane Technician",
                IsOEMRecommended = true,
                IsRegulatoryRequired = true,
                RegulatoryReference = "OSHA 1910.179",
                IsActive = true
            },
            new PMTemplate
            {
                Code = "PM-ROBOT-WEEKLY",
                Name = "Robot Cell Weekly Maintenance",
                Description = "Weekly robot arm inspection including teach pendant, cables, and safety sensors",
                Type = MaintenanceType.Preventative,
                Priority = PMPriority.High,
                TriggerType = PMTriggerType.Calendar,
                CalendarInterval = RecurrenceType.Weekly,
                CalendarIntervalValue = 1,
                EstimatedHours = 1.5m,
                EstimatedLaborCost = 105m,
                EstimatedPartsCost = 20m,
                EstimatedTotalCost = 125m,
                RequiresShutdown = true,
                RequiresLOTO = true,
                SkillLevel = "Advanced",
                Craft = "Robotics Technician",
                IsActive = true
            },
            new PMTemplate
            {
                Code = "PM-HVAC-MONTHLY",
                Name = "HVAC System Monthly Service",
                Description = "Monthly filter inspection, coil cleaning, and refrigerant check",
                Type = MaintenanceType.Preventative,
                Priority = PMPriority.Medium,
                TriggerType = PMTriggerType.Calendar,
                CalendarInterval = RecurrenceType.Monthly,
                CalendarIntervalValue = 1,
                EstimatedHours = 2.0m,
                EstimatedLaborCost = 140m,
                EstimatedPartsCost = 75m,
                EstimatedTotalCost = 215m,
                RequiresShutdown = false,
                RequiresLOTO = false,
                SkillLevel = "Intermediate",
                Craft = "HVAC Technician",
                IsActive = true
            },
            new PMTemplate
            {
                Code = "PM-FORK-DAILY",
                Name = "Forklift Daily Pre-Shift Inspection",
                Description = "Daily operator inspection before each shift including brakes, lights, horn, and fluid levels",
                Type = MaintenanceType.Preventative,
                Priority = PMPriority.High,
                TriggerType = PMTriggerType.Calendar,
                CalendarInterval = RecurrenceType.Daily,
                CalendarIntervalValue = 1,
                EstimatedHours = 0.25m,
                EstimatedLaborCost = 17.50m,
                EstimatedPartsCost = 0m,
                EstimatedTotalCost = 17.50m,
                RequiresShutdown = false,
                RequiresLOTO = false,
                SkillLevel = "Basic",
                Craft = "Operator",
                IsOEMRecommended = true,
                IsRegulatoryRequired = true,
                RegulatoryReference = "OSHA 1910.178(q)(7)",
                IsActive = true
            },
            new PMTemplate
            {
                Code = "PM-PRESS-WEEKLY",
                Name = "Hydraulic Press Weekly Safety Check",
                Description = "Weekly inspection of hydraulic system, safety guards, and two-hand controls",
                Type = MaintenanceType.Preventative,
                Priority = PMPriority.Critical,
                TriggerType = PMTriggerType.Calendar,
                CalendarInterval = RecurrenceType.Weekly,
                CalendarIntervalValue = 1,
                EstimatedHours = 1.5m,
                EstimatedLaborCost = 105m,
                EstimatedPartsCost = 25m,
                EstimatedTotalCost = 130m,
                RequiresShutdown = true,
                RequiresLOTO = true,
                SkillLevel = "Intermediate",
                Craft = "Maintenance Technician",
                IsActive = true
            }
        };

        protected override async Task<PMTemplate?> FindByNaturalKeyAsync(PMTemplate item, CancellationToken ct)
            => await Context.PMTemplates.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(PMTemplate item) => item.Code;
        protected override bool ShouldUpdate(PMTemplate existing, PMTemplate incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.EstimatedHours != incoming.EstimatedHours || existing.EstimatedTotalCost != incoming.EstimatedTotalCost;
        protected override void UpdateEntity(PMTemplate existing, PMTemplate incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.Type = incoming.Type;
            existing.Priority = incoming.Priority;
            existing.TriggerType = incoming.TriggerType;
            existing.CalendarInterval = incoming.CalendarInterval;
            existing.CalendarIntervalValue = incoming.CalendarIntervalValue;
            existing.EstimatedHours = incoming.EstimatedHours;
            existing.EstimatedLaborCost = incoming.EstimatedLaborCost;
            existing.EstimatedPartsCost = incoming.EstimatedPartsCost;
            existing.EstimatedTotalCost = incoming.EstimatedTotalCost;
            existing.RequiresShutdown = incoming.RequiresShutdown;
            existing.RequiresLOTO = incoming.RequiresLOTO;
            existing.SkillLevel = incoming.SkillLevel;
            existing.Craft = incoming.Craft;
            existing.IsOEMRecommended = incoming.IsOEMRecommended;
            existing.IsRegulatoryRequired = incoming.IsRegulatoryRequired;
            existing.RegulatoryReference = incoming.RegulatoryReference;
            existing.IsActive = incoming.IsActive;
        }
    }
    #endregion

    #region DemoPackV1PMSchedules
    public class DemoPackV1PMSchedulesSeedStep : BaseSeedStep<PMSchedule>
    {
        public override string StepName => "DemoPackV1PMSchedules";
        public override string DomainName => "PMSchedules";
        public override string NaturalKeyDescription => "Name (linked to PMTemplate.Code)";

        private Dictionary<string, int> _templateIdMap = new();
        private int? _tenantId;
        private int? _companyId;
        private int? _siteId;

        public DemoPackV1PMSchedulesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override async Task OnBeforeExecuteAsync(CancellationToken cancellationToken)
        {
            _templateIdMap = await Context.PMTemplates
                .Where(t => t.Code.StartsWith("PM-"))
                .ToDictionaryAsync(t => t.Code, t => t.Id, cancellationToken);

            // Get tenant context - use first tenant or null if single-tenant mode
            var tenant = await Context.Tenants.FirstOrDefaultAsync(cancellationToken);
            _tenantId = tenant?.Id;

            // Get first company for the tenant
            var company = await Context.Companies.FirstOrDefaultAsync(cancellationToken);
            _companyId = company?.Id;

            // Get first site for the company
            if (_companyId.HasValue)
            {
                var site = await Context.Sites
                    .Where(s => s.CompanyId == _companyId)
                    .FirstOrDefaultAsync(cancellationToken);
                _siteId = site?.Id;
            }
        }

        protected override IEnumerable<PMSchedule> GetSeedData()
        {
            var schedules = new List<PMSchedule>();
            var startDate = DateTime.UtcNow.Date.AddDays(-7);
            var nextDue = DateTime.UtcNow.Date.AddDays(3); // Set initial next due date for visibility

            if (_templateIdMap.TryGetValue("PM-CNC-DAILY", out var cncDailyId))
            {
                schedules.Add(new PMSchedule
                {
                    Name = "CNC-001 Daily Inspection",
                    Description = "Daily inspection for CNC Vertical Milling Center DEMO-CNC-001",
                    PMTemplateId = cncDailyId,
                    TenantId = _tenantId,
                    CompanyId = _companyId,
                    SiteId = _siteId,
                    Active = true,
                    StartDateUtc = startDate,
                    NextDueDateUtc = nextDue,
                    CadenceType = PMCadenceType.IntervalDays,
                    IntervalDays = 1,
                    LeadDays = 0,
                    TimeZoneId = "America/New_York"
                });
            }

            if (_templateIdMap.TryGetValue("PM-CNC-WEEKLY", out var cncWeeklyId))
            {
                schedules.Add(new PMSchedule
                {
                    Name = "CNC-001 Weekly Maintenance",
                    Description = "Weekly maintenance for CNC Vertical Milling Center DEMO-CNC-001",
                    PMTemplateId = cncWeeklyId,
                    TenantId = _tenantId,
                    CompanyId = _companyId,
                    SiteId = _siteId,
                    Active = true,
                    StartDateUtc = startDate,
                    NextDueDateUtc = nextDue.AddDays(2),
                    CadenceType = PMCadenceType.Weekly,
                    DaysOfWeekMask = 2,
                    LeadDays = 1,
                    TimeZoneId = "America/New_York"
                });

                schedules.Add(new PMSchedule
                {
                    Name = "CNC-002 Weekly Maintenance",
                    Description = "Weekly maintenance for CNC Lathe 4-Axis DEMO-CNC-002",
                    PMTemplateId = cncWeeklyId,
                    TenantId = _tenantId,
                    CompanyId = _companyId,
                    SiteId = _siteId,
                    Active = true,
                    StartDateUtc = startDate,
                    NextDueDateUtc = nextDue.AddDays(4),
                    CadenceType = PMCadenceType.Weekly,
                    DaysOfWeekMask = 4,
                    LeadDays = 1,
                    TimeZoneId = "America/New_York"
                });
            }

            if (_templateIdMap.TryGetValue("PM-CNC-MONTHLY", out var cncMonthlyId))
            {
                schedules.Add(new PMSchedule
                {
                    Name = "CNC Fleet Monthly Service",
                    Description = "Monthly comprehensive service for all CNC machines",
                    PMTemplateId = cncMonthlyId,
                    TenantId = _tenantId,
                    CompanyId = _companyId,
                    SiteId = _siteId,
                    Active = true,
                    StartDateUtc = startDate,
                    NextDueDateUtc = nextDue.AddDays(10),
                    CadenceType = PMCadenceType.Monthly,
                    DayOfMonth = 15,
                    LeadDays = 3,
                    TimeZoneId = "America/New_York"
                });
            }

            if (_templateIdMap.TryGetValue("PM-COMP-WEEKLY", out var compWeeklyId))
            {
                schedules.Add(new PMSchedule
                {
                    Name = "Compressor-001 Weekly Inspection",
                    Description = "Weekly inspection for Rotary Screw Compressor 100HP",
                    PMTemplateId = compWeeklyId,
                    TenantId = _tenantId,
                    CompanyId = _companyId,
                    SiteId = _siteId,
                    Active = true,
                    StartDateUtc = startDate,
                    NextDueDateUtc = nextDue.AddDays(1),
                    CadenceType = PMCadenceType.Weekly,
                    DaysOfWeekMask = 2,
                    LeadDays = 0,
                    TimeZoneId = "America/New_York"
                });
            }

            if (_templateIdMap.TryGetValue("PM-CRANE-MONTHLY", out var craneMonthlyId))
            {
                schedules.Add(new PMSchedule
                {
                    Name = "Crane-001 Monthly Inspection",
                    Description = "Monthly OSHA-required inspection for 20T Overhead Bridge Crane",
                    PMTemplateId = craneMonthlyId,
                    TenantId = _tenantId,
                    CompanyId = _companyId,
                    SiteId = _siteId,
                    Active = true,
                    StartDateUtc = startDate,
                    NextDueDateUtc = nextDue.AddDays(25),
                    CadenceType = PMCadenceType.Monthly,
                    DayOfMonth = 1,
                    LeadDays = 2,
                    TimeZoneId = "America/New_York"
                });
            }

            if (_templateIdMap.TryGetValue("PM-ROBOT-WEEKLY", out var robotWeeklyId))
            {
                schedules.Add(new PMSchedule
                {
                    Name = "Robot-001 Weekly Maintenance",
                    Description = "Weekly maintenance for 6-Axis Welding Robot",
                    PMTemplateId = robotWeeklyId,
                    TenantId = _tenantId,
                    CompanyId = _companyId,
                    SiteId = _siteId,
                    Active = true,
                    StartDateUtc = startDate,
                    NextDueDateUtc = nextDue.AddDays(5),
                    CadenceType = PMCadenceType.Weekly,
                    DaysOfWeekMask = 8,
                    LeadDays = 1,
                    TimeZoneId = "America/New_York"
                });
            }

            if (_templateIdMap.TryGetValue("PM-HVAC-MONTHLY", out var hvacMonthlyId))
            {
                schedules.Add(new PMSchedule
                {
                    Name = "HVAC Building A Monthly Service",
                    Description = "Monthly service for Rooftop HVAC Unit Building A",
                    PMTemplateId = hvacMonthlyId,
                    TenantId = _tenantId,
                    CompanyId = _companyId,
                    SiteId = _siteId,
                    Active = true,
                    StartDateUtc = startDate,
                    NextDueDateUtc = nextDue.AddDays(8),
                    CadenceType = PMCadenceType.Monthly,
                    DayOfMonth = 10,
                    LeadDays = 2,
                    TimeZoneId = "America/New_York"
                });
            }

            if (_templateIdMap.TryGetValue("PM-PRESS-WEEKLY", out var pressWeeklyId))
            {
                schedules.Add(new PMSchedule
                {
                    Name = "Press-001 Weekly Safety Check",
                    Description = "Weekly safety inspection for 200T Hydraulic Press",
                    PMTemplateId = pressWeeklyId,
                    TenantId = _tenantId,
                    CompanyId = _companyId,
                    SiteId = _siteId,
                    Active = true,
                    StartDateUtc = startDate,
                    NextDueDateUtc = nextDue.AddDays(6),
                    CadenceType = PMCadenceType.Weekly,
                    DaysOfWeekMask = 2,
                    LeadDays = 0,
                    TimeZoneId = "America/New_York"
                });
            }

            return schedules;
        }

        protected override async Task<PMSchedule?> FindByNaturalKeyAsync(PMSchedule item, CancellationToken ct)
            => await Context.PMSchedules.FirstOrDefaultAsync(x => x.Name == item.Name, ct);
        protected override string GetNaturalKeyValue(PMSchedule item) => item.Name;
        protected override bool ShouldUpdate(PMSchedule existing, PMSchedule incoming)
            => !StringEquals(existing.Description, incoming.Description) || existing.PMTemplateId != incoming.PMTemplateId
               || existing.Active != incoming.Active || existing.CadenceType != incoming.CadenceType;
        protected override void UpdateEntity(PMSchedule existing, PMSchedule incoming)
        {
            existing.Description = incoming.Description;
            existing.PMTemplateId = incoming.PMTemplateId;
            existing.Active = incoming.Active;
            existing.CadenceType = incoming.CadenceType;
            existing.IntervalDays = incoming.IntervalDays;
            existing.DaysOfWeekMask = incoming.DaysOfWeekMask;
            existing.DayOfMonth = incoming.DayOfMonth;
            existing.LeadDays = incoming.LeadDays;
            existing.TimeZoneId = incoming.TimeZoneId;
        }
    }
    #endregion

    #region DemoPackV1CipProjects
    public class DemoPackV1CipSeedStep : ISeedStep
    {
        public string StepName => "DemoPackV1CipProjects";
        public string DomainName => "CipProjects";
        public string NaturalKeyDescription => "ProjectNumber";

        private readonly AppDbContext _context;
        private readonly ILogger _logger;

        public DemoPackV1CipSeedStep(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new SeedStepResult
            {
                StepName = StepName,
                DomainName = DomainName,
                StartTime = DateTime.UtcNow
            };

            try
            {
                var projects = await _context.CipProjects
                    .Include(p => p.Costs)
                    .ToListAsync(cancellationToken);

                foreach (var project in projects)
                {
                    var costCount = project.Costs?.Count ?? 0;
                    var costSum = project.Costs?.Sum(c => c.Amount) ?? 0m;

                    if (project.TotalCosts > 0 && costCount == 0)
                    {
                        var breakdown = GenerateDeterministicCosts(project);
                        foreach (var cost in breakdown)
                        {
                            _context.CipCosts.Add(cost);
                        }
                        project.TotalCosts = breakdown.Sum(c => c.Amount);
                        result.Inserted += breakdown.Count;
                        _logger.LogInformation(
                            "CIP {ProjectNumber}: seeded {Count} cost rows totaling {Total:C} to match TotalCosts",
                            project.ProjectNumber, breakdown.Count, project.TotalCosts);
                    }
                    else
                    {
                        project.TotalCosts = costSum;
                        result.Skipped++;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"CIP seed error: {ex.Message}");
                _logger.LogError(ex, "Error in CIP seed step");
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }

        public Task<PreviewStepResult> PreviewAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PreviewStepResult
            {
                StepName = StepName,
                DomainName = DomainName
            });
        }

        private static List<CipCost> GenerateDeterministicCosts(CipProject project)
        {
            var total = project.TotalCosts;
            var baseDate = project.StartDate;
            var costs = new List<CipCost>();

            var portions = new[] { 0.35m, 0.25m, 0.20m, 0.12m, 0.08m };
            var descriptions = new[]
            {
                "Engineering & Design",
                "Materials & Equipment",
                "Labor - Installation",
                "Permits & Inspection",
                "Contingency & Miscellaneous"
            };
            var costTypes = new[]
            {
                CipCostType.Engineering,
                CipCostType.Materials,
                CipCostType.Construction,
                CipCostType.Permits,
                CipCostType.Other
            };

            decimal running = 0m;
            for (int i = 0; i < portions.Length; i++)
            {
                var amount = i == portions.Length - 1
                    ? total - running
                    : Math.Round(total * portions[i], 2);

                costs.Add(new CipCost
                {
                    CipProjectId = project.Id,
                    Description = descriptions[i],
                    CostType = costTypes[i],
                    TransactionDate = baseDate.AddDays(30 * (i + 1)),
                    Amount = amount
                });
                running += amount;
            }

            return costs;
        }
    }
    #endregion
}
