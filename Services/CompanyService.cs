using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public interface ICompanyService
    {
        Task<bool> IsMultiCompanyModeAsync();
        Task<bool> IsSingleCompanyModeAsync();
        Task<int> GetDefaultCompanyIdAsync();
        Task<Company?> GetDefaultCompanyAsync();
        Task<List<Company>> GetActiveCompaniesAsync();
        Task<Company?> GetCompanyAsync(int companyId);
        Task<FinancialMode> GetFinancialModeAsync();
        Task<bool> IsStandaloneModeAsync();
        Task<bool> IsERPIntegrationModeAsync();
    }

    public class CompanyService : ICompanyService
    {
        private readonly AppDbContext _context;
        private bool? _isMultiCompanyMode;
        private int? _defaultCompanyId;
        private Company? _defaultCompany;
        private List<Company>? _activeCompanies;

        public CompanyService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsMultiCompanyModeAsync()
        {
            if (_isMultiCompanyMode.HasValue)
                return _isMultiCompanyMode.Value;

            var companies = await GetActiveCompaniesAsync();
            _isMultiCompanyMode = companies.Count > 1 || 
                companies.Any(c => c.CompanyStructure == CompanyStructure.MultiCompany);
            
            return _isMultiCompanyMode.Value;
        }

        public async Task<bool> IsSingleCompanyModeAsync()
        {
            return !await IsMultiCompanyModeAsync();
        }

        public async Task<int> GetDefaultCompanyIdAsync()
        {
            if (_defaultCompanyId.HasValue)
                return _defaultCompanyId.Value;

            var company = await GetDefaultCompanyAsync();
            _defaultCompanyId = company?.Id ?? 0;
            return _defaultCompanyId.Value;
        }

        public async Task<Company?> GetDefaultCompanyAsync()
        {
            if (_defaultCompany != null)
                return _defaultCompany;

            _defaultCompany = await _context.Companies
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync();

            return _defaultCompany;
        }

        public async Task<List<Company>> GetActiveCompaniesAsync()
        {
            if (_activeCompanies != null)
                return _activeCompanies;

            _activeCompanies = await _context.Companies
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return _activeCompanies;
        }

        public async Task<Company?> GetCompanyAsync(int companyId)
        {
            return await _context.Companies.Where(c => c.Id == companyId).FirstOrDefaultAsync();
        }

        public async Task<FinancialMode> GetFinancialModeAsync()
        {
            var company = await GetDefaultCompanyAsync();
            return company?.FinancialMode ?? FinancialMode.Standalone;
        }

        public async Task<bool> IsStandaloneModeAsync()
        {
            var mode = await GetFinancialModeAsync();
            return mode == FinancialMode.Standalone;
        }

        public async Task<bool> IsERPIntegrationModeAsync()
        {
            var mode = await GetFinancialModeAsync();
            return mode == FinancialMode.ERPIntegration;
        }
    }
}
