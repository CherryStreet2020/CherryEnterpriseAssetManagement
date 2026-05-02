using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class SeedDataModel : PageModel
    {
        private readonly AppDbContext _context;
        private static readonly Random _random = new Random();

        public SeedDataModel(AppDbContext context)
        {
            _context = context;
        }

        public Dictionary<string, int> TableCounts { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadTableCounts();
        }

        private async Task LoadTableCounts()
        {
            TableCounts = new Dictionary<string, int>
            {
                ["Assets"] = await _context.Assets.CountAsync(),
                ["MaintenanceEvents"] = await _context.MaintenanceEvents.CountAsync(),
                ["Vendors"] = await _context.Vendors.CountAsync(),
                ["Locations"] = await _context.Locations.CountAsync(),
                ["Technicians"] = await _context.Technicians.CountAsync(),
                ["Companies"] = await _context.Companies.CountAsync(),
                ["ItemCategories"] = await _context.ItemCategories.CountAsync(),
                ["Items"] = await _context.Items.CountAsync(),
                ["ItemInventories"] = await _context.ItemInventories2.CountAsync(),
                ["ItemTransactions"] = await _context.ItemTransactions.CountAsync(),
                ["ItemVendors"] = await _context.ItemVendors.CountAsync(),
                ["Kits"] = await _context.Kits.CountAsync(),
                ["ReorderAlerts"] = await _context.ReorderAlerts.CountAsync(),
                ["KitItems"] = await _context.KitItems.CountAsync(),
                ["WorkOrderParts"] = await _context.WorkOrderParts.CountAsync(),
                ["PurchaseRequisitions"] = await _context.PurchaseRequisitions.CountAsync(),
                ["RequisitionLines"] = await _context.PurchaseRequisitionLines.CountAsync()
            };
        }

        public async Task<IActionResult> OnPostSeedItemCategoriesAsync()
        {
            try
            {
                if (await _context.ItemCategories.AnyAsync())
                {
                    TempData["Error"] = "Item Categories already exist. Clear data first.";
                    return RedirectToPage();
                }

                var categories = new List<ItemCategory>
                {
                    new() { Code = "BEAR", Name = "Bearings", Description = "Ball and roller bearings", SortOrder = 1 },
                    new() { Code = "FILT", Name = "Filters", Description = "Air, oil, and hydraulic filters", SortOrder = 2 },
                    new() { Code = "BELT", Name = "Belts & Drives", Description = "V-belts, timing belts, chains", SortOrder = 3 },
                    new() { Code = "SEAL", Name = "Seals & Gaskets", Description = "O-rings, seals, gaskets", SortOrder = 4 },
                    new() { Code = "ELEC", Name = "Electrical", Description = "Motors, switches, sensors", SortOrder = 5 },
                    new() { Code = "LUBR", Name = "Lubricants", Description = "Oils, greases, lubricants", SortOrder = 6 },
                    new() { Code = "TOOL", Name = "Tools", Description = "Hand and power tools", SortOrder = 7 },
                    new() { Code = "FAST", Name = "Fasteners", Description = "Bolts, nuts, screws, washers", SortOrder = 8 }
                };

                _context.ItemCategories.AddRange(categories);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Created {categories.Count} item categories.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSeedItemsAsync()
        {
            try
            {
                if (await _context.Items.AnyAsync())
                {
                    TempData["Error"] = "Items already exist. Clear data first.";
                    return RedirectToPage();
                }

                var categories = await _context.ItemCategories.ToListAsync();
                var vendors = await _context.Vendors.Take(5).ToListAsync();
                var locations = await _context.Locations.Take(5).ToListAsync();
                var company = await _context.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();

                if (!categories.Any())
                {
                    TempData["Error"] = "Seed Item Categories first (Step 1).";
                    return RedirectToPage();
                }

                var items = new List<Item>();
                var partData = GetPartData();

                foreach (var part in partData)
                {
                    var category = categories.FirstOrDefault(c => c.Code == part.CategoryCode);
                    var vendor = vendors.Any() ? vendors[_random.Next(vendors.Count)] : null;

                    items.Add(new Item
                    {
                        PartNumber = part.PartNumber,
                        Description = part.Description,
                        Type = part.Type,
                        CategoryId = category?.Id,
                        Status = ItemStatus.Active,
                        UOM = part.UOM,
                        StandardCost = part.Cost,
                        AverageCost = part.Cost,
                        ReorderPoint = part.ReorderPoint,
                        ReorderQuantity = part.ReorderQty,
                        MinQuantity = part.MinQty,
                        MaxQuantity = part.MaxQty,
                        LeadTimeDays = _random.Next(3, 21),
                        PrimaryVendorId = vendor?.Id,
                        IsCriticalSpare = part.IsCritical,
                        BarcodeType = BarcodeType.Code128,
                        Barcode = $"BC{part.PartNumber}",
                        ABCClass = part.ABCClass,
                        CompanyId = company?.Id,
                        CreatedBy = "DataSeed"
                    });
                }

                _context.Items.AddRange(items);
                await _context.SaveChangesAsync();

                if (vendors.Any())
                {
                    var itemVendors = new List<ItemVendor>();
                    foreach (var item in items)
                    {
                        var vendor = vendors[_random.Next(vendors.Count)];
                        itemVendors.Add(new ItemVendor
                        {
                            ItemId = item.Id,
                            VendorId = vendor.Id,
                            VendorPartNumber = $"V-{item.PartNumber}",
                            UnitPrice = item.StandardCost * (decimal)(0.9 + _random.NextDouble() * 0.2),
                            LeadTimeDays = _random.Next(5, 15),
                            IsPreferred = true
                        });
                    }
                    _context.ItemVendors.AddRange(itemVendors);
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = $"Created {items.Count} items with vendor links.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSeedInventoryAsync()
        {
            try
            {
                if (await _context.ItemInventories2.AnyAsync())
                {
                    TempData["Error"] = "Inventory records already exist. Clear data first.";
                    return RedirectToPage();
                }

                var items = await _context.Items.ToListAsync();
                var locations = await _context.Locations.Take(5).ToListAsync();
                var company = await _context.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();

                if (!items.Any())
                {
                    TempData["Error"] = "Seed Items first (Step 2).";
                    return RedirectToPage();
                }

                var inventories = new List<ItemInventory>();
                var transactions = new List<ItemTransaction>();
                int txnNum = 1;

                foreach (var item in items)
                {
                    var location = locations.Any() ? locations[_random.Next(locations.Count)] : null;
                    var bin = $"A{_random.Next(1,10)}-{_random.Next(1,20):D2}";
                    
                    var receiptQty = (decimal)_random.Next(20, 100);
                    var receiptDate = DateTime.UtcNow.AddDays(-_random.Next(30, 90));
                    
                    transactions.Add(new ItemTransaction
                    {
                        TransactionNumber = $"RCV-{txnNum++:D5}",
                        ItemId = item.Id,
                        Type = TransactionType.Receipt,
                        Quantity = receiptQty,
                        UnitCost = item.StandardCost,
                        ToLocationId = location?.Id,
                        ToBin = bin,
                        ReferenceType = "PO",
                        ReferenceNumber = $"PO-2024-{_random.Next(1000,9999)}",
                        TransactedBy = "Receiving",
                        TransactionDate = receiptDate,
                        Notes = "Initial stock receipt",
                        CompanyId = company?.Id
                    });
                    
                    var needsReorder = _random.NextDouble() < 0.3;
                    var qtyOnHand = needsReorder 
                        ? item.ReorderPoint * (decimal)(_random.NextDouble() * 0.8)
                        : receiptQty;

                    inventories.Add(new ItemInventory
                    {
                        ItemId = item.Id,
                        LocationId = location?.Id,
                        Warehouse = location?.Name ?? "Main Warehouse",
                        Bin = bin,
                        QuantityOnHand = qtyOnHand,
                        QuantityReserved = qtyOnHand > 5 ? (decimal)_random.Next(0, 3) : 0,
                        QuantityOnOrder = needsReorder ? item.ReorderQuantity : 0,
                        LastReceiptDate = receiptDate,
                        LastCountDate = DateTime.UtcNow.AddDays(-_random.Next(1, 30)),
                        CompanyId = company?.Id
                    });
                }

                _context.ItemTransactions.AddRange(transactions);
                _context.ItemInventories2.AddRange(inventories);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Created {inventories.Count} inventory records with {transactions.Count} receipt transactions.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSeedKitsAsync()
        {
            try
            {
                if (await _context.Kits.AnyAsync())
                {
                    TempData["Error"] = "Kits already exist. Clear data first.";
                    return RedirectToPage();
                }

                var items = await _context.Items.ToListAsync();
                var categories = await _context.ItemCategories.ToListAsync();
                var company = await _context.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();

                if (items.Count < 5)
                {
                    TempData["Error"] = "Need at least 5 items to create kits. Seed Items first (Step 2).";
                    return RedirectToPage();
                }

                var kits = new List<Kit>
                {
                    new() { KitNumber = "KIT-001", Name = "Conveyor Maintenance Kit", Description = "Standard parts for conveyor belt maintenance", CompanyId = company?.Id },
                    new() { KitNumber = "KIT-002", Name = "Pump Rebuild Kit", Description = "Complete pump overhaul parts", CompanyId = company?.Id },
                    new() { KitNumber = "KIT-003", Name = "Motor Service Kit", Description = "Electric motor maintenance parts", CompanyId = company?.Id },
                    new() { KitNumber = "KIT-004", Name = "Hydraulic Service Kit", Description = "Hydraulic system maintenance", CompanyId = company?.Id },
                    new() { KitNumber = "KIT-005", Name = "Annual PM Kit", Description = "Standard annual preventive maintenance parts", CompanyId = company?.Id }
                };

                _context.Kits.AddRange(kits);
                await _context.SaveChangesAsync();

                var kitItems = new List<KitItem>();
                foreach (var kit in kits)
                {
                    var selectedItems = items.OrderBy(_ => _random.Next()).Take(_random.Next(3, 8)).ToList();
                    int seq = 1;
                    decimal totalCost = 0;

                    foreach (var item in selectedItems)
                    {
                        var qty = (decimal)_random.Next(1, 4);
                        kitItems.Add(new KitItem
                        {
                            KitId = kit.Id,
                            ItemId = item.Id,
                            Quantity = qty,
                            Sequence = seq++
                        });
                        totalCost += item.StandardCost * qty;
                    }

                    kit.TotalCost = totalCost;
                }

                _context.KitItems.AddRange(kitItems);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Created {kits.Count} kits with {kitItems.Count} items.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSeedWorkOrderPartsAsync()
        {
            try
            {
                if (await _context.WorkOrderParts.AnyAsync())
                {
                    TempData["Error"] = "Work Order Parts already exist. Clear data first.";
                    return RedirectToPage();
                }

                var events = await _context.MaintenanceEvents.Take(20).ToListAsync();
                var items = await _context.Items.ToListAsync();
                var locations = await _context.Locations.ToListAsync();
                var inventories = await _context.ItemInventories2.ToListAsync();
                var company = await _context.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();

                if (!items.Any())
                {
                    TempData["Error"] = "Seed Items first (Step 2).";
                    return RedirectToPage();
                }

                if (!events.Any())
                {
                    TempData["Error"] = "No maintenance events found.";
                    return RedirectToPage();
                }

                var workOrderParts = new List<WorkOrderPart>();
                var issueTransactions = new List<ItemTransaction>();
                int txnNum = await _context.ItemTransactions.CountAsync() + 1;

                foreach (var evt in events)
                {
                    var numParts = _random.Next(1, 5);
                    var selectedItems = items.OrderBy(_ => _random.Next()).Take(numParts).ToList();
                    var location = locations.Any() ? locations[_random.Next(locations.Count)] : null;

                    foreach (var item in selectedItems)
                    {
                        var qtyPlanned = (decimal)_random.Next(1, 5);
                        var qtyUsed = evt.Status == MaintenanceStatus.Completed ? qtyPlanned : (decimal)_random.Next(0, (int)qtyPlanned + 1);

                        workOrderParts.Add(new WorkOrderPart
                        {
                            MaintenanceEventId = evt.Id,
                            ItemId = item.Id,
                            QuantityPlanned = qtyPlanned,
                            QuantityIssued = qtyUsed,
                            QuantityUsed = qtyUsed,
                            UnitCost = item.StandardCost,
                            IssuedFromLocationId = location?.Id,
                            IssuedBy = "Storeroom",
                            IssuedDate = evt.ScheduledDate
                        });

                        if (qtyUsed > 0)
                        {
                            issueTransactions.Add(new ItemTransaction
                            {
                                TransactionNumber = $"ISS-{txnNum++:D5}",
                                ItemId = item.Id,
                                Type = TransactionType.Issue,
                                Quantity = qtyUsed,
                                UnitCost = item.StandardCost,
                                FromLocationId = location?.Id,
                                ReferenceType = "WO",
                                ReferenceNumber = evt.WorkOrderNumber ?? $"WO-{evt.Id}",
                                WorkOrderId = evt.Id,
                                TransactedBy = "Storeroom",
                                TransactionDate = evt.ScheduledDate,
                                Notes = $"Parts issued for work order",
                                CompanyId = company?.Id
                            });

                            var inv = inventories.FirstOrDefault(i => i.ItemId == item.Id);
                            if (inv != null)
                            {
                                inv.QuantityOnHand = Math.Max(0, inv.QuantityOnHand - qtyUsed);
                                inv.LastIssueDate = evt.ScheduledDate;
                            }
                        }
                    }
                }

                _context.WorkOrderParts.AddRange(workOrderParts);
                _context.ItemTransactions.AddRange(issueTransactions);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Created {workOrderParts.Count} work order parts with {issueTransactions.Count} issue transactions.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostGenerateReorderAlertsAsync()
        {
            try
            {
                var items = await _context.Items.ToListAsync();
                var inventories = await _context.ItemInventories2.ToListAsync();
                var company = await _context.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();

                if (!items.Any() || !inventories.Any())
                {
                    TempData["Error"] = "Seed Items and Inventory first.";
                    return RedirectToPage();
                }

                var alerts = new List<ReorderAlert>();
                
                foreach (var item in items)
                {
                    var inv = inventories.FirstOrDefault(i => i.ItemId == item.Id);
                    if (inv == null) continue;

                    if (inv.QuantityOnHand <= item.ReorderPoint)
                    {
                        var alertType = inv.QuantityOnHand <= 0 
                            ? RequisitionSource.StockoutAlert 
                            : (inv.QuantityOnHand <= item.MinQuantity ? RequisitionSource.SafetyStock : RequisitionSource.AutoReorder);

                        alerts.Add(new ReorderAlert
                        {
                            ItemId = item.Id,
                            AlertType = alertType,
                            CurrentStock = inv.QuantityOnHand,
                            ReorderPoint = item.ReorderPoint,
                            SafetyStock = item.MinQuantity,
                            SuggestedQuantity = item.ReorderQuantity,
                            IsAcknowledged = false,
                            CompanyId = company?.Id
                        });
                    }
                }

                if (alerts.Any())
                {
                    await _context.ReorderAlerts.Where(a => !a.IsAcknowledged).ExecuteDeleteAsync();
                    _context.ReorderAlerts.AddRange(alerts);
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = $"Generated {alerts.Count} reorder alerts for low-stock items.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSeedRequisitionsAsync()
        {
            try
            {
                if (await _context.PurchaseRequisitions.AnyAsync())
                {
                    TempData["Error"] = "Requisitions already exist. Clear data first.";
                    return RedirectToPage();
                }

                var items = await _context.Items.Include(i => i.PrimaryVendor).ToListAsync();
                var company = await _context.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();

                if (!items.Any())
                {
                    TempData["Error"] = "Seed Items first (Step 2).";
                    return RedirectToPage();
                }

                var requisitions = new List<PurchaseRequisition>();
                var lines = new List<PurchaseRequisitionLine>();

                for (int i = 1; i <= 10; i++)
                {
                    var status = i <= 3 ? RequisitionStatus.Draft : (i <= 6 ? RequisitionStatus.Pending : RequisitionStatus.Approved);
                    var req = new PurchaseRequisition
                    {
                        RequisitionNumber = $"REQ-2024-{i:D4}",
                        Justification = $"Stock replenishment requisition {i}",
                        Status = status,
                        Priority = (RequisitionPriority)_random.Next(0, 3),
                        Requestor = "System",
                        RequisitionDate = DateTime.UtcNow.AddDays(-_random.Next(1, 30)),
                        RequiredDate = DateTime.UtcNow.AddDays(_random.Next(7, 30)),
                        CompanyId = company?.Id,
                        Notes = "Auto-generated test requisition"
                    };
                    requisitions.Add(req);
                }

                _context.PurchaseRequisitions.AddRange(requisitions);
                await _context.SaveChangesAsync();

                foreach (var req in requisitions)
                {
                    var numLines = _random.Next(2, 6);
                    var selectedItems = items.OrderBy(_ => _random.Next()).Take(numLines).ToList();
                    int lineNum = 1;
                    decimal totalAmount = 0;

                    foreach (var item in selectedItems)
                    {
                        var qty = (decimal)_random.Next(5, 50);
                        var unitPrice = item.StandardCost;
                        var lineAmount = qty * unitPrice;
                        totalAmount += lineAmount;

                        lines.Add(new PurchaseRequisitionLine
                        {
                            RequisitionId = req.Id,
                            LineNumber = lineNum++,
                            ItemId = item.Id,
                            Description = item.Description,
                            Quantity = qty,
                            UOM = item.StockUOM,
                            UnitPrice = unitPrice,
                            SuggestedVendorId = item.PrimaryVendorId
                        });
                    }

                    req.TotalAmount = totalAmount;
                }

                _context.PurchaseRequisitionLines.AddRange(lines);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Created {requisitions.Count} purchase requisitions with {lines.Count} lines.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSeedAllAsync()
        {
            try
            {
                await OnPostSeedItemCategoriesAsync();
                await OnPostSeedItemsAsync();
                await OnPostSeedInventoryAsync();
                await OnPostSeedKitsAsync();
                await OnPostSeedWorkOrderPartsAsync();
                await OnPostGenerateReorderAlertsAsync();
                await OnPostSeedRequisitionsAsync();

                TempData["Success"] = "All test data seeded with transactions successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error during seeding: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostClearNewDataAsync()
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"PurchaseRequisitionLines\"");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"PurchaseRequisitions\"");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"ReorderAlerts\"");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"WorkOrderParts\"");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"ItemTransactions\"");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"KitItems\"");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"Kits\"");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"ItemInventories2\"");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"ItemVendors\"");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"Items\"");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"ItemCategories\"");

                TempData["Success"] = "Cleared all test data (Items, Inventory, Transactions, Alerts, Kits, WorkOrderParts, Requisitions).";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        private List<PartDefinition> GetPartData()
        {
            return new List<PartDefinition>
            {
                new("6205-2RS", "Ball Bearing 6205-2RS 25x52x15mm", "BEAR", ItemType.Part, 12.50m, 10, 25, 5, 50, true, ABCClassification.A),
                new("6206-2RS", "Ball Bearing 6206-2RS 30x62x16mm", "BEAR", ItemType.Part, 15.75m, 8, 20, 4, 40, true, ABCClassification.A),
                new("6207-2RS", "Ball Bearing 6207-2RS 35x72x17mm", "BEAR", ItemType.Part, 18.25m, 6, 15, 3, 30, false, ABCClassification.B),
                new("6208-2RS", "Ball Bearing 6208-2RS 40x80x18mm", "BEAR", ItemType.Part, 22.00m, 5, 12, 2, 25, false, ABCClassification.B),
                new("22205", "Spherical Roller Bearing 22205", "BEAR", ItemType.Part, 45.00m, 3, 8, 1, 15, true, ABCClassification.A),
                new("AIR-FILT-01", "Panel Air Filter 20x25x4", "FILT", ItemType.Consumable, 8.50m, 20, 50, 10, 100, false, ABCClassification.C),
                new("AIR-FILT-02", "Panel Air Filter 24x24x2", "FILT", ItemType.Consumable, 6.25m, 25, 60, 12, 120, false, ABCClassification.C),
                new("OIL-FILT-10", "Hydraulic Oil Filter Element", "FILT", ItemType.Consumable, 35.00m, 5, 15, 2, 30, true, ABCClassification.B),
                new("OIL-FILT-15", "Spin-On Oil Filter", "FILT", ItemType.Consumable, 12.00m, 15, 40, 8, 80, false, ABCClassification.C),
                new("FUEL-FILT-5", "Fuel Filter Element", "FILT", ItemType.Consumable, 18.50m, 8, 20, 4, 40, false, ABCClassification.B),
                new("VB-A68", "V-Belt A68", "BELT", ItemType.Part, 14.25m, 6, 15, 3, 30, false, ABCClassification.B),
                new("VB-B75", "V-Belt B75", "BELT", ItemType.Part, 18.50m, 5, 12, 2, 25, false, ABCClassification.B),
                new("TB-5M-15", "Timing Belt 5M 15mm Wide", "BELT", ItemType.Part, 45.00m, 2, 6, 1, 12, true, ABCClassification.A),
                new("CHAIN-40", "Roller Chain #40 10ft", "BELT", ItemType.Part, 28.00m, 3, 8, 1, 15, false, ABCClassification.B),
                new("CHAIN-60", "Roller Chain #60 10ft", "BELT", ItemType.Part, 42.00m, 2, 5, 1, 10, false, ABCClassification.B),
                new("OR-214", "O-Ring 214 Buna-N", "SEAL", ItemType.Part, 1.25m, 50, 150, 25, 300, false, ABCClassification.C),
                new("OR-228", "O-Ring 228 Buna-N", "SEAL", ItemType.Part, 1.50m, 40, 120, 20, 250, false, ABCClassification.C),
                new("OR-330", "O-Ring 330 Viton", "SEAL", ItemType.Part, 4.50m, 20, 60, 10, 120, false, ABCClassification.C),
                new("SEAL-25X42", "Oil Seal 25x42x7", "SEAL", ItemType.Part, 6.75m, 10, 25, 5, 50, false, ABCClassification.C),
                new("GASKET-SET-1", "Pump Gasket Set", "SEAL", ItemType.Part, 28.00m, 5, 12, 2, 25, true, ABCClassification.B),
                new("MTR-1HP", "Motor 1HP 1800RPM TEFC", "ELEC", ItemType.Part, 285.00m, 1, 3, 0, 5, true, ABCClassification.A),
                new("MTR-2HP", "Motor 2HP 1800RPM TEFC", "ELEC", ItemType.Part, 425.00m, 1, 2, 0, 4, true, ABCClassification.A),
                new("PROX-SENS", "Proximity Sensor 10-30VDC", "ELEC", ItemType.Part, 65.00m, 3, 8, 1, 15, true, ABCClassification.B),
                new("PHOTO-EYE", "Photo Eye Sensor", "ELEC", ItemType.Part, 85.00m, 2, 6, 1, 12, true, ABCClassification.B),
                new("RELAY-24V", "Ice Cube Relay 24VDC DPDT", "ELEC", ItemType.Part, 18.50m, 10, 25, 5, 50, false, ABCClassification.C),
                new("OIL-32", "Hydraulic Oil ISO 32 5gal", "LUBR", ItemType.Consumable, 45.00m, 5, 15, 2, 30, false, ABCClassification.B),
                new("OIL-68", "Hydraulic Oil ISO 68 5gal", "LUBR", ItemType.Consumable, 48.00m, 4, 12, 2, 25, false, ABCClassification.B),
                new("GREASE-EP2", "EP2 Grease Cartridge", "LUBR", ItemType.Consumable, 8.50m, 25, 75, 12, 150, false, ABCClassification.C),
                new("GEAR-OIL", "Gear Oil EP 220 1gal", "LUBR", ItemType.Consumable, 32.00m, 5, 15, 2, 30, false, ABCClassification.C),
                new("PENETRANT", "Penetrating Oil Spray 12oz", "LUBR", ItemType.Consumable, 6.25m, 20, 50, 10, 100, false, ABCClassification.C),
                new("WRENCH-SET", "Combination Wrench Set SAE", "TOOL", ItemType.Tool, 125.00m, 2, 5, 1, 10, false, ABCClassification.B),
                new("SOCKET-SET", "Socket Set 3/8\" Drive", "TOOL", ItemType.Tool, 85.00m, 2, 5, 1, 10, false, ABCClassification.B),
                new("TORQUE-WR", "Torque Wrench 3/8\" 10-80 ft-lb", "TOOL", ItemType.Tool, 145.00m, 1, 3, 0, 5, false, ABCClassification.B),
                new("MULTI-MTR", "Digital Multimeter", "TOOL", ItemType.Tool, 65.00m, 2, 5, 1, 10, false, ABCClassification.C),
                new("THERMO-GUN", "Infrared Thermometer", "TOOL", ItemType.Tool, 55.00m, 2, 5, 1, 10, false, ABCClassification.C),
                new("BOLT-M8X30", "Hex Bolt M8x30 Gr8.8 Zinc", "FAST", ItemType.Part, 0.45m, 100, 300, 50, 500, false, ABCClassification.C),
                new("BOLT-M10X40", "Hex Bolt M10x40 Gr8.8 Zinc", "FAST", ItemType.Part, 0.65m, 80, 250, 40, 400, false, ABCClassification.C),
                new("NUT-M8", "Hex Nut M8 Gr8 Zinc", "FAST", ItemType.Part, 0.12m, 200, 500, 100, 1000, false, ABCClassification.C),
                new("NUT-M10", "Hex Nut M10 Gr8 Zinc", "FAST", ItemType.Part, 0.15m, 150, 400, 75, 800, false, ABCClassification.C),
                new("WASHER-M8", "Flat Washer M8 Zinc", "FAST", ItemType.Part, 0.08m, 250, 600, 125, 1200, false, ABCClassification.C),
                new("WASHER-M10", "Flat Washer M10 Zinc", "FAST", ItemType.Part, 0.10m, 200, 500, 100, 1000, false, ABCClassification.C),
                new("LOCKWASH-M8", "Lock Washer M8 Zinc", "FAST", ItemType.Part, 0.10m, 200, 500, 100, 1000, false, ABCClassification.C),
                new("SETSCREW-M6", "Set Screw M6x10 Cup Point", "FAST", ItemType.Part, 0.25m, 100, 300, 50, 500, false, ABCClassification.C),
                new("ANCHOR-3/8", "Wedge Anchor 3/8x3", "FAST", ItemType.Part, 1.85m, 50, 150, 25, 300, false, ABCClassification.C),
                new("COTTER-3/32", "Cotter Pin 3/32x1", "FAST", ItemType.Part, 0.05m, 300, 800, 150, 1500, false, ABCClassification.C),
                new("SHAFT-COL-1", "Shaft Collar 1\" Bore", "BEAR", ItemType.Part, 8.50m, 15, 40, 8, 80, false, ABCClassification.C),
                new("PILLOW-1", "Pillow Block Bearing 1\" Bore", "BEAR", ItemType.Part, 35.00m, 4, 10, 2, 20, true, ABCClassification.B),
                new("COUPLING-1", "Jaw Coupling 1\" Bore", "BELT", ItemType.Part, 42.00m, 3, 8, 1, 15, false, ABCClassification.B),
                new("SPROCKET-40", "Sprocket #40 20T 5/8 Bore", "BELT", ItemType.Part, 18.50m, 4, 10, 2, 20, false, ABCClassification.C),
                new("CONTACTOR-30A", "Contactor 30A 3P 120V Coil", "ELEC", ItemType.Part, 85.00m, 2, 6, 1, 12, true, ABCClassification.B)
            };
        }

        private record PartDefinition(string PartNumber, string Description, string CategoryCode, ItemType Type, decimal Cost, int ReorderPoint, int ReorderQty, int MinQty, int MaxQty, bool IsCritical, ABCClassification ABCClass, UnitOfMeasure UOM = UnitOfMeasure.Each);
    }
}
