using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;
using Abs.FixedAssets.Services.Items;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Seeding.Pipelines
{
    public class DemoPackV2Pipeline : ISeedPipeline
    {
        public string Name => "DemoPackV2";
        public string Version => "2.0.0";
        public string Description => "Demo Data Pack v2: Items, Manufacturers, MPNs, VPNs, AVL, Alternates, Supersessions";
        public bool IsDevOnly => true;

        private readonly List<ISeedStep> _steps;
        public IReadOnlyList<ISeedStep> Steps => _steps;

        public DemoPackV2Pipeline(AppDbContext context, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<DemoPackV2Pipeline>();
            _steps = new List<ISeedStep>
            {
                new DemoPackV2ManufacturersSeedStep(context, logger),
                new DemoPackV2ItemsSeedStep(context, logger),
                new DemoPackV2MPNsSeedStep(context, logger),
                new DemoPackV2VPNsSeedStep(context, logger),
                new DemoPackV2AVLSeedStep(context, logger),
                new DemoPackV2AlternatesSeedStep(context, logger),
                new DemoPackV2SupersessionsSeedStep(context, logger),
                new DemoPackV2PMTemplatesSeedStep(context, logger),
                new DemoPackV2PMTemplateAssetsSeedStep(context, logger),
                new DemoPackV2PMSchedulesSeedStep(context, logger)
            };
        }
    }

    #region Manufacturers
    public class DemoPackV2ManufacturersSeedStep : BaseSeedStep<Manufacturer>
    {
        public override string StepName => "DemoPackV2Manufacturers";
        public override string DomainName => "Manufacturers";
        public override string NaturalKeyDescription => "Code";

        public DemoPackV2ManufacturersSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Manufacturer> GetSeedData() => Enumerable.Range(1, 20).Select(i => new Manufacturer
        {
            Code = $"DEMO-MFR-{i:D3}",
            Name = $"DEMO MANUFACTURER {i:D3}",
            Active = true
        });

        protected override async Task<Manufacturer?> FindByNaturalKeyAsync(Manufacturer item, CancellationToken ct)
            => await Context.Manufacturers.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
        protected override string GetNaturalKeyValue(Manufacturer item) => item.Code ?? string.Empty;
        protected override bool ShouldUpdate(Manufacturer existing, Manufacturer incoming)
            => !StringEquals(existing.Name, incoming.Name);
        protected override void UpdateEntity(Manufacturer existing, Manufacturer incoming)
        {
            existing.Name = incoming.Name;
            existing.Active = incoming.Active;
        }
    }
    #endregion

    #region Items
    public class DemoPackV2ItemsSeedStep : BaseSeedStep<Item>
    {
        public override string StepName => "DemoPackV2Items";
        public override string DomainName => "Items";
        public override string NaturalKeyDescription => "PartNumber";

        private static readonly string[] Categories = { "Bearings", "Belts", "Fasteners", "Filters", "Gaskets", "Seals", "Motors", "Pumps", "Valves", "Sensors" };
        private static readonly string[] Prefixes = { "SKF", "FAG", "NSK", "NTN", "TIMKEN", "GATES", "GOODYEAR", "PARKER", "EATON", "HONEYWELL" };

        public DemoPackV2ItemsSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Item> GetSeedData()
        {
            var items = new List<Item>();
            var random = new Random(42); // Fixed seed for reproducibility
            for (int i = 1; i <= 150; i++)
            {
                var catIdx = (i - 1) % Categories.Length;
                var prefixIdx = (i - 1) / 15 % Prefixes.Length;
                
                // Procurement fields - realistically populate for 50+ items
                var stockPolicy = i switch
                {
                    <= 15 => StockPolicy.CriticalSpare, // First 15 are critical spares
                    <= 45 => StockPolicy.Stock,         // Next 30 are stock items
                    <= 60 => StockPolicy.Nonstock,      // Next 15 are nonstock
                    _ => i % 3 == 0 ? StockPolicy.Stock : StockPolicy.Nonstock
                };
                
                var leadTimeDays = stockPolicy switch
                {
                    StockPolicy.CriticalSpare => random.Next(1, 7),    // 1-7 days for critical
                    StockPolicy.Stock => random.Next(5, 21),            // 5-20 days for stock
                    _ => random.Next(14, 31)                             // 14-30 days for nonstock
                };
                
                var basePrice = catIdx switch
                {
                    0 => 25.00m + random.Next(1, 200),   // Bearings: $26-225
                    1 => 15.00m + random.Next(1, 80),    // Belts: $16-95
                    2 => 0.50m + random.Next(1, 10),     // Fasteners: $0.50-10
                    3 => 10.00m + random.Next(1, 50),    // Filters: $11-60
                    4 => 5.00m + random.Next(1, 30),     // Gaskets: $6-35
                    5 => 8.00m + random.Next(1, 25),     // Seals: $9-33
                    6 => 150.00m + random.Next(1, 500),  // Motors: $151-650
                    7 => 200.00m + random.Next(1, 800),  // Pumps: $201-1000
                    8 => 50.00m + random.Next(1, 300),   // Valves: $51-350
                    _ => 30.00m + random.Next(1, 150)    // Sensors: $31-180
                };
                
                items.Add(new Item
                {
                    PartNumber = $"DEMO-PN-{i:D4}",
                    Description = $"{Categories[catIdx]} - {Prefixes[prefixIdx]} Series {i}",
                    Type = ItemType.Part,
                    StockUOM = catIdx switch { 0 or 1 => "EA", 2 or 3 => "PK", _ => "EA" },
                    IsActive = true,
                    MinQuantity = i % 5 == 0 ? 10 : 5,
                    MaxQuantity = i % 5 == 0 ? 100 : 50,
                    ReorderPoint = i % 5 == 0 ? 20 : 10,
                    // Procurement fields (v2-lite)
                    LeadTimeDays = leadTimeDays,
                    MinOrderQty = catIdx <= 2 ? random.Next(1, 11) : random.Next(1, 6),
                    OrderMultiple = catIdx == 2 ? 10 : (catIdx <= 4 ? 5 : 1),
                    PackQty = catIdx switch { 2 => 100, 3 => 12, 4 => 10, _ => 1 },
                    PurchaseUOM = catIdx switch { 2 => "BX", 3 => "PK", _ => "EA" },
                    StockPolicy = stockPolicy,
                    LastPrice = basePrice,
                    CurrencyCode = "USD",
                    PriceEffectiveDate = DateTime.UtcNow.AddDays(-random.Next(1, 90)),
                    ContractFlag = i <= 60, // First 60 items on contract for excellent buyability
                    ContractRef = i <= 60 ? $"CTR-2025-{(i - 1) / 10 + 1:D3}" : null
                });
            }
            return items;
        }

        protected override async Task<Item?> FindByNaturalKeyAsync(Item item, CancellationToken ct)
            => await Context.Items.FirstOrDefaultAsync(x => x.PartNumber == item.PartNumber, ct);
        protected override string GetNaturalKeyValue(Item item) => item.PartNumber;
        protected override bool ShouldUpdate(Item existing, Item incoming)
            => !StringEquals(existing.Description, incoming.Description);
        protected override void UpdateEntity(Item existing, Item incoming)
        {
            existing.Description = incoming.Description;
            existing.StockUOM = incoming.StockUOM;
            existing.MinQuantity = incoming.MinQuantity;
            existing.MaxQuantity = incoming.MaxQuantity;
            existing.ReorderPoint = incoming.ReorderPoint;
        }
    }
    #endregion

    #region MPNs (ItemManufacturerParts)
    public class DemoPackV2MPNsSeedStep : ISeedStep
    {
        public string StepName => "DemoPackV2MPNs";
        public string DomainName => "ItemManufacturerParts";
        public string NaturalKeyDescription => "ManufacturerId+MfrPartNumber";

        private readonly AppDbContext _context;
        private readonly ILogger _logger;

        public DemoPackV2MPNsSeedStep(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken ct = default)
        {
            var result = new SeedStepResult { StepName = StepName, DomainName = DomainName, StartTime = DateTime.UtcNow };
            try
            {
                var items = await _context.Items.Where(x => x.PartNumber.StartsWith("DEMO-PN-")).ToListAsync(ct);
                var mfrs = await _context.Manufacturers.Where(x => x.Code != null && x.Code.StartsWith("DEMO-MFR-")).ToListAsync(ct);
                if (!items.Any() || !mfrs.Any()) { result.EndTime = DateTime.UtcNow; return result; }

                foreach (var item in items)
                {
                    var mfrIdx = (int.Parse(item.PartNumber.Replace("DEMO-PN-", "")) - 1) % mfrs.Count;
                    for (int suffix = 1; suffix <= (item.PartNumber.EndsWith("0") ? 3 : 1); suffix++)
                    {
                        var mfr = mfrs[(mfrIdx + suffix - 1) % mfrs.Count];
                        var mpn = $"DEMO-MPN-{mfr.Code}-{item.PartNumber.Replace("DEMO-PN-", "")}-{suffix}";

                        var existing = await _context.ItemManufacturerParts
                            .FirstOrDefaultAsync(x => x.ManufacturerId == mfr.Id && x.MfrPartNumber == mpn, ct);

                        if (existing == null)
                        {
                            _context.ItemManufacturerParts.Add(new ItemManufacturerPart
                            {
                                ItemId = item.Id,
                                ManufacturerId = mfr.Id,
                                MfrPartNumber = mpn,
                                IsActive = true
                            });
                            result.Inserted++;
                        }
                        else
                        {
                            result.Skipped++;
                        }
                    }
                }
                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex) { result.Failed++; result.Errors.Add(ex.Message); _logger.LogError(ex, "MPN seed error"); }
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        public async Task<PreviewStepResult> PreviewAsync(CancellationToken ct = default)
        {
            var result = new PreviewStepResult { StepName = StepName, DomainName = DomainName };
            var items = await _context.Items.Where(x => x.PartNumber.StartsWith("DEMO-PN-")).ToListAsync(ct);
            var mfrs = await _context.Manufacturers.Where(x => x.Code != null && x.Code.StartsWith("DEMO-MFR-")).ToListAsync(ct);
            if (!items.Any() || !mfrs.Any()) return result;

            foreach (var item in items)
            {
                var mfrIdx = (int.Parse(item.PartNumber.Replace("DEMO-PN-", "")) - 1) % mfrs.Count;
                for (int suffix = 1; suffix <= (item.PartNumber.EndsWith("0") ? 3 : 1); suffix++)
                {
                    var mfr = mfrs[(mfrIdx + suffix - 1) % mfrs.Count];
                    var mpn = $"DEMO-MPN-{mfr.Code}-{item.PartNumber.Replace("DEMO-PN-", "")}-{suffix}";
                    var exists = await _context.ItemManufacturerParts.AnyAsync(x => x.ManufacturerId == mfr.Id && x.MfrPartNumber == mpn, ct);
                    if (exists) result.WouldSkip++; else result.WouldCreate++;
                    result.TotalInSeedData++;
                }
            }
            return result;
        }
    }
    #endregion

    #region VPNs (VendorItemParts)
    public class DemoPackV2VPNsSeedStep : ISeedStep
    {
        public string StepName => "DemoPackV2VPNs";
        public string DomainName => "VendorItemParts";
        public string NaturalKeyDescription => "VendorId+VendorPartNumber";

        private readonly AppDbContext _context;
        private readonly ILogger _logger;

        private static readonly string[] VendorDomains = new[]
        {
            "https://www.grainger.com/product/",
            "https://www.mcmaster.com/",
            "https://www.motion.com/products/",
            "https://www.fastenal.com/products/",
            "https://www.mscdirect.com/product/"
        };

        private static readonly string[] PlaceholderImages = new[]
        {
            "https://images.unsplash.com/photo-1558618666-fcd25c85cd64?w=200&h=200&fit=crop",
            "https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?w=200&h=200&fit=crop",
            "https://images.unsplash.com/photo-1581092160562-40aa08e78837?w=200&h=200&fit=crop",
            "https://images.unsplash.com/photo-1565193566173-7a0ee3dbe261?w=200&h=200&fit=crop",
            "https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?w=200&h=200&fit=crop"
        };

        public DemoPackV2VPNsSeedStep(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        private (string? catalogUrl, string? datasheetUrl, string? imageUrl) GetCatalogData(int itemNum, int vendorIdx)
        {
            if (itemNum > 60) return (null, null, null);
            var domain = VendorDomains[vendorIdx % VendorDomains.Length];
            var image = PlaceholderImages[itemNum % PlaceholderImages.Length];
            var datasheetDomain = "https://docs.example.com/specs/";
            return ($"{domain}demo-part-{itemNum:D4}", $"{datasheetDomain}demo-part-{itemNum:D4}.pdf", image);
        }

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken ct = default)
        {
            var result = new SeedStepResult { StepName = StepName, DomainName = DomainName, StartTime = DateTime.UtcNow };
            try
            {
                var items = await _context.Items.Where(x => x.PartNumber.StartsWith("DEMO-PN-")).ToListAsync(ct);
                var vendors = await _context.Vendors.Where(x => x.IsActive).Take(10).ToListAsync(ct);
                if (!items.Any() || !vendors.Any()) { result.EndTime = DateTime.UtcNow; return result; }

                var multiVendorItems = items.Where(x => int.Parse(x.PartNumber.Replace("DEMO-PN-", "")) <= 5).ToList();
                foreach (var item in multiVendorItems)
                {
                    var itemNum = int.Parse(item.PartNumber.Replace("DEMO-PN-", ""));
                    var vendorCount = 0;
                    foreach (var vendor in vendors)
                    {
                        var vpn = $"DEMO-VPN-{vendor.Id}-{item.PartNumber.Replace("DEMO-PN-", "")}";
                        var existing = await _context.VendorItemParts
                            .FirstOrDefaultAsync(x => x.VendorId == vendor.Id && x.VendorPartNumber == vpn, ct);
                        var (catalogUrl, datasheetUrl, imageUrl) = GetCatalogData(itemNum, vendorCount);
                        var random = new Random(itemNum * 100 + vendorCount);
                        var vendorLeadTime = random.Next(3, 21);
                        var vendorPrice = 10.0m + (decimal)random.Next(1, 300) + (vendorCount * 5);
                        if (existing == null)
                        {
                            _context.VendorItemParts.Add(new VendorItemPart
                            {
                                ItemId = item.Id,
                                VendorId = vendor.Id,
                                VendorPartNumber = vpn,
                                CatalogUrl = catalogUrl,
                                DatasheetUrl = datasheetUrl,
                                ExternalImageUrl = imageUrl,
                                Preferred = vendorCount == 0,
                                IsActive = true,
                                LeadTimeDays = vendorLeadTime,
                                UnitPrice = vendorPrice,
                                PackQty = vendorCount == 0 ? 1 : (vendorCount + 1),
                                VendorUom = "EA",
                                PriceEffectiveDate = DateTime.UtcNow.AddDays(-random.Next(1, 60))
                            });
                            result.Inserted++;
                        }
                        else
                        {
                            if (existing.CatalogUrl == null && catalogUrl != null)
                            {
                                existing.CatalogUrl = catalogUrl;
                                existing.DatasheetUrl = datasheetUrl;
                                existing.ExternalImageUrl = imageUrl;
                                existing.Preferred = vendorCount == 0;
                                result.Updated++;
                            }
                            else { result.Skipped++; }
                        }
                        vendorCount++;
                    }
                }

                foreach (var item in items.Skip(5))
                {
                    var itemNum = int.Parse(item.PartNumber.Replace("DEMO-PN-", ""));
                    var vendorIdx = (itemNum - 1) % vendors.Count;
                    var vendor = vendors[vendorIdx];
                    var vpn = $"DEMO-VPN-{vendor.Id}-{item.PartNumber.Replace("DEMO-PN-", "")}";
                    var existing = await _context.VendorItemParts
                        .FirstOrDefaultAsync(x => x.VendorId == vendor.Id && x.VendorPartNumber == vpn, ct);
                    var (catalogUrl, datasheetUrl, imageUrl) = GetCatalogData(itemNum, vendorIdx);
                    if (existing == null)
                    {
                        _context.VendorItemParts.Add(new VendorItemPart
                        {
                            ItemId = item.Id,
                            VendorId = vendor.Id,
                            VendorPartNumber = vpn,
                            CatalogUrl = catalogUrl,
                            DatasheetUrl = datasheetUrl,
                            ExternalImageUrl = imageUrl,
                            Preferred = true,
                            IsActive = true
                        });
                        result.Inserted++;
                    }
                    else
                    {
                        if (existing.CatalogUrl == null && catalogUrl != null)
                        {
                            existing.CatalogUrl = catalogUrl;
                            existing.DatasheetUrl = datasheetUrl;
                            existing.ExternalImageUrl = imageUrl;
                            existing.Preferred = true;
                            result.Updated++;
                        }
                        else { result.Skipped++; }
                    }
                }

                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex) { result.Failed++; result.Errors.Add(ex.Message); _logger.LogError(ex, "VPN seed error"); }
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        public async Task<PreviewStepResult> PreviewAsync(CancellationToken ct = default)
        {
            var result = new PreviewStepResult { StepName = StepName, DomainName = DomainName };
            var items = await _context.Items.Where(x => x.PartNumber.StartsWith("DEMO-PN-")).ToListAsync(ct);
            var vendors = await _context.Vendors.Where(x => x.IsActive).Take(10).ToListAsync(ct);
            if (!items.Any() || !vendors.Any()) return result;

            var multiVendorItems = items.Where(x => int.Parse(x.PartNumber.Replace("DEMO-PN-", "")) <= 5).ToList();
            foreach (var item in multiVendorItems)
            {
                foreach (var vendor in vendors)
                {
                    var vpn = $"DEMO-VPN-{vendor.Id}-{item.PartNumber.Replace("DEMO-PN-", "")}";
                    var exists = await _context.VendorItemParts.AnyAsync(x => x.VendorId == vendor.Id && x.VendorPartNumber == vpn, ct);
                    if (exists) result.WouldSkip++; else result.WouldCreate++;
                    result.TotalInSeedData++;
                }
            }
            foreach (var item in items.Skip(5))
            {
                var vendorIdx = (int.Parse(item.PartNumber.Replace("DEMO-PN-", "")) - 1) % vendors.Count;
                var vendor = vendors[vendorIdx];
                var vpn = $"DEMO-VPN-{vendor.Id}-{item.PartNumber.Replace("DEMO-PN-", "")}";
                var exists = await _context.VendorItemParts.AnyAsync(x => x.VendorId == vendor.Id && x.VendorPartNumber == vpn, ct);
                if (exists) result.WouldSkip++; else result.WouldCreate++;
                result.TotalInSeedData++;
            }
            return result;
        }
    }
    #endregion

    #region AVL (ItemApprovedVendors)
    public class DemoPackV2AVLSeedStep : ISeedStep
    {
        public string StepName => "DemoPackV2AVL";
        public string DomainName => "ItemApprovedVendors";
        public string NaturalKeyDescription => "ItemId+VendorId";

        private readonly AppDbContext _context;
        private readonly ILogger _logger;

        public DemoPackV2AVLSeedStep(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken ct = default)
        {
            var result = new SeedStepResult { StepName = StepName, DomainName = DomainName, StartTime = DateTime.UtcNow };
            try
            {
                var items = await _context.Items.Where(x => x.PartNumber.StartsWith("DEMO-PN-")).ToListAsync(ct);
                var vendors = await _context.Vendors.Where(x => x.IsActive).Take(10).ToListAsync(ct);
                if (!items.Any() || !vendors.Any()) { result.EndTime = DateTime.UtcNow; return result; }

                foreach (var item in items)
                {
                    var itemNum = int.Parse(item.PartNumber.Replace("DEMO-PN-", ""));
                    var vendorCount = 1 + (itemNum % 3);
                    var preferredVendorIdx = (itemNum - 1) % vendors.Count;

                    for (int v = 0; v < vendorCount && v < vendors.Count; v++)
                    {
                        var vendor = vendors[(preferredVendorIdx + v) % vendors.Count];
                        var isPreferred = v == 0;
                        var status = v == 0 ? AvlApprovalStatus.Approved : (v == 1 ? AvlApprovalStatus.Conditional : AvlApprovalStatus.Blocked);

                        var existing = await _context.ItemApprovedVendors
                            .FirstOrDefaultAsync(x => x.ItemId == item.Id && x.VendorId == vendor.Id, ct);

                        if (existing == null)
                        {
                            if (isPreferred)
                            {
                                var currentPreferred = await _context.ItemApprovedVendors
                                    .Where(x => x.ItemId == item.Id && x.IsPreferred)
                                    .ToListAsync(ct);
                                foreach (var cp in currentPreferred) cp.IsPreferred = false;
                            }

                            var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Default tenant required for seeding");
                            _context.ItemApprovedVendors.Add(new ItemApprovedVendor
                            {
                                ItemId = item.Id,
                                VendorId = vendor.Id,
                                IsPreferred = isPreferred,
                                ApprovalStatus = status,
                                Notes = $"Demo AVL entry for {item.PartNumber}",
                                TenantId = defaultTenant.Id,
                                CreatedAtUtc = DateTime.UtcNow
                            });
                            result.Inserted++;
                        }
                        else { result.Skipped++; }
                    }
                }

                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex) { result.Failed++; result.Errors.Add(ex.Message); _logger.LogError(ex, "AVL seed error"); }
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        public async Task<PreviewStepResult> PreviewAsync(CancellationToken ct = default)
        {
            var result = new PreviewStepResult { StepName = StepName, DomainName = DomainName };
            var items = await _context.Items.Where(x => x.PartNumber.StartsWith("DEMO-PN-")).ToListAsync(ct);
            var vendors = await _context.Vendors.Where(x => x.IsActive).Take(10).ToListAsync(ct);
            if (!items.Any() || !vendors.Any()) return result;

            foreach (var item in items)
            {
                var itemNum = int.Parse(item.PartNumber.Replace("DEMO-PN-", ""));
                var vendorCount = 1 + (itemNum % 3);
                var preferredVendorIdx = (itemNum - 1) % vendors.Count;

                for (int v = 0; v < vendorCount && v < vendors.Count; v++)
                {
                    var vendor = vendors[(preferredVendorIdx + v) % vendors.Count];
                    var exists = await _context.ItemApprovedVendors.AnyAsync(x => x.ItemId == item.Id && x.VendorId == vendor.Id, ct);
                    if (exists) result.WouldSkip++; else result.WouldCreate++;
                    result.TotalInSeedData++;
                }
            }
            return result;
        }
    }
    #endregion

    #region Alternates
    public class DemoPackV2AlternatesSeedStep : ISeedStep
    {
        public string StepName => "DemoPackV2Alternates";
        public string DomainName => "ItemAlternates";
        public string NaturalKeyDescription => "ItemId+AlternateItemId";

        private readonly AppDbContext _context;
        private readonly ILogger _logger;

        public DemoPackV2AlternatesSeedStep(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken ct = default)
        {
            var result = new SeedStepResult { StepName = StepName, DomainName = DomainName, StartTime = DateTime.UtcNow };
            try
            {
                var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Default tenant required for seeding");
                var items = await _context.Items.Where(x => x.PartNumber.StartsWith("DEMO-PN-")).OrderBy(x => x.PartNumber).ToListAsync(ct);
                if (items.Count < 10) { result.EndTime = DateTime.UtcNow; return result; }

                var targetItems = items.Where(x => int.Parse(x.PartNumber.Replace("DEMO-PN-", "")) % 3 == 0).Take(45).ToList();

                foreach (var item in targetItems)
                {
                    var itemNum = int.Parse(item.PartNumber.Replace("DEMO-PN-", ""));
                    var altCount = 2 + (itemNum % 3);

                    for (int a = 1; a <= altCount; a++)
                    {
                        var altIdx = (items.IndexOf(item) + a * 10) % items.Count;
                        var altItem = items[altIdx];
                        if (altItem.Id == item.Id) continue;

                        var existing = await _context.ItemAlternates
                            .FirstOrDefaultAsync(x => x.ItemId == item.Id && x.AlternateItemId == altItem.Id, ct);

                        if (existing == null)
                        {
                            var rank = (a - 1) / 2 + 1;
                            _context.ItemAlternates.Add(new ItemAlternate
                            {
                                ItemId = item.Id,
                                AlternateItemId = altItem.Id,
                                AlternateType = a % 2 == 0 ? AlternateType.Equivalent : AlternateType.Substitute,
                                Rank = rank,
                                Reason = $"Alternate {a} for {item.PartNumber}",
                                IsApproved = a <= 3,
                                TenantId = defaultTenant.Id,
                                CreatedAtUtc = DateTime.UtcNow
                            });
                            result.Inserted++;
                        }
                        else { result.Skipped++; }
                    }
                }

                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex) { result.Failed++; result.Errors.Add(ex.Message); _logger.LogError(ex, "Alternates seed error"); }
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        public async Task<PreviewStepResult> PreviewAsync(CancellationToken ct = default)
        {
            var result = new PreviewStepResult { StepName = StepName, DomainName = DomainName };
            var items = await _context.Items.Where(x => x.PartNumber.StartsWith("DEMO-PN-")).OrderBy(x => x.PartNumber).ToListAsync(ct);
            if (items.Count < 10) return result;

            var targetItems = items.Where(x => int.Parse(x.PartNumber.Replace("DEMO-PN-", "")) % 3 == 0).Take(45).ToList();

            foreach (var item in targetItems)
            {
                var itemNum = int.Parse(item.PartNumber.Replace("DEMO-PN-", ""));
                var altCount = 2 + (itemNum % 3);

                for (int a = 1; a <= altCount; a++)
                {
                    var altIdx = (items.IndexOf(item) + a * 10) % items.Count;
                    var altItem = items[altIdx];
                    if (altItem.Id == item.Id) continue;

                    var exists = await _context.ItemAlternates.AnyAsync(x => x.ItemId == item.Id && x.AlternateItemId == altItem.Id, ct);
                    if (exists) result.WouldSkip++; else result.WouldCreate++;
                    result.TotalInSeedData++;
                }
            }
            return result;
        }
    }
    #endregion

    #region Supersessions
    public class DemoPackV2SupersessionsSeedStep : ISeedStep
    {
        public string StepName => "DemoPackV2Supersessions";
        public string DomainName => "ItemSupersessions";
        public string NaturalKeyDescription => "OldItemId";

        private readonly AppDbContext _context;
        private readonly ILogger _logger;

        public DemoPackV2SupersessionsSeedStep(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken ct = default)
        {
            var result = new SeedStepResult { StepName = StepName, DomainName = DomainName, StartTime = DateTime.UtcNow };
            try
            {
                var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Default tenant required for seeding");
                var items = await _context.Items.Where(x => x.PartNumber.StartsWith("DEMO-PN-")).OrderBy(x => x.PartNumber).ToListAsync(ct);
                if (items.Count < 30) { result.EndTime = DateTime.UtcNow; return result; }

                var chainStarts = items.Where(x => int.Parse(x.PartNumber.Replace("DEMO-PN-", "")) % 10 == 1).Take(15).ToList();

                foreach (var chainStart in chainStarts)
                {
                    var startIdx = items.IndexOf(chainStart);
                    var chainLength = 2 + (startIdx % 2);

                    for (int c = 0; c < chainLength - 1; c++)
                    {
                        var oldIdx = startIdx + c;
                        var newIdx = startIdx + c + 1;
                        if (newIdx >= items.Count) break;

                        var oldItem = items[oldIdx];
                        var newItem = items[newIdx];

                        var existing = await _context.ItemSupersessions
                            .FirstOrDefaultAsync(x => x.OldItemId == oldItem.Id, ct);

                        if (existing == null)
                        {
                            _context.ItemSupersessions.Add(new ItemSupersession
                            {
                                OldItemId = oldItem.Id,
                                NewItemId = newItem.Id,
                                EffectiveFromUtc = DateTime.UtcNow.AddMonths(-6 + c),
                                Reason = $"Superseded by {newItem.PartNumber}",
                                TenantId = defaultTenant.Id,
                                CreatedAtUtc = DateTime.UtcNow
                            });
                            result.Inserted++;
                        }
                        else { result.Skipped++; }
                    }
                }

                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex) { result.Failed++; result.Errors.Add(ex.Message); _logger.LogError(ex, "Supersessions seed error"); }
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        public async Task<PreviewStepResult> PreviewAsync(CancellationToken ct = default)
        {
            var result = new PreviewStepResult { StepName = StepName, DomainName = DomainName };
            var items = await _context.Items.Where(x => x.PartNumber.StartsWith("DEMO-PN-")).OrderBy(x => x.PartNumber).ToListAsync(ct);
            if (items.Count < 30) return result;

            var chainStarts = items.Where(x => int.Parse(x.PartNumber.Replace("DEMO-PN-", "")) % 10 == 1).Take(15).ToList();

            foreach (var chainStart in chainStarts)
            {
                var startIdx = items.IndexOf(chainStart);
                var chainLength = 2 + (startIdx % 2);

                for (int c = 0; c < chainLength - 1; c++)
                {
                    var oldIdx = startIdx + c;
                    var newIdx = startIdx + c + 1;
                    if (newIdx >= items.Count) break;

                    var oldItem = items[oldIdx];
                    var exists = await _context.ItemSupersessions.AnyAsync(x => x.OldItemId == oldItem.Id, ct);
                    if (exists) result.WouldSkip++; else result.WouldCreate++;
                    result.TotalInSeedData++;
                }
            }
            return result;
        }
    }
    #endregion

    #region PMTemplates
    /// <summary>
    /// Seeds PM Templates with released revisions.
    /// Creates deterministic PM templates with realistic maintenance cadences.
    /// Natural key: Code (e.g., "PM-DAILY-SAFETY", "PM-WEEKLY-LUBE")
    /// Each template gets a released PMTemplateRevision and CurrentReleasedRevisionId is set.
    /// </summary>
    public class DemoPackV2PMTemplatesSeedStep : ISeedStep
    {
        public string StepName => "DemoPackV2PMTemplates";
        public string DomainName => "PMTemplates + PMTemplateRevisions";
        public string NaturalKeyDescription => "Code";

        private readonly AppDbContext _context;
        private readonly ILogger _logger;

        public DemoPackV2PMTemplatesSeedStep(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        private static readonly (string Code, string Name, string Description, RecurrenceType Interval, int IntervalValue, PMPriority Priority, bool RequiresLOTO)[] TemplateDefinitions = new[]
        {
            ("PM-DAILY-SAFETY", "Daily Safety Inspection", "Daily visual safety inspection of equipment and guards", RecurrenceType.Daily, 1, PMPriority.High, false),
            ("PM-WEEKLY-LUBE", "Weekly Lubrication", "Weekly lubrication of bearings and moving parts", RecurrenceType.Weekly, 1, PMPriority.Medium, false),
            ("PM-WEEKLY-CLEAN", "Weekly Cleaning", "Weekly cleaning of equipment and surrounding area", RecurrenceType.Weekly, 1, PMPriority.Low, false),
            ("PM-BIWEEKLY-FILTER", "Bi-Weekly Filter Check", "Bi-weekly inspection and cleaning of filters", RecurrenceType.Weekly, 2, PMPriority.Medium, false),
            ("PM-MONTHLY-INSPECT", "Monthly Inspection", "Comprehensive monthly inspection and adjustment", RecurrenceType.Monthly, 1, PMPriority.Medium, false),
            ("PM-MONTHLY-CALIBRATE", "Monthly Calibration", "Monthly calibration check of sensors and gauges", RecurrenceType.Monthly, 1, PMPriority.High, true),
            ("PM-QUARTERLY-OVERHAUL", "Quarterly Overhaul", "Quarterly major inspection and component replacement", RecurrenceType.Quarterly, 1, PMPriority.High, true),
            ("PM-QUARTERLY-ELECTRICAL", "Quarterly Electrical Inspection", "Quarterly inspection of electrical connections and panels", RecurrenceType.Quarterly, 1, PMPriority.Critical, true),
            ("PM-SEMIANNUAL-MOTOR", "Semi-Annual Motor Service", "Semi-annual motor inspection and bearing replacement", RecurrenceType.Monthly, 6, PMPriority.High, true),
            ("PM-ANNUAL-MAJOR", "Annual Major Overhaul", "Annual comprehensive overhaul and rebuild", RecurrenceType.Annually, 1, PMPriority.Critical, true),
            ("PM-ANNUAL-CERT", "Annual Certification", "Annual regulatory certification inspection", RecurrenceType.Annually, 1, PMPriority.Critical, true),
            ("PM-COND-VIBRATION", "Condition-Based Vibration Analysis", "Vibration analysis based on sensor readings", RecurrenceType.Monthly, 1, PMPriority.Medium, false)
        };

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken ct = default)
        {
            var result = new SeedStepResult { StepName = StepName, DomainName = DomainName, StartTime = DateTime.UtcNow };
            try
            {
                var existingTemplates = await _context.PMTemplates
                    .Where(t => t.Code.StartsWith("PM-"))
                    .ToDictionaryAsync(t => t.Code, ct);

                var companies = await _context.Companies.Take(3).ToListAsync(ct);
                var defaultCompanyId = companies.FirstOrDefault()?.Id;

                foreach (var def in TemplateDefinitions)
                {
                    if (existingTemplates.TryGetValue(def.Code, out var existing))
                    {
                        if (existing.CurrentReleasedRevisionId == null)
                        {
                            var revision = await EnsureReleasedRevisionAsync(existing, ct);
                            existing.CurrentReleasedRevisionId = revision.Id;
                            result.Updated++;
                        }
                        else
                        {
                            result.Skipped++;
                        }
                        continue;
                    }

                    var template = new PMTemplate
                    {
                        Code = def.Code,
                        Name = def.Name,
                        Description = def.Description,
                        Type = MaintenanceType.Preventative,
                        Priority = def.Priority,
                        TriggerType = PMTriggerType.Calendar,
                        CalendarInterval = def.Interval,
                        CalendarIntervalValue = def.IntervalValue,
                        EstimatedHours = def.Priority switch
                        {
                            PMPriority.Low => 0.5m,
                            PMPriority.Medium => 1.0m,
                            PMPriority.High => 2.0m,
                            PMPriority.Critical => 4.0m,
                            _ => 1.0m
                        },
                        RequiresLOTO = def.RequiresLOTO,
                        RequiresShutdown = def.RequiresLOTO,
                        IsActive = true,
                        CompanyId = defaultCompanyId,
                        CreatedBy = "DemoPackV2Seed"
                    };

                    _context.PMTemplates.Add(template);
                    await _context.SaveChangesAsync(ct);

                    var newRevision = await CreateReleasedRevisionAsync(template, ct);
                    template.CurrentReleasedRevisionId = newRevision.Id;
                    await _context.SaveChangesAsync(ct);

                    result.Inserted++;
                }

                _logger.LogInformation("PMTemplates seed: {Inserted} inserted, {Updated} updated, {Skipped} skipped",
                    result.Inserted, result.Updated, result.Skipped);
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(ex.Message);
                _logger.LogError(ex, "PMTemplates seed error");
            }
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        private async Task<PMTemplateRevision> EnsureReleasedRevisionAsync(PMTemplate template, CancellationToken ct)
        {
            var existingRevision = await _context.Set<PMTemplateRevision>()
                .FirstOrDefaultAsync(r => r.PMTemplateId == template.Id && r.Status == RevisionStatus.Released, ct);

            if (existingRevision != null)
                return existingRevision;

            return await CreateReleasedRevisionAsync(template, ct);
        }

        private async Task<PMTemplateRevision> CreateReleasedRevisionAsync(PMTemplate template, CancellationToken ct)
        {
            var revision = new PMTemplateRevision
            {
                PMTemplateId = template.Id,
                RevisionCode = "A",
                Status = RevisionStatus.Released,
                Name = template.Name,
                Description = template.Description,
                Type = template.Type,
                Priority = template.Priority,
                TriggerType = template.TriggerType,
                CalendarInterval = template.CalendarInterval,
                CalendarIntervalValue = template.CalendarIntervalValue,
                EstimatedHours = template.EstimatedHours,
                RequiresLOTO = template.RequiresLOTO,
                RequiresShutdown = template.RequiresShutdown,
                ChangeReason = "Initial release from DemoPackV2 seed",
                EffectiveFromUtc = DateTime.UtcNow.AddDays(-30),
                ReleasedAtUtc = DateTime.UtcNow.AddDays(-30),
                CreatedByUserId = "DemoPackV2Seed",
                ApprovedByUserId = "DemoPackV2Seed",
                ApprovedAtUtc = DateTime.UtcNow.AddDays(-30)
            };

            _context.Set<PMTemplateRevision>().Add(revision);
            await _context.SaveChangesAsync(ct);
            return revision;
        }

        public async Task<PreviewStepResult> PreviewAsync(CancellationToken ct = default)
        {
            var result = new PreviewStepResult { StepName = StepName, DomainName = DomainName };
            var existing = await _context.PMTemplates.Where(t => t.Code.StartsWith("PM-")).Select(t => t.Code).ToListAsync(ct);

            foreach (var def in TemplateDefinitions)
            {
                result.TotalInSeedData++;
                if (existing.Contains(def.Code))
                    result.WouldSkip++;
                else
                    result.WouldCreate++;
            }
            return result;
        }
    }
    #endregion

    #region PMTemplateAssets
    /// <summary>
    /// Seeds PMTemplateAsset assignments - links PM templates to assets.
    /// Creates deterministic assignments: first 30 assets per site get assigned templates.
    /// Natural key: (AssetId, PMTemplateId)
    /// </summary>
    public class DemoPackV2PMTemplateAssetsSeedStep : ISeedStep
    {
        public string StepName => "DemoPackV2PMTemplateAssets";
        public string DomainName => "PMTemplateAssets";
        public string NaturalKeyDescription => "(AssetId, PMTemplateId)";

        private readonly AppDbContext _context;
        private readonly ILogger _logger;

        public DemoPackV2PMTemplateAssetsSeedStep(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken ct = default)
        {
            var result = new SeedStepResult { StepName = StepName, DomainName = DomainName, StartTime = DateTime.UtcNow };
            try
            {
                var templates = await _context.PMTemplates
                    .Where(t => t.IsActive && t.Code.StartsWith("PM-"))
                    .OrderBy(t => t.Code)
                    .ToListAsync(ct);

                if (!templates.Any())
                {
                    result.Errors.Add("No active PM templates found - cannot seed PMTemplateAssets");
                    result.Failed++;
                    result.EndTime = DateTime.UtcNow;
                    return result;
                }

                var sites = await _context.Sites.Include(s => s.Company).ToListAsync(ct);
                if (!sites.Any())
                {
                    result.Errors.Add("No sites found - cannot seed PMTemplateAssets");
                    result.Failed++;
                    result.EndTime = DateTime.UtcNow;
                    return result;
                }

                var existingKeys = await _context.Set<PMTemplateAsset>()
                    .Select(a => new { a.AssetId, a.PMTemplateId })
                    .ToListAsync(ct);
                var existingSet = existingKeys.Select(k => $"{k.AssetId}|{k.PMTemplateId}").ToHashSet();

                foreach (var site in sites)
                {
                    var siteAssets = await _context.Assets
                        .Where(a => a.SiteId == site.Id && a.Status == AssetStatus.Active)
                        .OrderBy(a => a.Id)
                        .Take(30)
                        .ToListAsync(ct);

                    if (!siteAssets.Any())
                        continue;

                    var templateIndex = 0;
                    foreach (var asset in siteAssets)
                    {
                        var numTemplates = (asset.Id % 3) + 1;
                        for (int t = 0; t < numTemplates && t < templates.Count; t++)
                        {
                            var template = templates[(templateIndex + t) % templates.Count];
                            var key = $"{asset.Id}|{template.Id}";

                            if (existingSet.Contains(key))
                            {
                                result.Skipped++;
                                continue;
                            }

                            var assignment = new PMTemplateAsset
                            {
                                AssetId = asset.Id,
                                PMTemplateId = template.Id,
                                IsActive = true,
                                NextDueDate = DateTime.UtcNow.AddDays(-5 + (asset.Id % 60))
                            };

                            _context.Set<PMTemplateAsset>().Add(assignment);
                            existingSet.Add(key);
                            result.Inserted++;
                        }
                        templateIndex++;
                    }
                }

                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("PMTemplateAssets seed: {Inserted} inserted, {Skipped} skipped",
                    result.Inserted, result.Skipped);
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(ex.Message);
                _logger.LogError(ex, "PMTemplateAssets seed error");
            }
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        public async Task<PreviewStepResult> PreviewAsync(CancellationToken ct = default)
        {
            var result = new PreviewStepResult { StepName = StepName, DomainName = DomainName };

            var templates = await _context.PMTemplates
                .Where(t => t.IsActive && t.Code.StartsWith("PM-"))
                .ToListAsync(ct);
            if (!templates.Any()) return result;

            var existingKeys = await _context.Set<PMTemplateAsset>()
                .Select(a => new { a.AssetId, a.PMTemplateId })
                .ToListAsync(ct);
            var existingSet = existingKeys.Select(k => $"{k.AssetId}|{k.PMTemplateId}").ToHashSet();

            var sites = await _context.Sites.ToListAsync(ct);
            foreach (var site in sites)
            {
                var siteAssets = await _context.Assets
                    .Where(a => a.SiteId == site.Id && a.Status == AssetStatus.Active)
                    .OrderBy(a => a.Id)
                    .Take(30)
                    .ToListAsync(ct);

                var templateIndex = 0;
                foreach (var asset in siteAssets)
                {
                    var numTemplates = (asset.Id % 3) + 1;
                    for (int t = 0; t < numTemplates && t < templates.Count; t++)
                    {
                        var template = templates[(templateIndex + t) % templates.Count];
                        var key = $"{asset.Id}|{template.Id}";
                        result.TotalInSeedData++;
                        if (existingSet.Contains(key))
                            result.WouldSkip++;
                        else
                            result.WouldCreate++;
                    }
                    templateIndex++;
                }
            }
            return result;
        }
    }
    #endregion

    #region PMSchedules
    /// <summary>
    /// Seeds PMSchedules derived from PMTemplateAsset assignments.
    /// Creates schedules for ALL companies/sites where PMTemplateAssets exist.
    /// Natural key: (CompanyId, SiteId, PMTemplateId) - one schedule per unique combination.
    /// </summary>
    public class DemoPackV2PMSchedulesSeedStep : ISeedStep
    {
        public string StepName => "DemoPackV2PMSchedules";
        public string DomainName => "PMSchedules";
        public string NaturalKeyDescription => "(CompanyId, SiteId, PMTemplateId) - derived from PMTemplateAsset assignments";

        private readonly AppDbContext _context;
        private readonly ILogger _logger;
        private const int MaxSchedulesPerSite = 5;

        public DemoPackV2PMSchedulesSeedStep(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken ct = default)
        {
            var result = new SeedStepResult { StepName = StepName, DomainName = DomainName, StartTime = DateTime.UtcNow };
            try
            {
                var tenant = await _context.Tenants.FirstOrDefaultAsync(ct);
                if (tenant == null)
                {
                    result.Errors.Add("No tenant found - cannot seed PMSchedules");
                    result.Failed++;
                    result.EndTime = DateTime.UtcNow;
                    return result;
                }

                var assignments = await _context.Set<PMTemplateAsset>()
                    .Include(a => a.Asset)
                        .ThenInclude(a => a!.Site)
                            .ThenInclude(s => s!.Company)
                    .Include(a => a.PMTemplate)
                    .Where(a => a.IsActive && a.PMTemplate != null && a.PMTemplate.IsActive)
                    .Where(a => a.Asset != null && a.Asset.Site != null && a.Asset.Site.Company != null)
                    .OrderBy(a => a.Asset!.Site!.CompanyId)
                    .ThenBy(a => a.Asset!.SiteId)
                    .ThenBy(a => a.AssetId)
                    .ThenBy(a => a.PMTemplateId)
                    .ToListAsync(ct);

                if (!assignments.Any())
                {
                    _logger.LogWarning("No active PMTemplateAsset assignments found - falling back to template-based seeding");
                    await FallbackSeedFromTemplatesAsync(result, tenant.Id, ct);
                    result.EndTime = DateTime.UtcNow;
                    return result;
                }

                var existingKeys = await _context.PMSchedules
                    .Select(s => new { s.CompanyId, s.SiteId, s.PMTemplateId })
                    .ToListAsync(ct);
                var existingSet = existingKeys.Select(k => $"{k.CompanyId}|{k.SiteId}|{k.PMTemplateId}").ToHashSet();

                var grouped = assignments
                    .GroupBy(a => new { CompanyId = a.Asset!.Site!.CompanyId, SiteId = a.Asset!.SiteId })
                    .ToList();

                var scheduleIndex = 0;
                var dueDaysOffsets = new[] { -5, -2, 0, 3, 7, 14, 21, 30, 45 };

                foreach (var siteGroup in grouped)
                {
                    var companyId = siteGroup.Key.CompanyId;
                    var siteId = siteGroup.Key.SiteId;
                    var company = siteGroup.First().Asset!.Site!.Company;
                    var site = siteGroup.First().Asset!.Site;

                    var selectedAssignments = siteGroup
                        .GroupBy(a => a.PMTemplateId)
                        .Take(MaxSchedulesPerSite)
                        .Select(g => g.First())
                        .ToList();

                    foreach (var assignment in selectedAssignments)
                    {
                        var naturalKey = $"{companyId}|{siteId}|{assignment.PMTemplateId}";
                        if (existingSet.Contains(naturalKey))
                        {
                            result.Skipped++;
                            continue;
                        }

                        var template = assignment.PMTemplate!;
                        var intervalDays = GetIntervalDays(template);
                        var dueDaysOffset = dueDaysOffsets[scheduleIndex % dueDaysOffsets.Length];

                        var schedule = new PMSchedule
                        {
                            Name = $"{template.Code} - {site?.Name ?? "Site"}",
                            Description = $"Auto-generated from PMTemplateAsset: {template.Name} at {company?.Name}/{site?.Name}",
                            PMTemplateId = template.Id,
                            Active = true,
                            CadenceType = PMCadenceType.IntervalDays,
                            IntervalDays = intervalDays,
                            StartDateUtc = DateTime.UtcNow.Date.AddDays(-14),
                            NextDueDateUtc = DateTime.UtcNow.Date.AddDays(dueDaysOffset),
                            TimeZoneId = "America/New_York",
                            TenantId = tenant.Id,
                            CompanyId = companyId,
                            SiteId = siteId,
                            CreatedBy = "DemoPackV2Seed"
                        };

                        _context.PMSchedules.Add(schedule);
                        existingSet.Add(naturalKey);
                        result.Inserted++;
                        scheduleIndex++;
                    }
                }

                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("PMSchedules seed: {Inserted} inserted, {Skipped} skipped across {Sites} sites",
                    result.Inserted, result.Skipped, grouped.Count);
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(ex.Message);
                _logger.LogError(ex, "PMSchedules seed error");
            }
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        private async Task FallbackSeedFromTemplatesAsync(SeedStepResult result, int tenantId, CancellationToken ct)
        {
            var companies = await _context.Companies.ToListAsync(ct);
            if (!companies.Any())
            {
                result.Errors.Add("No companies found - cannot seed PMSchedules");
                result.Failed++;
                return;
            }

            var sites = await _context.Sites.ToListAsync(ct);
            var sitesByCompany = sites.GroupBy(s => s.CompanyId).ToDictionary(g => g.Key, g => g.ToList());

            var templates = await _context.PMTemplates
                .Where(t => t.IsActive && t.Code.StartsWith("PM-"))
                .OrderBy(t => t.Code)
                .Take(5)
                .ToListAsync(ct);

            if (!templates.Any())
            {
                result.Errors.Add("No active PM templates found - cannot seed PMSchedules");
                result.Failed++;
                return;
            }

            var existingKeys = await _context.PMSchedules
                .Select(s => new { s.CompanyId, s.SiteId, s.PMTemplateId })
                .ToListAsync(ct);
            var existingSet = existingKeys.Select(k => $"{k.CompanyId}|{k.SiteId}|{k.PMTemplateId}").ToHashSet();

            var dueDaysOffsets = new[] { -5, -2, 0, 3, 7, 14, 21, 30, 45 };
            var scheduleIndex = 0;

            foreach (var company in companies)
            {
                if (!sitesByCompany.TryGetValue(company.Id, out var companySites) || !companySites.Any())
                    continue;

                foreach (var site in companySites.Take(3))
                {
                    foreach (var template in templates.Take(3))
                    {
                        var naturalKey = $"{company.Id}|{site.Id}|{template.Id}";
                        if (existingSet.Contains(naturalKey))
                        {
                            result.Skipped++;
                            continue;
                        }

                        var intervalDays = GetIntervalDays(template);
                        var dueDaysOffset = dueDaysOffsets[scheduleIndex % dueDaysOffsets.Length];

                        var schedule = new PMSchedule
                        {
                            Name = $"{template.Code} - {site.Name}",
                            Description = $"Fallback schedule for {template.Name} at {company.Name}/{site.Name}",
                            PMTemplateId = template.Id,
                            Active = true,
                            CadenceType = PMCadenceType.IntervalDays,
                            IntervalDays = intervalDays,
                            StartDateUtc = DateTime.UtcNow.Date.AddDays(-14),
                            NextDueDateUtc = DateTime.UtcNow.Date.AddDays(dueDaysOffset),
                            TimeZoneId = "America/New_York",
                            TenantId = tenantId,
                            CompanyId = company.Id,
                            SiteId = site.Id,
                            CreatedBy = "DemoPackV2Seed-Fallback"
                        };

                        _context.PMSchedules.Add(schedule);
                        existingSet.Add(naturalKey);
                        result.Inserted++;
                        scheduleIndex++;
                    }
                }
            }

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("PMSchedules fallback seed: {Inserted} inserted, {Skipped} skipped", result.Inserted, result.Skipped);
        }

        private static int GetIntervalDays(PMTemplate template)
        {
            if (template.Code.Contains("DAILY")) return 1;
            if (template.Code.Contains("WEEKLY")) return 7;
            if (template.Code.Contains("MONTHLY")) return 30;
            if (template.Code.Contains("QUARTERLY")) return 90;
            return template.CalendarIntervalValue * (template.CalendarInterval switch
            {
                RecurrenceType.Daily => 1,
                RecurrenceType.Weekly => 7,
                RecurrenceType.Monthly => 30,
                RecurrenceType.Quarterly => 90,
                RecurrenceType.Annually => 365,
                _ => 30
            });
        }

        public async Task<PreviewStepResult> PreviewAsync(CancellationToken ct = default)
        {
            var result = new PreviewStepResult { StepName = StepName, DomainName = DomainName };

            var assignments = await _context.Set<PMTemplateAsset>()
                .Include(a => a.Asset)
                .Include(a => a.PMTemplate)
                .Where(a => a.IsActive && a.PMTemplate != null && a.PMTemplate.IsActive)
                .Where(a => a.Asset != null && a.Asset.SiteId != null)
                .ToListAsync(ct);

            var existingKeys = await _context.PMSchedules
                .Select(s => new { s.CompanyId, s.SiteId, s.PMTemplateId })
                .ToListAsync(ct);
            var existingSet = existingKeys.Select(k => $"{k.CompanyId}|{k.SiteId}|{k.PMTemplateId}").ToHashSet();

            if (assignments.Any())
            {
                var grouped = assignments
                    .Where(a => a.Asset?.Site != null)
                    .GroupBy(a => new { CompanyId = a.Asset!.Site!.CompanyId, SiteId = a.Asset!.SiteId });

                foreach (var siteGroup in grouped)
                {
                    var selectedTemplates = siteGroup
                        .GroupBy(a => a.PMTemplateId)
                        .Take(MaxSchedulesPerSite);

                    foreach (var templateGroup in selectedTemplates)
                    {
                        result.TotalInSeedData++;
                        var key = $"{siteGroup.Key.CompanyId}|{siteGroup.Key.SiteId}|{templateGroup.Key}";
                        if (existingSet.Contains(key))
                            result.WouldSkip++;
                        else
                            result.WouldCreate++;
                    }
                }
            }
            else
            {
                var companies = await _context.Companies.ToListAsync(ct);
                var allSites = await _context.Sites.ToListAsync(ct);
                var sitesByCompany = allSites.GroupBy(s => s.CompanyId).ToDictionary(g => g.Key, g => g.ToList());
                var templates = await _context.PMTemplates.Where(t => t.IsActive && t.Code.StartsWith("PM-")).Take(5).ToListAsync(ct);

                foreach (var company in companies)
                {
                    if (!sitesByCompany.TryGetValue(company.Id, out var companySites)) continue;

                    foreach (var site in companySites.Take(3))
                    {
                        foreach (var template in templates.Take(3))
                        {
                            result.TotalInSeedData++;
                            var key = $"{company.Id}|{site.Id}|{template.Id}";
                            if (existingSet.Contains(key))
                                result.WouldSkip++;
                            else
                                result.WouldCreate++;
                        }
                    }
                }
            }

            return result;
        }
    }
    #endregion
}
