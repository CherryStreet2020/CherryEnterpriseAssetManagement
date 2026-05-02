using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public class AiAssistantService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _context;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public AiAssistantService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _httpClient = new HttpClient();
            
            _baseUrl = Environment.GetEnvironmentVariable("AI_INTEGRATIONS_OPENAI_BASE_URL") 
                ?? "https://api.openai.com/v1";
            _apiKey = Environment.GetEnvironmentVariable("AI_INTEGRATIONS_OPENAI_API_KEY") 
                ?? "";
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<string> GetAssetContextAsync()
        {
            var context = new StringBuilder();
            
            // ========== FIXED ASSETS SUMMARY ==========
            var totalAssets = await _context.Assets.CountAsync();
            var activeAssets = await _context.Assets.CountAsync(a => a.Status == AssetStatus.Active);
            var totalValue = await _context.Assets.SumAsync(a => a.AcquisitionCost);
            var totalAccumDepr = await _context.Assets.SumAsync(a => a.AccumulatedDepreciation);
            
            var locationSummary = await _context.Assets
                .Where(a => a.Status == AssetStatus.Active && a.LocationRef != null)
                .GroupBy(a => a.LocationRef!.Name)
                .Select(g => new { Location = g.Key, Count = g.Count(), Value = g.Sum(a => a.AcquisitionCost) })
                .ToListAsync();

            var fullyDepreciated = await _context.Assets
                .CountAsync(a => a.AccumulatedDepreciation >= a.AcquisitionCost && a.Status == AssetStatus.Active);

            var recentAcquisitions = await _context.Assets
                .Where(a => a.InServiceDate >= DateTime.UtcNow.AddMonths(-6))
                .CountAsync();

            var topAssets = await _context.Assets
                .OrderByDescending(a => a.AcquisitionCost)
                .Take(10)
                .Select(a => new { a.Id, a.AssetNumber, a.Description, a.AcquisitionCost, Location = a.LocationRef != null ? a.LocationRef.Name : "Unknown" })
                .ToListAsync();

            context.AppendLine("=== FIXED ASSETS DATABASE SUMMARY ===");
            context.AppendLine($"Total Assets: {totalAssets} [View All](/Assets)");
            context.AppendLine($"Active Assets: {activeAssets}");
            context.AppendLine($"Total Asset Value: ${totalValue:N0}");
            context.AppendLine($"Total Accumulated Depreciation: ${totalAccumDepr:N0}");
            context.AppendLine($"Net Book Value: ${(totalValue - totalAccumDepr):N0}");
            context.AppendLine($"Fully Depreciated Assets: {fullyDepreciated}");
            context.AppendLine($"Assets Acquired (Last 6 Months): {recentAcquisitions}");
            context.AppendLine();
            context.AppendLine("=== ASSETS BY LOCATION ===");
            foreach (var loc in locationSummary.OrderByDescending(l => l.Value))
            {
                context.AppendLine($"- {loc.Location ?? "Unknown"}: {loc.Count} assets, ${loc.Value:N0}");
            }
            context.AppendLine();
            context.AppendLine("=== TOP 10 HIGHEST VALUE ASSETS ===");
            foreach (var asset in topAssets)
            {
                context.AppendLine($"- [{asset.AssetNumber}](/Assets/Details/{asset.Id}): {asset.Description} - ${asset.AcquisitionCost:N0} at {asset.Location}");
            }
            context.AppendLine();

            // ========== MAINTENANCE STATUS ==========
            var maintenanceOverdue = await _context.MaintenanceEvents
                .CountAsync(m => m.ScheduledDate < DateTime.UtcNow && m.Status != MaintenanceStatus.Completed);

            var upcomingMaintenance = await _context.MaintenanceEvents
                .Where(m => m.ScheduledDate >= DateTime.UtcNow && m.ScheduledDate <= DateTime.UtcNow.AddDays(30))
                .CountAsync();

            var maintenanceByType = await _context.MaintenanceEvents
                .Where(m => m.Status != MaintenanceStatus.Completed)
                .GroupBy(m => m.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            context.AppendLine("=== MAINTENANCE STATUS ===");
            context.AppendLine($"Overdue Maintenance Events: {maintenanceOverdue} [View Maintenance](/Maintenance)");
            context.AppendLine($"Upcoming Maintenance (Next 30 Days): {upcomingMaintenance}");
            if (maintenanceByType.Any())
            {
                context.AppendLine("Open Maintenance by Type:");
                foreach (var mt in maintenanceByType)
                {
                    context.AppendLine($"  - {mt.Type}: {mt.Count}");
                }
            }
            context.AppendLine();

            // ========== CIP PROJECTS (CONSTRUCTION IN PROGRESS) ==========
            var totalProjects = await _context.CipProjects.CountAsync();
            var activeProjects = await _context.CipProjects.CountAsync(p => p.Status == CipProjectStatus.Active);
            var totalBudget = await _context.CipProjects.SumAsync(p => p.BudgetAmount);
            var totalSpent = await _context.CipProjects.SumAsync(p => p.TotalCosts);

            var overBudgetProjects = await _context.CipProjects
                .Where(p => p.TotalCosts > p.BudgetAmount && p.Status != CipProjectStatus.Cancelled)
                .Select(p => new { 
                    p.Id,
                    p.ProjectNumber, 
                    p.Name, 
                    p.BudgetAmount, 
                    p.TotalCosts,
                    OverBy = p.TotalCosts - p.BudgetAmount,
                    PercentOver = p.BudgetAmount > 0 ? ((p.TotalCosts - p.BudgetAmount) / p.BudgetAmount * 100) : 0
                })
                .ToListAsync();

            var projectsByStatus = await _context.CipProjects
                .GroupBy(p => p.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var allActiveProjects = await _context.CipProjects
                .Where(p => p.Status == CipProjectStatus.Active || p.Status == CipProjectStatus.Planned)
                .Select(p => new { p.Id, p.ProjectNumber, p.Name, p.BudgetAmount, p.TotalCosts, p.Status })
                .ToListAsync();

            context.AppendLine("=== CIP PROJECTS (CONSTRUCTION IN PROGRESS) ===");
            context.AppendLine($"Total Projects: {totalProjects} [View All](/CIP)");
            context.AppendLine($"Active Projects: {activeProjects}");
            context.AppendLine($"Total Budget (All Projects): ${totalBudget:N0}");
            context.AppendLine($"Total Spent (All Projects): ${totalSpent:N0}");
            context.AppendLine($"Overall Budget Utilization: {(totalBudget > 0 ? (totalSpent / totalBudget * 100) : 0):F1}%");
            context.AppendLine();
            context.AppendLine("Projects by Status:");
            foreach (var ps in projectsByStatus)
            {
                context.AppendLine($"  - {ps.Status}: {ps.Count}");
            }
            context.AppendLine();
            if (overBudgetProjects.Any())
            {
                context.AppendLine($"*** OVER BUDGET PROJECTS ({overBudgetProjects.Count}) ***");
                foreach (var p in overBudgetProjects.OrderByDescending(x => x.OverBy))
                {
                    context.AppendLine($"  - [{p.ProjectNumber}](/CIP/Details/{p.Id}): {p.Name}");
                    context.AppendLine($"    Budget: ${p.BudgetAmount:N0}, Spent: ${p.TotalCosts:N0}, Over by: ${p.OverBy:N0} ({p.PercentOver:F1}%)");
                }
                context.AppendLine();
            }
            else
            {
                context.AppendLine("No projects are over budget.");
                context.AppendLine();
            }
            if (allActiveProjects.Any())
            {
                context.AppendLine("Active/Planned Projects:");
                foreach (var p in allActiveProjects)
                {
                    var pct = p.BudgetAmount > 0 ? (p.TotalCosts / p.BudgetAmount * 100) : 0;
                    context.AppendLine($"  - [{p.ProjectNumber}](/CIP/Details/{p.Id}): {p.Name} [{p.Status}] - ${p.TotalCosts:N0} of ${p.BudgetAmount:N0} ({pct:F0}% used)");
                }
                context.AppendLine();
            }

            // ========== TECHNICIANS ==========
            var technicians = await _context.Technicians
                .Where(t => t.Active)
                .Select(t => new { t.Id, t.Name, t.Specialty, t.HourlyRate })
                .ToListAsync();

            var technicianWorkload = await _context.MaintenanceEvents
                .Where(m => m.TechnicianId != null && m.Status != MaintenanceStatus.Completed)
                .GroupBy(m => m.TechnicianId)
                .Select(g => new { TechnicianId = g.Key, OpenTasks = g.Count() })
                .ToListAsync();

            context.AppendLine("=== TECHNICIANS ===");
            context.AppendLine($"Active Technicians: {technicians.Count}");
            foreach (var t in technicians)
            {
                var workload = technicianWorkload.FirstOrDefault(w => w.TechnicianId == t.Id);
                context.AppendLine($"  - {t.Name}: {t.Specialty ?? "General"}, ${t.HourlyRate ?? 0:N0}/hr, {workload?.OpenTasks ?? 0} open tasks");
            }
            context.AppendLine();

            // ========== PROJECT MANAGERS ==========
            var projectManagers = await _context.ProjectManagers
                .Where(pm => pm.Active)
                .Select(pm => new { pm.Id, pm.Name, pm.Department })
                .ToListAsync();

            var pmWorkload = await _context.CipProjects
                .Where(p => p.ProjectManagerId != null && (p.Status == CipProjectStatus.Active || p.Status == CipProjectStatus.Planned))
                .GroupBy(p => p.ProjectManagerId)
                .Select(g => new { ProjectManagerId = g.Key, ActiveProjects = g.Count() })
                .ToListAsync();

            context.AppendLine("=== PROJECT MANAGERS ===");
            context.AppendLine($"Active Project Managers: {projectManagers.Count}");
            foreach (var pm in projectManagers)
            {
                var workload = pmWorkload.FirstOrDefault(w => w.ProjectManagerId == pm.Id);
                context.AppendLine($"  - {pm.Name}: {pm.Department ?? "General"}, {workload?.ActiveProjects ?? 0} active projects");
            }
            context.AppendLine();

            // ========== MANUFACTURERS ==========
            var manufacturers = await _context.Manufacturers
                .Where(m => m.Active)
                .Select(m => new { m.Id, m.Name, m.Country })
                .ToListAsync();

            var manufacturerAssets = await _context.Assets
                .Where(a => a.ManufacturerId != null)
                .GroupBy(a => a.ManufacturerId)
                .Select(g => new { ManufacturerId = g.Key, AssetCount = g.Count(), TotalValue = g.Sum(a => a.AcquisitionCost) })
                .ToListAsync();

            context.AppendLine("=== MANUFACTURERS ===");
            context.AppendLine($"Active Manufacturers: {manufacturers.Count}");
            var topMfgs = manufacturers
                .Select(mfg => {
                    var assetData = manufacturerAssets.FirstOrDefault(m => m.ManufacturerId == mfg.Id);
                    return new { mfg.Name, AssetCount = assetData?.AssetCount ?? 0, TotalValue = assetData?.TotalValue ?? 0 };
                })
                .OrderByDescending(m => m.TotalValue)
                .Take(15);
            foreach (var mfg in topMfgs)
            {
                context.AppendLine($"  - {mfg.Name}: {mfg.AssetCount} assets, ${mfg.TotalValue:N0} total value");
            }
            context.AppendLine();

            // ========== INVENTORY ==========
            var inventoryLists = await _context.InventoryLists
                .Select(il => new { il.Name, il.Status, il.TotalAssets, il.ScannedAssets, il.MissingAssets, il.FoundAssets })
                .ToListAsync();

            var activeInventories = inventoryLists.Count(il => il.Status == Models.InventoryStatus.InProgress);
            var completedInventories = inventoryLists.Count(il => il.Status == Models.InventoryStatus.Completed);
            var totalScanned = inventoryLists.Sum(il => il.ScannedAssets);
            var totalMissing = inventoryLists.Sum(il => il.MissingAssets);

            context.AppendLine("=== INVENTORY ===");
            context.AppendLine($"Total Inventory Lists: {inventoryLists.Count}");
            context.AppendLine($"In Progress: {activeInventories}");
            context.AppendLine($"Completed: {completedInventories}");
            context.AppendLine($"Total Assets Scanned (All Lists): {totalScanned}");
            context.AppendLine($"Total Missing Assets: {totalMissing}");
            if (inventoryLists.Any())
            {
                context.AppendLine("Recent Inventory Lists:");
                foreach (var il in inventoryLists.Take(5))
                {
                    context.AppendLine($"  - {il.Name} [{il.Status}]: {il.ScannedAssets}/{il.TotalAssets} scanned, {il.MissingAssets} missing");
                }
            }
            context.AppendLine();

            // ========== PARTS & SPARE INVENTORY ==========
            var totalItems = await _context.Items.CountAsync();
            var activeItems = await _context.Items.CountAsync(i => i.Status == ItemStatus.Active);
            
            var itemInventorySummary = await _context.ItemInventories2
                .GroupBy(ii => ii.ItemId)
                .Select(g => new { ItemId = g.Key, QtyOnHand = g.Sum(ii => ii.QuantityOnHand) })
                .ToListAsync();
            
            var itemsWithQty = await _context.Items
                .Where(i => i.Status == ItemStatus.Active)
                .Select(i => new { i.Id, i.StandardCost })
                .ToListAsync();
            
            var totalItemValue = itemsWithQty.Sum(i => {
                var inv = itemInventorySummary.FirstOrDefault(x => x.ItemId == i.Id);
                return i.StandardCost * (inv?.QtyOnHand ?? 0);
            });
            
            var lowStockItems = await _context.Items
                .Where(i => i.Status == ItemStatus.Active && i.ReorderPoint > 0)
                .Select(i => new { 
                    i.Id, 
                    i.PartNumber, 
                    i.Description, 
                    i.ReorderPoint, 
                    i.ReorderQuantity
                })
                .ToListAsync();
            
            var lowStockWithQty = lowStockItems
                .Select(i => {
                    var inv = itemInventorySummary.FirstOrDefault(x => x.ItemId == i.Id);
                    var qtyOnHand = inv?.QtyOnHand ?? 0;
                    return new { 
                        i.Id, 
                        i.PartNumber, 
                        i.Description, 
                        CurrentQty = qtyOnHand, 
                        i.ReorderPoint, 
                        i.ReorderQuantity,
                        Shortage = i.ReorderPoint - qtyOnHand
                    };
                })
                .Where(i => i.CurrentQty <= i.ReorderPoint)
                .OrderByDescending(i => i.Shortage)
                .Take(10)
                .ToList();

            var criticalItems = await _context.Items
                .Where(i => i.Status == ItemStatus.Active && i.ABCClass == ABCClassification.A)
                .CountAsync();

            var autoReorderItems = await _context.Items
                .CountAsync(i => i.AutoReorderEnabled);

            var itemsByCategory = await _context.Items
                .Where(i => i.Status == ItemStatus.Active && i.Category != null)
                .GroupBy(i => i.Category!.Name)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(10)
                .ToListAsync();

            var reorderAlerts = await _context.ReorderAlerts
                .Where(r => !r.IsAcknowledged)
                .CountAsync();

            var pendingRequisitions = await _context.PurchaseRequisitions
                .CountAsync(r => r.Status == RequisitionStatus.Pending);

            context.AppendLine("=== PARTS & SPARE INVENTORY ===");
            context.AppendLine($"Total Items in Catalog: {totalItems} [View Items](/Admin/Items)");
            context.AppendLine($"Active Items: {activeItems}");
            context.AppendLine($"Critical (Class A) Items: {criticalItems}");
            context.AppendLine($"Auto-Reorder Enabled: {autoReorderItems}");
            context.AppendLine($"Estimated Inventory Value: ${totalItemValue:N0}");
            context.AppendLine($"Active Reorder Alerts: {reorderAlerts} [View Requisitions](/Admin/Requisitions)");
            context.AppendLine($"Pending Purchase Requisitions: {pendingRequisitions}");
            context.AppendLine();

            if (lowStockWithQty.Any())
            {
                context.AppendLine("=== LOW STOCK ALERTS (Need Reorder) ===");
                foreach (var item in lowStockWithQty)
                {
                    context.AppendLine($"  - [{item.PartNumber}](/Admin/Items): {item.Description}");
                    context.AppendLine($"    Current: {item.CurrentQty:N0}, Reorder Point: {item.ReorderPoint:N0}, Suggested Order: {item.ReorderQuantity:N0}");
                }
                context.AppendLine();
            }

            if (itemsByCategory.Any())
            {
                context.AppendLine("Items by Category:");
                foreach (var cat in itemsByCategory)
                {
                    context.AppendLine($"  - {cat.Category ?? "Uncategorized"}: {cat.Count} items");
                }
                context.AppendLine();
            }

            // ========== COMPANIES ==========
            var companies = await _context.Companies.Select(c => new { c.Name, c.Currency }).ToListAsync();
            context.AppendLine("=== COMPANIES ===");
            foreach (var c in companies)
            {
                context.AppendLine($"  - {c.Name} ({c.Currency})");
            }
            context.AppendLine();

            // ========== EXCHANGE RATES ==========
            var exchangeRates = await _context.ExchangeRates
                .OrderByDescending(e => e.EffectiveDate)
                .Take(10)
                .Select(e => new { e.FromCurrency, e.ToCurrency, e.Rate, e.EffectiveDate })
                .ToListAsync();

            if (exchangeRates.Any())
            {
                context.AppendLine("=== RECENT EXCHANGE RATES ===");
                foreach (var er in exchangeRates)
                {
                    context.AppendLine($"  - {er.FromCurrency}/{er.ToCurrency}: {er.Rate:F4} (as of {er.EffectiveDate:yyyy-MM-dd})");
                }
                context.AppendLine();
            }

            return context.ToString();
        }

        public async Task<string> AskQuestionAsync(string question, string? conversationHistory = null)
        {
            var assetContext = await GetAssetContextAsync();
            
            var systemPrompt = @"You are CherryAI, an intelligent enterprise assistant for the 'CherryAI Fixed Assets' management system. You have access to summary data from the company's operational database and can answer questions about:

1. FIXED ASSETS - Portfolio value, depreciation, locations, net book values, top 10 assets by value
2. MAINTENANCE - Overdue items, upcoming schedules, open work by technician
3. CIP PROJECTS - Capital projects, budgets vs spending, over-budget project alerts, project status
4. PERSONNEL - Technicians (specialties, hourly rates, workload), Project Managers (departments, active projects)
5. MANUFACTURERS - Top 15 equipment manufacturers by asset value
6. INVENTORY - Physical inventory lists, scan progress, missing asset counts
7. COMPANIES - Multi-company setup, currencies
8. EXCHANGE RATES - Recent currency conversion rates

You have access to the following REAL-TIME summary data from the database:

" + assetContext + @"

Guidelines:
- Answer questions directly using the data provided above
- Be specific with numbers, names, and details - you have real data
- When asked about over-budget projects, list them with specific amounts
- Highlight critical issues (overdue maintenance, over-budget projects)
- Provide actionable insights and recommendations when appropriate
- Format currency with dollar signs and commas
- Use tables or bullet points for clarity when listing multiple items
- Be professional but conversational

IMPORTANT - Hyperlinks:
- The data includes markdown links like [AssetNumber](/Assets/Details/123) - USE THESE in your responses
- When referencing specific assets or projects, include the clickable link so users can navigate directly
- Format links as: [Display Text](/path) - they will render as clickable links
- Only use links that appear in the data above - do not invent new links
- Available link patterns:
  * Assets: [ASSET-001](/Assets/Details/{id}) or [View All Assets](/Assets)
  * CIP Projects: [CIP-001](/CIP/Details/{id}) or [View All Projects](/CIP)
  * Maintenance: [View Maintenance](/Maintenance)";

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            if (!string.IsNullOrEmpty(conversationHistory))
            {
                var historyLines = conversationHistory.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in historyLines.TakeLast(10))
                {
                    if (line.StartsWith("User: "))
                        messages.Add(new { role = "user", content = line.Substring(6) });
                    else if (line.StartsWith("Assistant: "))
                        messages.Add(new { role = "assistant", content = line.Substring(11) });
                }
            }

            messages.Add(new { role = "user", content = question });

            var requestBody = new
            {
                model = "gpt-4o-mini",
                messages = messages,
                max_tokens = 1024,
                temperature = 0.7
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"I'm having trouble connecting to the AI service. Error: {response.StatusCode}. Please try again later.";
                }

                using var doc = JsonDocument.Parse(responseContent);
                var answer = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return answer ?? "I couldn't generate a response. Please try again.";
            }
            catch (Exception ex)
            {
                return $"I encountered an error: {ex.Message}. Please try again.";
            }
        }
    }
}
