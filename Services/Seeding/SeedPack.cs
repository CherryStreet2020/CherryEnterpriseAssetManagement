namespace Abs.FixedAssets.Services.Seeding
{
    public enum SeedPackSize
    {
        Small,
        MidSize,
        Enterprise
    }

    public class SeedPack
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SeedPackSize Size { get; set; }
        public int CompanyCount { get; set; }
        public int SiteCount { get; set; }
        public int LocationCount { get; set; }
        public int AssetCount { get; set; }
        public int VendorCount { get; set; }
        public int TechnicianCount { get; set; }
        public int MaintenanceEventCount { get; set; }
        public int PMTemplateCount { get; set; }
        public int MaintenanceScheduleCount { get; set; }
        public bool IncludeWorkOrders { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string ColorClass { get; set; } = string.Empty;
    }

    public static class SeedPackDefinitions
    {
        public static readonly SeedPack SmallManufacturer = new()
        {
            Id = "small",
            Name = "Small Manufacturer",
            Description = "Single site operation with basic asset tracking. Ideal for testing core functionality.",
            Size = SeedPackSize.Small,
            CompanyCount = 1,
            SiteCount = 1,
            LocationCount = 5,
            AssetCount = 25,
            VendorCount = 5,
            TechnicianCount = 2,
            MaintenanceEventCount = 15,
            PMTemplateCount = 3,
            MaintenanceScheduleCount = 5,
            IncludeWorkOrders = false,
            Icon = "building",
            ColorClass = "bg-blue-100 text-blue-700"
        };

        public static readonly SeedPack MidSizeManufacturer = new()
        {
            Id = "midsize",
            Name = "Mid-Size Manufacturer",
            Description = "Multi-site operation with full maintenance tracking. Good for comprehensive testing.",
            Size = SeedPackSize.MidSize,
            CompanyCount = 2,
            SiteCount = 3,
            LocationCount = 12,
            AssetCount = 100,
            VendorCount = 8,
            TechnicianCount = 5,
            MaintenanceEventCount = 75,
            PMTemplateCount = 8,
            MaintenanceScheduleCount = 15,
            IncludeWorkOrders = true,
            Icon = "building-2",
            ColorClass = "bg-green-100 text-green-700"
        };

        public static readonly SeedPack EnterpriseManufacturer = new()
        {
            Id = "enterprise",
            Name = "Enterprise",
            Description = "Multi-company holding structure with full demo data. Matches production-scale deployment.",
            Size = SeedPackSize.Enterprise,
            CompanyCount = 3,
            SiteCount = 5,
            LocationCount = 21,
            AssetCount = 321,
            VendorCount = 10,
            TechnicianCount = 5,
            MaintenanceEventCount = 239,
            PMTemplateCount = 15,
            MaintenanceScheduleCount = 25,
            IncludeWorkOrders = true,
            Icon = "buildings",
            ColorClass = "bg-purple-100 text-purple-700"
        };

        public static IReadOnlyList<SeedPack> All => new[] { SmallManufacturer, MidSizeManufacturer, EnterpriseManufacturer };

        public static SeedPack? GetById(string id) => All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }
}
