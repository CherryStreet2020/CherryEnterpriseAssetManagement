using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Seeding.Pipelines
{
    public class DemoScenarioSeedPipeline : ISeedPipeline
    {
        public string Name => "DemoScenarioSeed";
        public string Version => "1.0.0";
        public string Description => "Demo scenario data: Sample assets, work orders, and transactions for development/testing only";
        public bool IsDevOnly => true;

        private readonly List<ISeedStep> _steps;
        public IReadOnlyList<ISeedStep> Steps => _steps;

        public DemoScenarioSeedPipeline(AppDbContext context, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<DemoScenarioSeedPipeline>();
            _steps = new List<ISeedStep>
            {
                new DemoAssetsSeedStep(context, logger),
                new DemoItemsSeedStep(context, logger)
            };
        }
    }

    #region DemoAssets
    public class DemoAssetsSeedStep : BaseSeedStep<Asset>
    {
        public override string StepName => "DemoAssets";
        public override string DomainName => "Assets";
        public override string NaturalKeyDescription => "AssetNumber";

        public DemoAssetsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Asset> GetSeedData() => new[]
        {
            new Asset { AssetNumber = "AST-000001", Description = "CNC Milling Machine", Model = "VMC-850", SerialNumber = "CNC2024001", InServiceDate = new DateTime(2022, 3, 15), AcquisitionCost = 175000m, SalvageValue = 8750m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true },
            new Asset { AssetNumber = "AST-000002", Description = "Hydraulic Press 500T", Model = "HP-500", SerialNumber = "HP2023045", InServiceDate = new DateTime(2021, 6, 1), AcquisitionCost = 125000m, SalvageValue = 6250m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true },
            new Asset { AssetNumber = "AST-000003", Description = "Industrial Robot Arm", Model = "RA-6X", SerialNumber = "ROB2024012", InServiceDate = new DateTime(2023, 1, 10), AcquisitionCost = 85000m, SalvageValue = 4250m, UsefulLifeMonths = 84, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false },
            new Asset { AssetNumber = "AST-000004", Description = "Air Compressor 100HP", Model = "AC-100", SerialNumber = "COMP2022087", InServiceDate = new DateTime(2020, 9, 20), AcquisitionCost = 45000m, SalvageValue = 2250m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true },
            new Asset { AssetNumber = "AST-000005", Description = "Overhead Crane 10T", Model = "OHC-10", SerialNumber = "CR2019032", InServiceDate = new DateTime(2019, 4, 5), AcquisitionCost = 95000m, SalvageValue = 4750m, UsefulLifeMonths = 180, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true },
            new Asset { AssetNumber = "AST-000006", Description = "Conveyor System Line A", Model = "CONV-A1", SerialNumber = "CONV2021055", InServiceDate = new DateTime(2021, 2, 28), AcquisitionCost = 65000m, SalvageValue = 3250m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false },
            new Asset { AssetNumber = "AST-000007", Description = "Welding Robot Cell", Model = "WR-200", SerialNumber = "WELD2022101", InServiceDate = new DateTime(2022, 7, 15), AcquisitionCost = 120000m, SalvageValue = 6000m, UsefulLifeMonths = 84, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true },
            new Asset { AssetNumber = "AST-000008", Description = "Forklift Electric 5000lb", Model = "FL-5000E", SerialNumber = "FORK2023022", InServiceDate = new DateTime(2023, 3, 1), AcquisitionCost = 35000m, SalvageValue = 3500m, UsefulLifeMonths = 60, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false },
            new Asset { AssetNumber = "AST-000009", Description = "CNC Lathe", Model = "LAT-450", SerialNumber = "LAT2024005", InServiceDate = new DateTime(2024, 1, 15), AcquisitionCost = 145000m, SalvageValue = 7250m, UsefulLifeMonths = 120, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = true },
            new Asset { AssetNumber = "AST-000010", Description = "HVAC Unit Building A", Model = "HVAC-500", SerialNumber = "HVAC2018099", InServiceDate = new DateTime(2018, 8, 10), AcquisitionCost = 75000m, SalvageValue = 3750m, UsefulLifeMonths = 180, DepreciationMethod = DepreciationMethod.StraightLine, Status = AssetStatus.Active, Active = true, IsCritical = false }
        };

        protected override async Task<Asset?> FindByNaturalKeyAsync(Asset item, CancellationToken ct)
            => await Context.Assets.FirstOrDefaultAsync(x => x.AssetNumber == item.AssetNumber, ct);
        protected override string GetNaturalKeyValue(Asset item) => item.AssetNumber;
        protected override bool ShouldUpdate(Asset existing, Asset incoming)
            => !StringEquals(existing.Description, incoming.Description) || !StringEquals(existing.Model, incoming.Model)
               || !StringEquals(existing.SerialNumber, incoming.SerialNumber);
        protected override void UpdateEntity(Asset existing, Asset incoming)
        {
            existing.Description = incoming.Description;
            existing.Model = incoming.Model;
            existing.SerialNumber = incoming.SerialNumber;
        }
    }
    #endregion

    #region DemoItems
    public class DemoItemsSeedStep : BaseSeedStep<Item>
    {
        public override string StepName => "DemoItems";
        public override string DomainName => "Items";
        public override string NaturalKeyDescription => "PartNumber";

        public DemoItemsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Item> GetSeedData() => new[]
        {
            new Item { PartNumber = "BRG-6205-2RS", Description = "Ball Bearing 6205 2RS", PurchaseUOM = "EA", StandardCost = 12.50m, ListPrice = 18.75m, ReorderPoint = 10, ReorderQuantity = 50, Status = ItemStatus.Active, Type = ItemType.Bearing },
            new Item { PartNumber = "BRG-6206-2RS", Description = "Ball Bearing 6206 2RS", PurchaseUOM = "EA", StandardCost = 15.00m, ListPrice = 22.50m, ReorderPoint = 10, ReorderQuantity = 50, Status = ItemStatus.Active, Type = ItemType.Bearing },
            new Item { PartNumber = "BLT-A68", Description = "V-Belt A68", PurchaseUOM = "EA", StandardCost = 8.50m, ListPrice = 12.75m, ReorderPoint = 5, ReorderQuantity = 20, Status = ItemStatus.Active, Type = ItemType.Belt },
            new Item { PartNumber = "BLT-B78", Description = "V-Belt B78", PurchaseUOM = "EA", StandardCost = 11.00m, ListPrice = 16.50m, ReorderPoint = 5, ReorderQuantity = 20, Status = ItemStatus.Active, Type = ItemType.Belt },
            new Item { PartNumber = "FLT-HYD-01", Description = "Hydraulic Filter 10 Micron", PurchaseUOM = "EA", StandardCost = 25.00m, ListPrice = 37.50m, ReorderPoint = 5, ReorderQuantity = 10, Status = ItemStatus.Active, Type = ItemType.Filter },
            new Item { PartNumber = "FLT-AIR-01", Description = "Air Filter Element", PurchaseUOM = "EA", StandardCost = 18.00m, ListPrice = 27.00m, ReorderPoint = 10, ReorderQuantity = 20, Status = ItemStatus.Active, Type = ItemType.Filter },
            new Item { PartNumber = "OIL-HYD-46", Description = "Hydraulic Oil ISO 46", PurchaseUOM = "GAL", StandardCost = 15.00m, ListPrice = 22.50m, ReorderPoint = 20, ReorderQuantity = 55, Status = ItemStatus.Active, Type = ItemType.Lubricant },
            new Item { PartNumber = "GRS-EP2", Description = "Grease EP2 Lithium", PurchaseUOM = "LB", StandardCost = 5.50m, ListPrice = 8.25m, ReorderPoint = 10, ReorderQuantity = 35, Status = ItemStatus.Active, Type = ItemType.Lubricant },
            new Item { PartNumber = "SEAL-OR-25", Description = "O-Ring 25mm ID", PurchaseUOM = "EA", StandardCost = 1.25m, ListPrice = 1.88m, ReorderPoint = 50, ReorderQuantity = 100, Status = ItemStatus.Active, Type = ItemType.Seal },
            new Item { PartNumber = "SEAL-OR-32", Description = "O-Ring 32mm ID", PurchaseUOM = "EA", StandardCost = 1.50m, ListPrice = 2.25m, ReorderPoint = 50, ReorderQuantity = 100, Status = ItemStatus.Active, Type = ItemType.Seal },
            new Item { PartNumber = "MTR-5HP-1800", Description = "Motor 5HP 1800RPM", PurchaseUOM = "EA", StandardCost = 450.00m, ListPrice = 675.00m, ReorderPoint = 1, ReorderQuantity = 2, Status = ItemStatus.Active, Type = ItemType.Electrical },
            new Item { PartNumber = "MTR-10HP-1800", Description = "Motor 10HP 1800RPM", PurchaseUOM = "EA", StandardCost = 750.00m, ListPrice = 1125.00m, ReorderPoint = 1, ReorderQuantity = 2, Status = ItemStatus.Active, Type = ItemType.Electrical },
            new Item { PartNumber = "VFD-5HP", Description = "VFD 5HP 480V", PurchaseUOM = "EA", StandardCost = 650.00m, ListPrice = 975.00m, ReorderPoint = 1, ReorderQuantity = 2, Status = ItemStatus.Active, Type = ItemType.Electrical },
            new Item { PartNumber = "PROX-18MM", Description = "Proximity Sensor 18mm", PurchaseUOM = "EA", StandardCost = 45.00m, ListPrice = 67.50m, ReorderPoint = 5, ReorderQuantity = 10, Status = ItemStatus.Active, Type = ItemType.Electrical },
            new Item { PartNumber = "TEMP-RTD", Description = "RTD Temperature Sensor", PurchaseUOM = "EA", StandardCost = 85.00m, ListPrice = 127.50m, ReorderPoint = 2, ReorderQuantity = 5, Status = ItemStatus.Active, Type = ItemType.Electrical }
        };

        protected override async Task<Item?> FindByNaturalKeyAsync(Item item, CancellationToken ct)
            => await Context.Items.FirstOrDefaultAsync(x => x.PartNumber == item.PartNumber, ct);
        protected override string GetNaturalKeyValue(Item item) => item.PartNumber;
        protected override bool ShouldUpdate(Item existing, Item incoming)
            => !StringEquals(existing.Description, incoming.Description) || existing.StandardCost != incoming.StandardCost
               || existing.ListPrice != incoming.ListPrice || existing.ReorderPoint != incoming.ReorderPoint
               || existing.ReorderQuantity != incoming.ReorderQuantity;
        protected override void UpdateEntity(Item existing, Item incoming)
        {
            existing.Description = incoming.Description;
            existing.StandardCost = incoming.StandardCost;
            existing.ListPrice = incoming.ListPrice;
            existing.ReorderPoint = incoming.ReorderPoint;
            existing.ReorderQuantity = incoming.ReorderQuantity;
        }
    }
    #endregion
}
