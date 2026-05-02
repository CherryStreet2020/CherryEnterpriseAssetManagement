using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Seeding.Pipelines
{
    public class VendorsAndPartsFoundationSeedPipeline : ISeedPipeline
    {
        public string Name => "VendorsAndPartsFoundationSeed";
        public string Version => "1.0.0";
        public string Description => "Vendor masters and item categories foundation for purchasing and inventory";
        public bool IsDevOnly => false;

        private readonly List<ISeedStep> _steps;
        public IReadOnlyList<ISeedStep> Steps => _steps;

        public VendorsAndPartsFoundationSeedPipeline(AppDbContext context, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<VendorsAndPartsFoundationSeedPipeline>();
            _steps = new List<ISeedStep>
            {
                new ItemCategoriesSeedStep(context, logger),
                new ManufacturersSeedStep(context, logger),
                new VendorsSeedStep(context, logger),
                new LaborRatesSeedStep(context, logger)
            };
        }
    }

    #region ItemCategories
    public class ItemCategoriesSeedStep : BaseSeedStep<ItemCategory>
    {
        public override string StepName => "ItemCategories";
        public override string DomainName => "ItemCategories";
        public override string NaturalKeyDescription => "Code";

        public ItemCategoriesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<ItemCategory> GetSeedData() => new[]
        {
            new ItemCategory { Code = "BEAR", Name = "Bearings", Description = "Ball bearings, roller bearings, bushings", IsActive = true },
            new ItemCategory { Code = "BELT", Name = "Belts", Description = "V-belts, timing belts, conveyor belts", IsActive = true },
            new ItemCategory { Code = "ELEC", Name = "Electrical", Description = "Motors, starters, relays, switches", IsActive = true },
            new ItemCategory { Code = "FLTR", Name = "Filters", Description = "Air, oil, hydraulic filters", IsActive = true },
            new ItemCategory { Code = "FAST", Name = "Fasteners", Description = "Bolts, nuts, screws, washers", IsActive = true },
            new ItemCategory { Code = "HYDR", Name = "Hydraulics", Description = "Cylinders, valves, hoses, fittings", IsActive = true },
            new ItemCategory { Code = "PNEU", Name = "Pneumatics", Description = "Air cylinders, valves, fittings", IsActive = true },
            new ItemCategory { Code = "SEAL", Name = "Seals & Gaskets", Description = "O-rings, seals, gaskets", IsActive = true },
            new ItemCategory { Code = "LUBR", Name = "Lubricants", Description = "Oils, greases, lubricants", IsActive = true },
            new ItemCategory { Code = "TOOL", Name = "Tools", Description = "Hand tools, power tools", IsActive = true },
            new ItemCategory { Code = "SAFE", Name = "Safety", Description = "PPE, safety equipment", IsActive = true },
            new ItemCategory { Code = "CHEM", Name = "Chemicals", Description = "Cleaning chemicals, solvents", IsActive = true },
            new ItemCategory { Code = "PIPE", Name = "Piping", Description = "Pipes, fittings, valves", IsActive = true },
            new ItemCategory { Code = "SENS", Name = "Sensors", Description = "Proximity, temperature, pressure sensors", IsActive = true },
            new ItemCategory { Code = "CTRL", Name = "Controls", Description = "PLCs, HMIs, controllers", IsActive = true },
            new ItemCategory { Code = "MISC", Name = "Miscellaneous", Description = "Other MRO items", IsActive = true }
        };

        protected override async Task<ItemCategory?> FindByNaturalKeyAsync(ItemCategory item, CancellationToken ct)
            => await Context.ItemCategories.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(ItemCategory item) => item.Code;
        protected override bool ShouldUpdate(ItemCategory existing, ItemCategory incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description);
        protected override void UpdateEntity(ItemCategory existing, ItemCategory incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
        }
    }
    #endregion

    #region Manufacturers
    public class ManufacturersSeedStep : BaseSeedStep<Manufacturer>
    {
        public override string StepName => "Manufacturers";
        public override string DomainName => "Manufacturers";
        public override string NaturalKeyDescription => "Name";

        public ManufacturersSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Manufacturer> GetSeedData() => new[]
        {
            new Manufacturer { Name = "SKF", Website = "https://www.skf.com", ContactPhone = "800-555-0001", Active = true },
            new Manufacturer { Name = "Timken", Website = "https://www.timken.com", ContactPhone = "800-555-0002", Active = true },
            new Manufacturer { Name = "Gates", Website = "https://www.gates.com", ContactPhone = "800-555-0003", Active = true },
            new Manufacturer { Name = "Parker Hannifin", Website = "https://www.parker.com", ContactPhone = "800-555-0004", Active = true },
            new Manufacturer { Name = "Siemens", Website = "https://www.siemens.com", ContactPhone = "800-555-0005", Active = true },
            new Manufacturer { Name = "Allen-Bradley", Website = "https://www.rockwellautomation.com", ContactPhone = "800-555-0006", Active = true },
            new Manufacturer { Name = "Festo", Website = "https://www.festo.com", ContactPhone = "800-555-0007", Active = true },
            new Manufacturer { Name = "SMC", Website = "https://www.smcusa.com", ContactPhone = "800-555-0008", Active = true },
            new Manufacturer { Name = "Baldor", Website = "https://www.baldor.com", ContactPhone = "800-555-0009", Active = true },
            new Manufacturer { Name = "Rexroth", Website = "https://www.boschrexroth.com", ContactPhone = "800-555-0010", Active = true }
        };

        protected override async Task<Manufacturer?> FindByNaturalKeyAsync(Manufacturer item, CancellationToken ct)
            => await Context.Manufacturers.FirstOrDefaultAsync(x => x.Name.ToLower() == item.Name.ToLower(), ct);
        protected override string GetNaturalKeyValue(Manufacturer item) => item.Name;
        protected override bool ShouldUpdate(Manufacturer existing, Manufacturer incoming)
            => !StringEquals(existing.Website, incoming.Website) || !StringEquals(existing.ContactPhone, incoming.ContactPhone);
        protected override void UpdateEntity(Manufacturer existing, Manufacturer incoming)
        {
            existing.Website = incoming.Website;
            existing.ContactPhone = incoming.ContactPhone;
        }
    }
    #endregion

    #region Vendors
    public class VendorsSeedStep : BaseSeedStep<Vendor>
    {
        public override string StepName => "Vendors";
        public override string DomainName => "Vendors";
        public override string NaturalKeyDescription => "Code";

        public VendorsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Vendor> GetSeedData() => new[]
        {
            new Vendor { Code = "GRNGR", Name = "Grainger", VendorType = VendorType.Distributor, Address = "100 Grainger Pkwy", City = "Lake Forest", Phone = "800-472-4643", Email = "orders@grainger.com", Website = "https://www.grainger.com", PaymentTerms = PaymentTerms.Net30 },
            new Vendor { Code = "MCMSTR", Name = "McMaster-Carr", VendorType = VendorType.Distributor, Address = "600 County Line Rd", City = "Elmhurst", Phone = "630-833-0300", Email = "orders@mcmaster.com", Website = "https://www.mcmaster.com", PaymentTerms = PaymentTerms.Net30 },
            new Vendor { Code = "MOTION", Name = "Motion Industries", VendorType = VendorType.Distributor, Address = "1605 Alton Rd", City = "Birmingham", Phone = "800-526-9328", Email = "sales@motion.com", Website = "https://www.motionindustries.com", PaymentTerms = PaymentTerms.Net30 },
            new Vendor { Code = "FASTEN", Name = "Fastenal", VendorType = VendorType.Distributor, Address = "2001 Theurer Blvd", City = "Winona", Phone = "507-454-5374", Email = "orders@fastenal.com", Website = "https://www.fastenal.com", PaymentTerms = PaymentTerms.Net30 },
            new Vendor { Code = "APPLIED", Name = "Applied Industrial", VendorType = VendorType.Distributor, Address = "1 Applied Plaza", City = "Cleveland", Phone = "216-426-4000", Email = "sales@applied.com", Website = "https://www.applied.com", PaymentTerms = PaymentTerms.Net30 },
            new Vendor { Code = "PARKER", Name = "Parker Hannifin Store", VendorType = VendorType.Manufacturer, Address = "6035 Parkland Blvd", City = "Cleveland", Phone = "800-272-7537", Email = "sales@parker.com", Website = "https://www.parker.com", PaymentTerms = PaymentTerms.Net45 },
            new Vendor { Code = "ROCKW", Name = "Rockwell Automation", VendorType = VendorType.Manufacturer, Address = "1201 S 2nd St", City = "Milwaukee", Phone = "414-382-2000", Email = "sales@rockwellautomation.com", Website = "https://www.rockwellautomation.com", PaymentTerms = PaymentTerms.Net45 },
            new Vendor { Code = "SIEMNS", Name = "Siemens Industry", VendorType = VendorType.Manufacturer, Address = "100 Technology Dr", City = "Alpharetta", Phone = "800-365-8766", Email = "sales@siemens.com", Website = "https://www.siemens.com", PaymentTerms = PaymentTerms.Net45 },
            new Vendor { Code = "CALSERV", Name = "Calibration Services Inc", VendorType = VendorType.ServiceProvider, Address = "500 Precision Way", City = "Atlanta", Phone = "404-555-1234", Email = "service@calserv.com", Website = "https://www.calserv.com", PaymentTerms = PaymentTerms.Net30 },
            new Vendor { Code = "ELECTRIC", Name = "Electric Motor Services", VendorType = VendorType.ServiceProvider, Address = "200 Motor Dr", City = "Detroit", Phone = "313-555-2345", Email = "service@electricmotor.com", Website = "https://www.electricmotorservices.com", PaymentTerms = PaymentTerms.Net30 }
        };

        protected override async Task<Vendor?> FindByNaturalKeyAsync(Vendor item, CancellationToken ct)
            => await Context.Vendors.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(Vendor item) => item.Code;
        protected override bool ShouldUpdate(Vendor existing, Vendor incoming)
            => !StringEquals(existing.Name, incoming.Name) || existing.VendorType != incoming.VendorType
               || !StringEquals(existing.Address, incoming.Address) || !StringEquals(existing.City, incoming.City)
               || !StringEquals(existing.Phone, incoming.Phone) || !StringEquals(existing.Email, incoming.Email)
               || !StringEquals(existing.Website, incoming.Website) || existing.PaymentTerms != incoming.PaymentTerms;
        protected override void UpdateEntity(Vendor existing, Vendor incoming)
        {
            existing.Name = incoming.Name;
            existing.VendorType = incoming.VendorType;
            existing.Address = incoming.Address;
            existing.City = incoming.City;
            existing.Phone = incoming.Phone;
            existing.Email = incoming.Email;
            existing.Website = incoming.Website;
            existing.PaymentTerms = incoming.PaymentTerms;
        }
    }
    #endregion

    #region LaborRates
    public class LaborRatesSeedStep : BaseSeedStep<LaborRate>
    {
        public override string StepName => "LaborRates";
        public override string DomainName => "LaborRates";
        public override string NaturalKeyDescription => "Code";

        public LaborRatesSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<LaborRate> GetSeedData() => new[]
        {
            new LaborRate { Code = "ELEC1", Name = "Electrician Level 1", Description = "Journeyman electrician", StandardRate = 65.00m, OvertimeRate = 97.50m, IsActive = true },
            new LaborRate { Code = "ELEC2", Name = "Electrician Level 2", Description = "Master electrician", StandardRate = 85.00m, OvertimeRate = 127.50m, IsActive = true },
            new LaborRate { Code = "MECH1", Name = "Mechanic Level 1", Description = "Journeyman mechanic", StandardRate = 60.00m, OvertimeRate = 90.00m, IsActive = true },
            new LaborRate { Code = "MECH2", Name = "Mechanic Level 2", Description = "Master mechanic", StandardRate = 80.00m, OvertimeRate = 120.00m, IsActive = true },
            new LaborRate { Code = "MILL", Name = "Millwright", Description = "Industrial millwright", StandardRate = 75.00m, OvertimeRate = 112.50m, IsActive = true },
            new LaborRate { Code = "WELD", Name = "Welder", Description = "Certified welder", StandardRate = 70.00m, OvertimeRate = 105.00m, IsActive = true },
            new LaborRate { Code = "PIPE", Name = "Pipefitter", Description = "Pipefitter/plumber", StandardRate = 68.00m, OvertimeRate = 102.00m, IsActive = true },
            new LaborRate { Code = "HVAC", Name = "HVAC Technician", Description = "HVAC specialist", StandardRate = 72.00m, OvertimeRate = 108.00m, IsActive = true },
            new LaborRate { Code = "INST", Name = "Instrumentation", Description = "Instrumentation tech", StandardRate = 78.00m, OvertimeRate = 117.00m, IsActive = true },
            new LaborRate { Code = "PLC", Name = "PLC/Controls", Description = "Controls engineer", StandardRate = 90.00m, OvertimeRate = 135.00m, IsActive = true }
        };

        protected override async Task<LaborRate?> FindByNaturalKeyAsync(LaborRate item, CancellationToken ct)
            => await Context.LaborRates.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(LaborRate item) => item.Code;
        protected override bool ShouldUpdate(LaborRate existing, LaborRate incoming)
            => !StringEquals(existing.Name, incoming.Name) || !StringEquals(existing.Description, incoming.Description)
               || existing.StandardRate != incoming.StandardRate || existing.OvertimeRate != incoming.OvertimeRate;
        protected override void UpdateEntity(LaborRate existing, LaborRate incoming)
        {
            existing.Name = incoming.Name;
            existing.Description = incoming.Description;
            existing.StandardRate = incoming.StandardRate;
            existing.OvertimeRate = incoming.OvertimeRate;
        }
    }
    #endregion
}
