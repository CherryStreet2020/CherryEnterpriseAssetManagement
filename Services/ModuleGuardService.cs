using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public interface IModuleGuardService
    {
        Task<bool> IsModuleEnabledAsync(string moduleName);
        Task<ModuleStatus> GetModuleStatusAsync();
    }

    public class ModuleStatus
    {
        public bool WorkOrdersEnabled { get; set; }
        public bool PurchasingEnabled { get; set; }
        public bool AccountsPayableEnabled { get; set; }
        public bool VendorsEnabled { get; set; }
        public bool InventoryEnabled { get; set; }
    }

    public class ModuleGuardService : IModuleGuardService
    {
        private readonly AppDbContext _context;
        private ModuleStatus? _cachedStatus;

        public ModuleGuardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ModuleStatus> GetModuleStatusAsync()
        {
            if (_cachedStatus != null)
                return _cachedStatus;

            var company = await _context.Companies
                .Where(c => c.ParentCompanyId == null)
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync() 
                ?? await _context.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();

            _cachedStatus = new ModuleStatus
            {
                WorkOrdersEnabled = company?.EnableWorkOrders ?? true,
                PurchasingEnabled = company?.EnablePurchasing ?? true,
                AccountsPayableEnabled = company?.EnableAccountsPayable ?? true,
                VendorsEnabled = company?.EnableVendors ?? true,
                InventoryEnabled = company?.EnableInventory ?? true
            };

            return _cachedStatus;
        }

        public async Task<bool> IsModuleEnabledAsync(string moduleName)
        {
            var status = await GetModuleStatusAsync();

            return moduleName.ToLower() switch
            {
                "workorders" or "maintenance" => status.WorkOrdersEnabled,
                "purchasing" or "requisitions" or "purchaseorders" => status.PurchasingEnabled,
                "accountspayable" or "ap" or "invoices" => status.AccountsPayableEnabled,
                "vendors" => status.VendorsEnabled,
                "inventory" or "items" or "stocklevels" => status.InventoryEnabled,
                _ => true
            };
        }
    }
}
