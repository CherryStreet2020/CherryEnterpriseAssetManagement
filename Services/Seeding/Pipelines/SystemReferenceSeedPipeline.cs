using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Seeding.Pipelines
{
    public class SystemReferenceSeedPipeline : ISeedPipeline
    {
        public string Name => "SystemReferenceSeed";
        public string Version => "1.0.0";
        public string Description => "Core system reference data: WO types, failure/cause/action/problem codes, crafts, priorities, numbering, currencies, tax limits";
        public bool IsDevOnly => false;

        private readonly List<ISeedStep> _steps;
        public IReadOnlyList<ISeedStep> Steps => _steps;

        public SystemReferenceSeedPipeline(AppDbContext context, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<SystemReferenceSeedPipeline>();
            _steps = new List<ISeedStep>
            {
                new WorkOrderTypesSeedStep(context, logger),
                new FailureCodesSeedStep(context, logger),
                new CauseCodesSeedStep(context, logger),
                new ActionCodesSeedStep(context, logger),
                new ProblemCodesSeedStep(context, logger),
                new PriorityLevelsSeedStep(context, logger),
                new CraftsSeedStep(context, logger),
                new MaintenanceTypeCodesSeedStep(context, logger),
                new LaborTypesSeedStep(context, logger),
                new SkillsSeedStep(context, logger),
                new NumberingSequencesSeedStep(context, logger),
                new PaymentTermsSeedStep(context, logger),
                new CurrenciesSeedStep(context, logger),
                new UOMDefinitionsSeedStep(context, logger),
                new ShippingMethodsSeedStep(context, logger),
                new TaxCodesSeedStep(context, logger),
                new Section179LimitsSeedStep(context, logger),
                new BonusDepreciationRatesSeedStep(context, logger),
                new CcaClassesSeedStep(context, logger)
            };
        }
    }

    #region WorkOrderTypes
    public class WorkOrderTypesSeedStep : BaseSeedStep<WorkOrderType>
    {
        public override string StepName => "WorkOrderTypes";
        public override string DomainName => "WorkOrderTypes";
        public override string NaturalKeyDescription => "Code";

        public WorkOrderTypesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<WorkOrderType> GetSeedData() => new[]
        {
            new WorkOrderType { Code = "CM", Name = "Corrective Maintenance", Description = "Unplanned repairs", Category = WorkOrderCategory.Corrective, RequiresApproval = false, IsActive = true, SortOrder = 1 },
            new WorkOrderType { Code = "PM", Name = "Preventive Maintenance", Description = "Scheduled maintenance", Category = WorkOrderCategory.Preventive, RequiresApproval = false, IsActive = true, SortOrder = 2 },
            new WorkOrderType { Code = "PDM", Name = "Predictive Maintenance", Description = "Condition-based maintenance", Category = WorkOrderCategory.Predictive, RequiresApproval = false, IsActive = true, SortOrder = 3 },
            new WorkOrderType { Code = "EM", Name = "Emergency Maintenance", Description = "Urgent breakdown repairs", Category = WorkOrderCategory.Emergency, RequiresApproval = false, IsActive = true, SortOrder = 4 },
            new WorkOrderType { Code = "CAL", Name = "Calibration", Description = "Calibration and certification", Category = WorkOrderCategory.Calibration, RequiresApproval = false, IsActive = true, SortOrder = 5 },
            new WorkOrderType { Code = "INS", Name = "Inspection", Description = "Routine inspection", Category = WorkOrderCategory.Inspection, RequiresApproval = false, IsActive = true, SortOrder = 6 },
            new WorkOrderType { Code = "OVH", Name = "Overhaul", Description = "Major overhaul or rebuild", Category = WorkOrderCategory.Project, RequiresApproval = true, IsActive = true, SortOrder = 7 },
            new WorkOrderType { Code = "MOD", Name = "Modification", Description = "Equipment modification", Category = WorkOrderCategory.Project, RequiresApproval = true, IsActive = true, SortOrder = 8 },
            new WorkOrderType { Code = "INST", Name = "Installation", Description = "New equipment installation", Category = WorkOrderCategory.Project, RequiresApproval = true, IsActive = true, SortOrder = 9 },
            new WorkOrderType { Code = "REM", Name = "Removal", Description = "Equipment removal/decommission", Category = WorkOrderCategory.Project, RequiresApproval = true, IsActive = true, SortOrder = 10 },
            new WorkOrderType { Code = "PROJ", Name = "Project Work", Description = "Capital project work", Category = WorkOrderCategory.Project, RequiresApproval = true, IsActive = true, SortOrder = 11 },
            new WorkOrderType { Code = "SAF", Name = "Safety Work", Description = "Safety-related work", Category = WorkOrderCategory.Safety, RequiresApproval = true, IsActive = true, SortOrder = 12 }
        };

        protected override async Task<WorkOrderType?> FindByNaturalKeyAsync(WorkOrderType item, CancellationToken ct)
            => await Context.WorkOrderTypes.FirstOrDefaultAsync(x => x.Code == item.Code, ct);

        protected override string GetNaturalKeyValue(WorkOrderType item) => item.Code;
        protected override bool ShouldUpdate(WorkOrderType existing, WorkOrderType incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.Category != incoming.Category || existing.RequiresApproval != incoming.RequiresApproval
               || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(WorkOrderType existing, WorkOrderType incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.Category = incoming.Category;
            existing.RequiresApproval = incoming.RequiresApproval;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region FailureCodes
    public class FailureCodesSeedStep : BaseSeedStep<FailureCode>
    {
        public override string StepName => "FailureCodes";
        public override string DomainName => "FailureCodes";
        public override string NaturalKeyDescription => "Code";

        public FailureCodesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<FailureCode> GetSeedData() => new[]
        {
            new FailureCode { Code = "BEAR", Name = "Bearing Failure", Description = "Bearing wear or damage", Category = FailureCategory.Mechanical, IsActive = true, SortOrder = 1 },
            new FailureCode { Code = "BELT", Name = "Belt Failure", Description = "Belt wear or breakage", Category = FailureCategory.Mechanical, IsActive = true, SortOrder = 2 },
            new FailureCode { Code = "GEAR", Name = "Gear Failure", Description = "Gear tooth wear or breakage", Category = FailureCategory.Mechanical, IsActive = true, SortOrder = 3 },
            new FailureCode { Code = "SEAL", Name = "Seal Leak", Description = "Seal failure causing leaks", Category = FailureCategory.Mechanical, IsActive = true, SortOrder = 4 },
            new FailureCode { Code = "PUMP", Name = "Pump Failure", Description = "Pump malfunction", Category = FailureCategory.Mechanical, IsActive = true, SortOrder = 5 },
            new FailureCode { Code = "MOTR", Name = "Motor Failure", Description = "Electric motor failure", Category = FailureCategory.Electrical, IsActive = true, SortOrder = 6 },
            new FailureCode { Code = "SENS", Name = "Sensor Failure", Description = "Sensor malfunction or drift", Category = FailureCategory.Electronic, IsActive = true, SortOrder = 7 },
            new FailureCode { Code = "CTRL", Name = "Control Failure", Description = "PLC or control system issue", Category = FailureCategory.Electronic, IsActive = true, SortOrder = 8 },
            new FailureCode { Code = "WIRE", Name = "Wiring Issue", Description = "Electrical wiring problem", Category = FailureCategory.Electrical, IsActive = true, SortOrder = 9 },
            new FailureCode { Code = "HOSE", Name = "Hose Failure", Description = "Hydraulic/pneumatic hose failure", Category = FailureCategory.Hydraulic, IsActive = true, SortOrder = 10 },
            new FailureCode { Code = "VALV", Name = "Valve Failure", Description = "Valve malfunction", Category = FailureCategory.Hydraulic, IsActive = true, SortOrder = 11 },
            new FailureCode { Code = "CYLN", Name = "Cylinder Failure", Description = "Hydraulic cylinder issue", Category = FailureCategory.Hydraulic, IsActive = true, SortOrder = 12 },
            new FailureCode { Code = "PNEU", Name = "Pneumatic Failure", Description = "Air system component failure", Category = FailureCategory.Pneumatic, IsActive = true, SortOrder = 13 },
            new FailureCode { Code = "CORR", Name = "Corrosion", Description = "Corrosion damage", Category = FailureCategory.Structural, IsActive = true, SortOrder = 14 },
            new FailureCode { Code = "FATG", Name = "Fatigue Failure", Description = "Metal fatigue or cracking", Category = FailureCategory.Structural, IsActive = true, SortOrder = 15 },
            new FailureCode { Code = "SOFT", Name = "Software Issue", Description = "Software bug or configuration", Category = FailureCategory.Software, IsActive = true, SortOrder = 16 },
            new FailureCode { Code = "CONT", Name = "Contamination", Description = "Fluid or material contamination", Category = FailureCategory.Environmental, IsActive = true, SortOrder = 17 },
            new FailureCode { Code = "OVHT", Name = "Overheating", Description = "Thermal damage from overheating", Category = FailureCategory.Environmental, IsActive = true, SortOrder = 18 },
            new FailureCode { Code = "VIBR", Name = "Excessive Vibration", Description = "Vibration-induced failure", Category = FailureCategory.Mechanical, IsActive = true, SortOrder = 19 },
            new FailureCode { Code = "LUBR", Name = "Lubrication Failure", Description = "Inadequate or wrong lubrication", Category = FailureCategory.Mechanical, IsActive = true, SortOrder = 20 }
        };

        protected override async Task<FailureCode?> FindByNaturalKeyAsync(FailureCode item, CancellationToken ct)
            => await Context.FailureCodes.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(FailureCode item) => item.Code;
        protected override bool ShouldUpdate(FailureCode existing, FailureCode incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.Category != incoming.Category || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(FailureCode existing, FailureCode incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.Category = incoming.Category;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region CauseCodes
    public class CauseCodesSeedStep : BaseSeedStep<CauseCode>
    {
        public override string StepName => "CauseCodes";
        public override string DomainName => "CauseCodes";
        public override string NaturalKeyDescription => "Code";

        public CauseCodesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<CauseCode> GetSeedData() => new[]
        {
            new CauseCode { Code = "WEAR", Name = "Normal Wear", Description = "End of component life", IsActive = true, SortOrder = 1 },
            new CauseCode { Code = "LACK", Name = "Lack of Maintenance", Description = "Missed or delayed PM", IsActive = true, SortOrder = 2 },
            new CauseCode { Code = "IMPR", Name = "Improper Operation", Description = "Incorrect use of equipment", IsActive = true, SortOrder = 3 },
            new CauseCode { Code = "MATL", Name = "Material Defect", Description = "Defective material or part", IsActive = true, SortOrder = 4 },
            new CauseCode { Code = "INST", Name = "Improper Installation", Description = "Incorrect installation", IsActive = true, SortOrder = 5 },
            new CauseCode { Code = "DSGN", Name = "Design Flaw", Description = "Inherent design issue", IsActive = true, SortOrder = 6 },
            new CauseCode { Code = "OVER", Name = "Overload", Description = "Exceeded capacity", IsActive = true, SortOrder = 7 },
            new CauseCode { Code = "CONT", Name = "Contamination", Description = "Foreign material contamination", IsActive = true, SortOrder = 8 },
            new CauseCode { Code = "LUBR", Name = "Lubrication Issue", Description = "Wrong or insufficient lubricant", IsActive = true, SortOrder = 9 },
            new CauseCode { Code = "ALIG", Name = "Misalignment", Description = "Component misalignment", IsActive = true, SortOrder = 10 },
            new CauseCode { Code = "TEMP", Name = "Temperature Extreme", Description = "Abnormal temperature exposure", IsActive = true, SortOrder = 11 },
            new CauseCode { Code = "VIBR", Name = "Vibration", Description = "Excessive vibration", IsActive = true, SortOrder = 12 },
            new CauseCode { Code = "CORR", Name = "Corrosion", Description = "Chemical or environmental corrosion", IsActive = true, SortOrder = 13 },
            new CauseCode { Code = "AGED", Name = "Age/Obsolescence", Description = "Equipment beyond useful life", IsActive = true, SortOrder = 14 }
        };

        protected override async Task<CauseCode?> FindByNaturalKeyAsync(CauseCode item, CancellationToken ct)
            => await Context.CauseCodes.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(CauseCode item) => item.Code;
        protected override bool ShouldUpdate(CauseCode existing, CauseCode incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(CauseCode existing, CauseCode incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region ActionCodes
    public class ActionCodesSeedStep : BaseSeedStep<ActionCode>
    {
        public override string StepName => "ActionCodes";
        public override string DomainName => "ActionCodes";
        public override string NaturalKeyDescription => "Code";

        public ActionCodesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<ActionCode> GetSeedData() => new[]
        {
            new ActionCode { Code = "REP", Name = "Repair", Description = "Repair component", Category = ActionCategory.Repair, RequiresParts = true, IsActive = true, SortOrder = 1 },
            new ActionCode { Code = "REPL", Name = "Replace", Description = "Replace component", Category = ActionCategory.Replace, RequiresParts = true, IsActive = true, SortOrder = 2 },
            new ActionCode { Code = "ADJ", Name = "Adjust", Description = "Adjust or calibrate", Category = ActionCategory.Adjust, RequiresParts = false, IsActive = true, SortOrder = 3 },
            new ActionCode { Code = "CLN", Name = "Clean", Description = "Clean component", Category = ActionCategory.Clean, RequiresParts = false, IsActive = true, SortOrder = 4 },
            new ActionCode { Code = "LUBR", Name = "Lubricate", Description = "Lubricate component", Category = ActionCategory.Lubricate, RequiresParts = true, IsActive = true, SortOrder = 5 },
            new ActionCode { Code = "TGHT", Name = "Tighten", Description = "Tighten fasteners", Category = ActionCategory.Adjust, RequiresParts = false, IsActive = true, SortOrder = 6 },
            new ActionCode { Code = "ALIG", Name = "Align", Description = "Align components", Category = ActionCategory.Adjust, RequiresParts = false, IsActive = true, SortOrder = 7 },
            new ActionCode { Code = "REBLD", Name = "Rebuild", Description = "Rebuild assembly", Category = ActionCategory.Overhaul, RequiresParts = true, IsActive = true, SortOrder = 8 },
            new ActionCode { Code = "INSP", Name = "Inspect", Description = "Inspect and report", Category = ActionCategory.Inspect, RequiresParts = false, IsActive = true, SortOrder = 9 },
            new ActionCode { Code = "TEST", Name = "Test", Description = "Test and verify", Category = ActionCategory.Test, RequiresParts = false, IsActive = true, SortOrder = 10 },
            new ActionCode { Code = "CAL", Name = "Calibrate", Description = "Calibrate instrument", Category = ActionCategory.Calibrate, RequiresParts = false, IsActive = true, SortOrder = 11 },
            new ActionCode { Code = "SEAL", Name = "Reseal", Description = "Replace seals", Category = ActionCategory.Replace, RequiresParts = true, IsActive = true, SortOrder = 12 },
            new ActionCode { Code = "FLUSH", Name = "Flush System", Description = "Flush and refill", Category = ActionCategory.Clean, RequiresParts = true, IsActive = true, SortOrder = 13 }
        };

        protected override async Task<ActionCode?> FindByNaturalKeyAsync(ActionCode item, CancellationToken ct)
            => await Context.ActionCodes.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(ActionCode item) => item.Code;
        protected override bool ShouldUpdate(ActionCode existing, ActionCode incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.Category != incoming.Category || existing.RequiresParts != incoming.RequiresParts
               || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(ActionCode existing, ActionCode incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.Category = incoming.Category;
            existing.RequiresParts = incoming.RequiresParts;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region ProblemCodes
    public class ProblemCodesSeedStep : BaseSeedStep<ProblemCode>
    {
        public override string StepName => "ProblemCodes";
        public override string DomainName => "ProblemCodes";
        public override string NaturalKeyDescription => "Code";

        public ProblemCodesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<ProblemCode> GetSeedData() => new[]
        {
            new ProblemCode { Code = "NRUN", Name = "Won't Run", Description = "Equipment will not start", IsActive = true, SortOrder = 1 },
            new ProblemCode { Code = "NOIS", Name = "Abnormal Noise", Description = "Unusual sound during operation", IsActive = true, SortOrder = 2 },
            new ProblemCode { Code = "VIBR", Name = "Excessive Vibration", Description = "Abnormal vibration", IsActive = true, SortOrder = 3 },
            new ProblemCode { Code = "LEAK", Name = "Leaking", Description = "Fluid leak detected", IsActive = true, SortOrder = 4 },
            new ProblemCode { Code = "OVHT", Name = "Overheating", Description = "Running too hot", IsActive = true, SortOrder = 5 },
            new ProblemCode { Code = "SLOW", Name = "Running Slow", Description = "Below normal speed/output", IsActive = true, SortOrder = 6 },
            new ProblemCode { Code = "QUAL", Name = "Quality Issue", Description = "Output quality problem", IsActive = true, SortOrder = 7 },
            new ProblemCode { Code = "ALRM", Name = "Alarm Active", Description = "Equipment alarm triggered", IsActive = true, SortOrder = 8 },
            new ProblemCode { Code = "INTM", Name = "Intermittent", Description = "Intermittent operation", IsActive = true, SortOrder = 9 },
            new ProblemCode { Code = "ERRM", Name = "Error Message", Description = "Error displayed on HMI", IsActive = true, SortOrder = 10 },
            new ProblemCode { Code = "PRES", Name = "Pressure Issue", Description = "Abnormal pressure", IsActive = true, SortOrder = 11 },
            new ProblemCode { Code = "ELEC", Name = "Electrical Problem", Description = "Electrical fault indication", IsActive = true, SortOrder = 12 }
        };

        protected override async Task<ProblemCode?> FindByNaturalKeyAsync(ProblemCode item, CancellationToken ct)
            => await Context.ProblemCodes.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(ProblemCode item) => item.Code;
        protected override bool ShouldUpdate(ProblemCode existing, ProblemCode incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(ProblemCode existing, ProblemCode incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region PriorityLevels
    public class PriorityLevelsSeedStep : BaseSeedStep<PriorityLevel>
    {
        public override string StepName => "PriorityLevels";
        public override string DomainName => "PriorityLevels";
        public override string NaturalKeyDescription => "Code";

        public PriorityLevelsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<PriorityLevel> GetSeedData() => new[]
        {
            new PriorityLevel { Code = "EMER", Name = "Emergency", Description = "Immediate action required", ResponseTimeHours = 1, Color = "#dc2626", IsDefault = false, IsActive = true, SortOrder = 1 },
            new PriorityLevel { Code = "HIGH", Name = "High", Description = "Urgent - same day", ResponseTimeHours = 8, Color = "#ea580c", IsDefault = false, IsActive = true, SortOrder = 2 },
            new PriorityLevel { Code = "MED", Name = "Medium", Description = "Within 48 hours", ResponseTimeHours = 48, Color = "#ca8a04", IsDefault = true, IsActive = true, SortOrder = 3 },
            new PriorityLevel { Code = "LOW", Name = "Low", Description = "Within 1 week", ResponseTimeHours = 168, Color = "#16a34a", IsDefault = false, IsActive = true, SortOrder = 4 },
            new PriorityLevel { Code = "PLAN", Name = "Planned", Description = "Scheduled work", ResponseTimeHours = 720, Color = "#2563eb", IsDefault = false, IsActive = true, SortOrder = 5 },
            new PriorityLevel { Code = "PROJ", Name = "Project", Description = "Project timeline", ResponseTimeHours = 2160, Color = "#7c3aed", IsDefault = false, IsActive = true, SortOrder = 6 }
        };

        protected override async Task<PriorityLevel?> FindByNaturalKeyAsync(PriorityLevel item, CancellationToken ct)
            => await Context.PriorityLevels.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(PriorityLevel item) => item.Code;
        protected override bool ShouldUpdate(PriorityLevel existing, PriorityLevel incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.ResponseTimeHours != incoming.ResponseTimeHours || !StringEquals(existing.Color, incoming.Color)
               || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(PriorityLevel existing, PriorityLevel incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.ResponseTimeHours = incoming.ResponseTimeHours;
            existing.Color = incoming.Color;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region Crafts
    public class CraftsSeedStep : BaseSeedStep<Craft>
    {
        public override string StepName => "Crafts";
        public override string DomainName => "Crafts";
        public override string NaturalKeyDescription => "Code";

        public CraftsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Craft> GetSeedData() => new[]
        {
            new Craft { Code = "ELEC", Name = "Electrician", Description = "Electrical systems", DefaultHourlyRate = 75.00m, IsActive = true, SortOrder = 1 },
            new Craft { Code = "MECH", Name = "Mechanic", Description = "Mechanical systems", DefaultHourlyRate = 70.00m, IsActive = true, SortOrder = 2 },
            new Craft { Code = "HVAC", Name = "HVAC Technician", Description = "Heating, ventilation, AC", DefaultHourlyRate = 72.00m, IsActive = true, SortOrder = 3 },
            new Craft { Code = "PLMB", Name = "Plumber", Description = "Plumbing systems", DefaultHourlyRate = 68.00m, IsActive = true, SortOrder = 4 },
            new Craft { Code = "WELD", Name = "Welder", Description = "Welding and fabrication", DefaultHourlyRate = 75.00m, IsActive = true, SortOrder = 5 },
            new Craft { Code = "MACH", Name = "Machinist", Description = "Machine shop work", DefaultHourlyRate = 72.00m, IsActive = true, SortOrder = 6 },
            new Craft { Code = "INST", Name = "Instrument Tech", Description = "Instrumentation and controls", DefaultHourlyRate = 78.00m, IsActive = true, SortOrder = 7 },
            new Craft { Code = "PLC", Name = "PLC Programmer", Description = "PLC programming and HMI", DefaultHourlyRate = 85.00m, IsActive = true, SortOrder = 8 },
            new Craft { Code = "LUBR", Name = "Lubrication Tech", Description = "Lubrication specialist", DefaultHourlyRate = 55.00m, IsActive = true, SortOrder = 9 },
            new Craft { Code = "MILL", Name = "Millwright", Description = "Heavy machinery", DefaultHourlyRate = 78.00m, IsActive = true, SortOrder = 10 }
        };

        protected override async Task<Craft?> FindByNaturalKeyAsync(Craft item, CancellationToken ct)
            => await Context.Crafts.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(Craft item) => item.Code;
        protected override bool ShouldUpdate(Craft existing, Craft incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.DefaultHourlyRate != incoming.DefaultHourlyRate || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(Craft existing, Craft incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.DefaultHourlyRate = incoming.DefaultHourlyRate;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region MaintenanceTypeCodes
    public class MaintenanceTypeCodesSeedStep : BaseSeedStep<MaintenanceTypeCode>
    {
        public override string StepName => "MaintenanceTypeCodes";
        public override string DomainName => "MaintenanceTypeCodes";
        public override string NaturalKeyDescription => "Code";

        public MaintenanceTypeCodesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<MaintenanceTypeCode> GetSeedData() => new[]
        {
            new MaintenanceTypeCode { Code = "PM", Name = "Preventive Maintenance", Description = "Scheduled preventive work", IsPreventive = true, IsCorrective = false, IsActive = true, SortOrder = 1 },
            new MaintenanceTypeCode { Code = "CM", Name = "Corrective Maintenance", Description = "Unplanned repair work", IsPreventive = false, IsCorrective = true, IsActive = true, SortOrder = 2 },
            new MaintenanceTypeCode { Code = "PDM", Name = "Predictive Maintenance", Description = "Condition-based maintenance", IsPreventive = true, IsCorrective = false, IsActive = true, SortOrder = 3 },
            new MaintenanceTypeCode { Code = "EM", Name = "Emergency Maintenance", Description = "Emergency breakdown", IsPreventive = false, IsCorrective = true, IsActive = true, SortOrder = 4 },
            new MaintenanceTypeCode { Code = "OH", Name = "Overhaul", Description = "Major overhaul work", IsPreventive = true, IsCorrective = false, IsActive = true, SortOrder = 5 }
        };

        protected override async Task<MaintenanceTypeCode?> FindByNaturalKeyAsync(MaintenanceTypeCode item, CancellationToken ct)
            => await Context.MaintenanceTypeCodes.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(MaintenanceTypeCode item) => item.Code;
        protected override bool ShouldUpdate(MaintenanceTypeCode existing, MaintenanceTypeCode incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.IsPreventive != incoming.IsPreventive || existing.IsCorrective != incoming.IsCorrective
               || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(MaintenanceTypeCode existing, MaintenanceTypeCode incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.IsPreventive = incoming.IsPreventive;
            existing.IsCorrective = incoming.IsCorrective;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region LaborTypes
    public class LaborTypesSeedStep : BaseSeedStep<LaborType>
    {
        public override string StepName => "LaborTypes";
        public override string DomainName => "LaborTypes";
        public override string NaturalKeyDescription => "Code";

        public LaborTypesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<LaborType> GetSeedData() => new[]
        {
            new LaborType { Code = "REG", Name = "Regular", Description = "Standard hourly rate", Category = LaborCategory.Regular, MultiplierRate = 1.0m, IsActive = true, SortOrder = 1 },
            new LaborType { Code = "OT15", Name = "Overtime 1.5x", Description = "Time and a half", Category = LaborCategory.Overtime, MultiplierRate = 1.5m, IsActive = true, SortOrder = 2 },
            new LaborType { Code = "OT20", Name = "Overtime 2.0x", Description = "Double time", Category = LaborCategory.DoubleTime, MultiplierRate = 2.0m, IsActive = true, SortOrder = 3 },
            new LaborType { Code = "HOLI", Name = "Holiday", Description = "Holiday rate", Category = LaborCategory.Holiday, MultiplierRate = 2.0m, IsActive = true, SortOrder = 4 },
            new LaborType { Code = "CALL", Name = "Call-Out", Description = "Emergency call-out", Category = LaborCategory.OnCall, MultiplierRate = 1.5m, IsActive = true, SortOrder = 5 },
            new LaborType { Code = "TRNG", Name = "Training", Description = "Training time", Category = LaborCategory.Training, MultiplierRate = 1.0m, IsActive = true, SortOrder = 6 },
            new LaborType { Code = "TRVL", Name = "Travel", Description = "Travel time", Category = LaborCategory.Travel, MultiplierRate = 1.0m, IsActive = true, SortOrder = 7 }
        };

        protected override async Task<LaborType?> FindByNaturalKeyAsync(LaborType item, CancellationToken ct)
            => await Context.LaborTypes.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(LaborType item) => item.Code;
        protected override bool ShouldUpdate(LaborType existing, LaborType incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.Category != incoming.Category || existing.MultiplierRate != incoming.MultiplierRate
               || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(LaborType existing, LaborType incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.Category = incoming.Category;
            existing.MultiplierRate = incoming.MultiplierRate;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region Skills
    public class SkillsSeedStep : BaseSeedStep<Skill>
    {
        public override string StepName => "Skills";
        public override string DomainName => "Skills";
        public override string NaturalKeyDescription => "Code";

        public SkillsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Skill> GetSeedData() => new[]
        {
            new Skill { Code = "ELEC", Name = "Electrical", Description = "Electrical systems", Level = SkillLevel.Advanced, IsActive = true, SortOrder = 1 },
            new Skill { Code = "MECH", Name = "Mechanical", Description = "Mechanical systems", Level = SkillLevel.Advanced, IsActive = true, SortOrder = 2 },
            new Skill { Code = "HYDR", Name = "Hydraulics", Description = "Hydraulic systems", Level = SkillLevel.Intermediate, IsActive = true, SortOrder = 3 },
            new Skill { Code = "PNEU", Name = "Pneumatics", Description = "Pneumatic systems", Level = SkillLevel.Intermediate, IsActive = true, SortOrder = 4 },
            new Skill { Code = "WELD", Name = "Welding", Description = "Welding and fabrication", Level = SkillLevel.Advanced, IsActive = true, SortOrder = 5 },
            new Skill { Code = "PLCPG", Name = "PLC Programming", Description = "PLC and automation", Level = SkillLevel.Expert, IsActive = true, SortOrder = 6 },
            new Skill { Code = "HVAC", Name = "HVAC", Description = "HVAC systems", Level = SkillLevel.Intermediate, IsActive = true, SortOrder = 7 },
            new Skill { Code = "CALIBR", Name = "Calibration", Description = "Instrument calibration", Level = SkillLevel.Advanced, IsActive = true, SortOrder = 8 }
        };

        protected override async Task<Skill?> FindByNaturalKeyAsync(Skill item, CancellationToken ct)
            => await Context.Skills.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(Skill item) => item.Code;
        protected override bool ShouldUpdate(Skill existing, Skill incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.Level != incoming.Level || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(Skill existing, Skill incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.Level = incoming.Level;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region NumberingSequences
    public class NumberingSequencesSeedStep : BaseSeedStep<NumberingSequence>
    {
        public override string StepName => "NumberingSequences";
        public override string DomainName => "NumberingSequences";
        public override string NaturalKeyDescription => "Code";

        public NumberingSequencesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<NumberingSequence> GetSeedData() => new[]
        {
            new NumberingSequence { Code = "ASSET", Name = "Asset Number", Prefix = "AST-", NextNumber = 1000, NumberLength = 6, IsActive = true },
            new NumberingSequence { Code = "WO", Name = "Work Order Number", Prefix = "WO-", NextNumber = 10000, NumberLength = 6, IsActive = true },
            new NumberingSequence { Code = "PO", Name = "Purchase Order Number", Prefix = "PO-", NextNumber = 5000, NumberLength = 6, IsActive = true },
            new NumberingSequence { Code = "REQ", Name = "Requisition Number", Prefix = "REQ-", NextNumber = 1000, NumberLength = 6, IsActive = true },
            new NumberingSequence { Code = "INV", Name = "Invoice Number", Prefix = "INV-", NextNumber = 1000, NumberLength = 6, IsActive = true },
            new NumberingSequence { Code = "GR", Name = "Goods Receipt Number", Prefix = "GR-", NextNumber = 1000, NumberLength = 6, IsActive = true },
            new NumberingSequence { Code = "TRANS", Name = "Transfer Number", Prefix = "TRF-", NextNumber = 100, NumberLength = 6, IsActive = true },
            new NumberingSequence { Code = "PROJ", Name = "Project Number", Prefix = "CIP-", NextNumber = 100, NumberLength = 5, IsActive = true }
        };

        protected override async Task<NumberingSequence?> FindByNaturalKeyAsync(NumberingSequence item, CancellationToken ct)
            => await Context.NumberingSequences.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(NumberingSequence item) => item.Code;
        protected override bool ShouldUpdate(NumberingSequence existing, NumberingSequence incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Prefix, incoming.Prefix)
               || existing.NumberLength != incoming.NumberLength;
        protected override void UpdateEntity(NumberingSequence existing, NumberingSequence incoming)
        {
            existing.Name = incoming.Name;
            existing.Prefix = incoming.Prefix;
            existing.NumberLength = incoming.NumberLength;
        }
    }
    #endregion

    #region PaymentTerms
    public class PaymentTermsSeedStep : BaseSeedStep<PaymentTerm>
    {
        public override string StepName => "PaymentTerms";
        public override string DomainName => "PaymentTerms";
        public override string NaturalKeyDescription => "Code";

        public PaymentTermsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<PaymentTerm> GetSeedData() => new[]
        {
            new PaymentTerm { Code = "NET30", Name = "Net 30", Description = "Payment due in 30 days", DueDays = 30, DiscountPercent = 0, DiscountDays = 0, IsActive = true, SortOrder = 1 },
            new PaymentTerm { Code = "NET45", Name = "Net 45", Description = "Payment due in 45 days", DueDays = 45, DiscountPercent = 0, DiscountDays = 0, IsActive = true, SortOrder = 2 },
            new PaymentTerm { Code = "NET60", Name = "Net 60", Description = "Payment due in 60 days", DueDays = 60, DiscountPercent = 0, DiscountDays = 0, IsActive = true, SortOrder = 3 },
            new PaymentTerm { Code = "2N10N30", Name = "2% 10 Net 30", Description = "2% discount if paid in 10 days", DueDays = 30, DiscountPercent = 2.0m, DiscountDays = 10, IsActive = true, SortOrder = 4 },
            new PaymentTerm { Code = "COD", Name = "Cash on Delivery", Description = "Payment due on delivery", DueDays = 0, DiscountPercent = 0, DiscountDays = 0, IsActive = true, SortOrder = 5 },
            new PaymentTerm { Code = "NET15", Name = "Net 15", Description = "Payment due in 15 days", DueDays = 15, DiscountPercent = 0, DiscountDays = 0, IsActive = true, SortOrder = 6 }
        };

        protected override async Task<PaymentTerm?> FindByNaturalKeyAsync(PaymentTerm item, CancellationToken ct)
            => await Context.PaymentTerms.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(PaymentTerm item) => item.Code;
        protected override bool ShouldUpdate(PaymentTerm existing, PaymentTerm incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.DueDays != incoming.DueDays || existing.DiscountPercent != incoming.DiscountPercent
               || existing.DiscountDays != incoming.DiscountDays || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(PaymentTerm existing, PaymentTerm incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.DueDays = incoming.DueDays;
            existing.DiscountPercent = incoming.DiscountPercent;
            existing.DiscountDays = incoming.DiscountDays;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region Currencies
    public class CurrenciesSeedStep : BaseSeedStep<Currency>
    {
        public override string StepName => "Currencies";
        public override string DomainName => "Currencies";
        public override string NaturalKeyDescription => "Code";

        public CurrenciesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Currency> GetSeedData() => new[]
        {
            new Currency { Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsBaseCurrency = true, IsActive = true, SortOrder = 1 },
            new Currency { Code = "CAD", Name = "Canadian Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, SortOrder = 2 },
            new Currency { Code = "EUR", Name = "Euro", Symbol = "€", DecimalPlaces = 2, IsActive = true, SortOrder = 3 },
            new Currency { Code = "GBP", Name = "British Pound", Symbol = "£", DecimalPlaces = 2, IsActive = true, SortOrder = 4 },
            new Currency { Code = "MXN", Name = "Mexican Peso", Symbol = "$", DecimalPlaces = 2, IsActive = true, SortOrder = 5 },
            new Currency { Code = "JPY", Name = "Japanese Yen", Symbol = "¥", DecimalPlaces = 0, IsActive = true, SortOrder = 6 }
        };

        protected override async Task<Currency?> FindByNaturalKeyAsync(Currency item, CancellationToken ct)
            => await Context.Currencies.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(Currency item) => item.Code;
        protected override bool ShouldUpdate(Currency existing, Currency incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Symbol, incoming.Symbol)
               || existing.DecimalPlaces != incoming.DecimalPlaces || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(Currency existing, Currency incoming)
        {
            existing.Name = incoming.Name;
            existing.Symbol = incoming.Symbol;
            existing.DecimalPlaces = incoming.DecimalPlaces;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region UOMDefinitions
    public class UOMDefinitionsSeedStep : BaseSeedStep<UOMDefinition>
    {
        public override string StepName => "UOMDefinitions";
        public override string DomainName => "UOMDefinitions";
        public override string NaturalKeyDescription => "Code";

        public UOMDefinitionsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<UOMDefinition> GetSeedData() => new[]
        {
            new UOMDefinition { Code = "EA", Name = "Each", Description = "Individual unit", Type = UOMType.Count, IsActive = true, SortOrder = 1 },
            new UOMDefinition { Code = "PCS", Name = "Pieces", Description = "Individual pieces", Type = UOMType.Count, IsActive = true, SortOrder = 2 },
            new UOMDefinition { Code = "BOX", Name = "Box", Description = "Box of items", Type = UOMType.Count, IsActive = true, SortOrder = 3 },
            new UOMDefinition { Code = "FT", Name = "Feet", Description = "Linear feet", Type = UOMType.Length, IsActive = true, SortOrder = 4 },
            new UOMDefinition { Code = "M", Name = "Meter", Description = "Linear meter", Type = UOMType.Length, IsActive = true, SortOrder = 5 },
            new UOMDefinition { Code = "LB", Name = "Pound", Description = "Weight in pounds", Type = UOMType.Weight, IsActive = true, SortOrder = 6 },
            new UOMDefinition { Code = "KG", Name = "Kilogram", Description = "Weight in kilograms", Type = UOMType.Weight, IsActive = true, SortOrder = 7 },
            new UOMDefinition { Code = "GAL", Name = "Gallon", Description = "Volume in gallons", Type = UOMType.Volume, IsActive = true, SortOrder = 8 },
            new UOMDefinition { Code = "L", Name = "Liter", Description = "Volume in liters", Type = UOMType.Volume, IsActive = true, SortOrder = 9 },
            new UOMDefinition { Code = "HR", Name = "Hour", Description = "Time in hours", Type = UOMType.Time, IsActive = true, SortOrder = 10 }
        };

        protected override async Task<UOMDefinition?> FindByNaturalKeyAsync(UOMDefinition item, CancellationToken ct)
            => await Context.UOMDefinitions.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(UOMDefinition item) => item.Code;
        protected override bool ShouldUpdate(UOMDefinition existing, UOMDefinition incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.Type != incoming.Type || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(UOMDefinition existing, UOMDefinition incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.Type = incoming.Type;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region ShippingMethods
    public class ShippingMethodsSeedStep : BaseSeedStep<ShippingMethod>
    {
        public override string StepName => "ShippingMethods";
        public override string DomainName => "ShippingMethods";
        public override string NaturalKeyDescription => "Code";

        public ShippingMethodsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<ShippingMethod> GetSeedData() => new[]
        {
            new ShippingMethod { Code = "GND", Name = "Ground", Description = "Standard ground shipping", Carrier = "Various", EstimatedDays = 5, IsActive = true },
            new ShippingMethod { Code = "2DAY", Name = "2-Day", Description = "2-day express shipping", Carrier = "Various", EstimatedDays = 2, IsActive = true },
            new ShippingMethod { Code = "OVNT", Name = "Overnight", Description = "Next day delivery", Carrier = "Various", EstimatedDays = 1, IsActive = true },
            new ShippingMethod { Code = "FRT", Name = "Freight/LTL", Description = "Less than truckload freight", Carrier = "Various", EstimatedDays = 7, IsActive = true },
            new ShippingMethod { Code = "PICK", Name = "Will Call/Pickup", Description = "Customer pickup", Carrier = "N/A", EstimatedDays = 0, IsActive = true }
        };

        protected override async Task<ShippingMethod?> FindByNaturalKeyAsync(ShippingMethod item, CancellationToken ct)
            => await Context.ShippingMethods.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(ShippingMethod item) => item.Code;
        protected override bool ShouldUpdate(ShippingMethod existing, ShippingMethod incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || !StringEquals(existing.Carrier, incoming.Carrier) || existing.EstimatedDays != incoming.EstimatedDays;
        protected override void UpdateEntity(ShippingMethod existing, ShippingMethod incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.Carrier = incoming.Carrier;
            existing.EstimatedDays = incoming.EstimatedDays;
        }
    }
    #endregion

    #region TaxCodes
    public class TaxCodesSeedStep : BaseSeedStep<TaxCode>
    {
        public override string StepName => "TaxCodes";
        public override string DomainName => "TaxCodes";
        public override string NaturalKeyDescription => "Code";

        public TaxCodesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<TaxCode> GetSeedData() => new[]
        {
            new TaxCode { Code = "NOTAX", Name = "No Tax", Description = "Tax exempt", Rate = 0, IsActive = true, SortOrder = 1 },
            new TaxCode { Code = "STD", Name = "Standard Rate", Description = "Standard sales tax", Rate = 0.0825m, IsActive = true, SortOrder = 2 },
            new TaxCode { Code = "GST", Name = "GST (Canada)", Description = "Goods and Services Tax", Rate = 0.05m, IsActive = true, SortOrder = 3 },
            new TaxCode { Code = "HST13", Name = "HST 13% (Ontario)", Description = "Harmonized Sales Tax", Rate = 0.13m, IsActive = true, SortOrder = 4 },
            new TaxCode { Code = "EXEMPT", Name = "Exempt", Description = "Tax exempt transaction", Rate = 0, IsActive = true, SortOrder = 5 }
        };

        protected override async Task<TaxCode?> FindByNaturalKeyAsync(TaxCode item, CancellationToken ct)
            => await Context.TaxCodes.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(TaxCode item) => item.Code;
        protected override bool ShouldUpdate(TaxCode existing, TaxCode incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.Rate != incoming.Rate || existing.SortOrder != incoming.SortOrder;
        protected override void UpdateEntity(TaxCode existing, TaxCode incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.Rate = incoming.Rate;
            existing.SortOrder = incoming.SortOrder;
        }
    }
    #endregion

    #region Section179Limits
    public class Section179LimitsSeedStep : BaseSeedStep<Section179Limits>
    {
        public override string StepName => "Section179Limits";
        public override string DomainName => "Section179Limits";
        public override string NaturalKeyDescription => "TaxYear";

        public Section179LimitsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Section179Limits> GetSeedData() => new[]
        {
            new Section179Limits { TaxYear = 2024, MaxDeduction = 1220000m, PhaseoutThreshold = 3050000m },
            new Section179Limits { TaxYear = 2025, MaxDeduction = 1250000m, PhaseoutThreshold = 3130000m },
            new Section179Limits { TaxYear = 2026, MaxDeduction = 1280000m, PhaseoutThreshold = 3200000m }
        };

        protected override async Task<Section179Limits?> FindByNaturalKeyAsync(Section179Limits item, CancellationToken ct)
            => await Context.Section179Limits.FirstOrDefaultAsync(x => x.TaxYear == item.TaxYear, ct);
        protected override string GetNaturalKeyValue(Section179Limits item) => item.TaxYear.ToString();
        protected override bool ShouldUpdate(Section179Limits existing, Section179Limits incoming)
            => existing.MaxDeduction != incoming.MaxDeduction || existing.PhaseoutThreshold != incoming.PhaseoutThreshold;
        protected override void UpdateEntity(Section179Limits existing, Section179Limits incoming)
        {
            existing.MaxDeduction = incoming.MaxDeduction;
            existing.PhaseoutThreshold = incoming.PhaseoutThreshold;
        }
    }
    #endregion

    #region BonusDepreciationRates
    public class BonusDepreciationRatesSeedStep : BaseSeedStep<BonusDepreciationRates>
    {
        public override string StepName => "BonusDepreciationRates";
        public override string DomainName => "BonusDepreciationRates";
        public override string NaturalKeyDescription => "TaxYear";

        public BonusDepreciationRatesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<BonusDepreciationRates> GetSeedData() => new[]
        {
            new BonusDepreciationRates { TaxYear = 2024, Rate = 0.60m, Notes = "60% bonus depreciation" },
            new BonusDepreciationRates { TaxYear = 2025, Rate = 0.40m, Notes = "40% bonus depreciation" },
            new BonusDepreciationRates { TaxYear = 2026, Rate = 0.20m, Notes = "20% bonus depreciation" }
        };

        protected override async Task<BonusDepreciationRates?> FindByNaturalKeyAsync(BonusDepreciationRates item, CancellationToken ct)
            => await Context.BonusDepreciationRates.FirstOrDefaultAsync(x => x.TaxYear == item.TaxYear, ct);
        protected override string GetNaturalKeyValue(BonusDepreciationRates item) => item.TaxYear.ToString();
        protected override bool ShouldUpdate(BonusDepreciationRates existing, BonusDepreciationRates incoming)
            => existing.Rate != incoming.Rate || !StringEquals(existing.Notes, incoming.Notes);
        protected override void UpdateEntity(BonusDepreciationRates existing, BonusDepreciationRates incoming)
        {
            existing.Rate = incoming.Rate;
            existing.Notes = incoming.Notes;
        }
    }
    #endregion

    #region CcaClasses
    public class CcaClassesSeedStep : BaseSeedStep<CcaClass>
    {
        public override string StepName => "CcaClasses";
        public override string DomainName => "CcaClasses";
        public override string NaturalKeyDescription => "ClassNumber";

        public CcaClassesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<CcaClass> GetSeedData() => new[]
        {
            new CcaClass { ClassNumber = 1, Description = "Buildings acquired after 1987", Rate = 4m, IsDecliningBalance = true },
            new CcaClass { ClassNumber = 8, Description = "Furniture, machinery, equipment", Rate = 20m, IsDecliningBalance = true },
            new CcaClass { ClassNumber = 10, Description = "Automotive equipment", Rate = 30m, IsDecliningBalance = true },
            new CcaClass { ClassNumber = 12, Description = "Tools under $500", Rate = 100m, IsDecliningBalance = true },
            new CcaClass { ClassNumber = 45, Description = "Computers acquired after Mar 2007", Rate = 45m, IsDecliningBalance = true },
            new CcaClass { ClassNumber = 50, Description = "Computer equipment after Jan 2024", Rate = 55m, IsDecliningBalance = true }
        };

        protected override async Task<CcaClass?> FindByNaturalKeyAsync(CcaClass item, CancellationToken ct)
            => await Context.CcaClasses.FirstOrDefaultAsync(x => x.ClassNumber == item.ClassNumber, ct);
        protected override string GetNaturalKeyValue(CcaClass item) => item.ClassNumber.ToString();
        protected override bool ShouldUpdate(CcaClass existing, CcaClass incoming)
            => !StringEquals(existing.Description, incoming.Description) || existing.Rate != incoming.Rate
               || existing.IsDecliningBalance != incoming.IsDecliningBalance;
        protected override void UpdateEntity(CcaClass existing, CcaClass incoming)
        {
            existing.Description = incoming.Description;
            existing.Rate = incoming.Rate;
            existing.IsDecliningBalance = incoming.IsDecliningBalance;
        }
    }
    #endregion
}
