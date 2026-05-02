using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services;

public class MasterDataImportService
{
    private readonly AppDbContext _context;
    private readonly ITenantContext _tenantContext;

    public MasterDataImportService(AppDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<ImportValidationResult> ValidateAsync(string entityType, Stream file)
    {
        var result = new ImportValidationResult();
        try
        {
            using var workbook = new XLWorkbook(file);
            var ws = workbook.Worksheets.First();
            var headers = ReadHeaders(ws);
            if (headers.Count == 0)
            {
                result.Errors.Add(new ImportRowError(0, "File", "No headers found in the first row."));
                return result;
            }

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            result.TotalRows = Math.Max(0, lastRow - 1);

            for (int row = 2; row <= lastRow; row++)
            {
                var values = ReadRow(ws, row, headers.Count);
                if (values.All(string.IsNullOrWhiteSpace)) continue;
                var map = BuildMap(headers, values);
                var rowErrors = await ValidateRow(entityType, map, row);
                if (rowErrors.Any())
                    result.Errors.AddRange(rowErrors);
                else
                    result.ValidRows++;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ImportRowError(0, "File", $"Failed to read file: {ex.Message}"));
        }
        return result;
    }

    public async Task<MasterImportResult> ImportAsync(string entityType, Stream file)
    {
        var result = new MasterImportResult();
        try
        {
            using var workbook = new XLWorkbook(file);
            var ws = workbook.Worksheets.First();
            var headers = ReadHeaders(ws);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            int pendingCount = 0;

            for (int row = 2; row <= lastRow; row++)
            {
                var values = ReadRow(ws, row, headers.Count);
                if (values.All(string.IsNullOrWhiteSpace)) continue;
                var map = BuildMap(headers, values);

                try
                {
                    var rowErrors = await ValidateRow(entityType, map, row);
                    if (rowErrors.Any())
                    {
                        result.Errors.AddRange(rowErrors);
                        result.FailedCount++;
                        continue;
                    }

                    var imported = await ImportRow(entityType, map);
                    if (imported)
                        pendingCount++;
                    else
                        result.SkippedCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new ImportRowError(row, "Row", ex.Message));
                    result.FailedCount++;
                }
            }

            if (pendingCount > 0)
            {
                await _context.SaveChangesAsync();
                result.ImportedCount = pendingCount;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ImportRowError(0, "File", $"Import failed: {ex.Message}"));
        }
        return result;
    }

    private List<string> ReadHeaders(IXLWorksheet ws)
    {
        var headers = new List<string>();
        var row = ws.Row(1);
        for (int c = 1; c <= 30; c++)
        {
            var val = row.Cell(c).GetString().Trim().Replace(" *", "").TrimEnd('*').Trim();
            if (string.IsNullOrEmpty(val)) break;
            headers.Add(val);
        }
        return headers;
    }

    private List<string> ReadRow(IXLWorksheet ws, int rowNum, int colCount)
    {
        var values = new List<string>();
        for (int c = 1; c <= colCount; c++)
            values.Add(ws.Cell(rowNum, c).GetString().Trim());
        return values;
    }

    private Dictionary<string, string> BuildMap(List<string> headers, List<string> values)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count && i < values.Count; i++)
            map[NormalizeHeader(headers[i])] = values[i];
        return map;
    }

    private string NormalizeHeader(string h) => h.ToLower().Replace(" ", "").Replace("_", "").Replace("-", "");

    private string Get(Dictionary<string, string> map, params string[] keys)
    {
        foreach (var k in keys)
        {
            var nk = NormalizeHeader(k);
            if (map.TryGetValue(nk, out var v) && !string.IsNullOrWhiteSpace(v))
                return v;
        }
        return "";
    }

    private bool GetBool(Dictionary<string, string> map, params string[] keys)
    {
        var v = Get(map, keys).ToLower();
        return v == "yes" || v == "true" || v == "1";
    }

    private decimal GetDecimal(Dictionary<string, string> map, params string[] keys)
    {
        var v = Get(map, keys).Replace("$", "").Replace(",", "");
        return decimal.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    private int GetInt(Dictionary<string, string> map, params string[] keys)
    {
        var v = Get(map, keys);
        return int.TryParse(v, out var i) ? i : 0;
    }

    private DateTime? GetDate(Dictionary<string, string> map, params string[] keys)
    {
        var v = Get(map, keys);
        return DateTime.TryParse(v, out var d) ? d : null;
    }

    private int GetCompanyId() => _tenantContext.CompanyId ?? 1;

    private async Task<List<ImportRowError>> ValidateRow(string entityType, Dictionary<string, string> map, int row)
    {
        var errors = new List<ImportRowError>();

        switch (entityType)
        {
            case "Company":
                if (string.IsNullOrWhiteSpace(Get(map, "Company Code", "CompanyCode")))
                    errors.Add(new ImportRowError(row, "Company Code", "Required"));
                if (string.IsNullOrWhiteSpace(Get(map, "Company Name", "CompanyName")))
                    errors.Add(new ImportRowError(row, "Company Name", "Required"));
                break;

            case "Site":
                if (string.IsNullOrWhiteSpace(Get(map, "Site Code", "SiteCode")))
                    errors.Add(new ImportRowError(row, "Site Code", "Required"));
                if (string.IsNullOrWhiteSpace(Get(map, "Site Name", "SiteName")))
                    errors.Add(new ImportRowError(row, "Site Name", "Required"));
                var siteCompanyCode = Get(map, "Company Code", "CompanyCode");
                if (string.IsNullOrWhiteSpace(siteCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", "Required"));
                else if (!await _context.Companies.AnyAsync(c => c.CompanyCode == siteCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", $"Company '{siteCompanyCode}' not found"));
                break;

            case "Location":
                if (string.IsNullOrWhiteSpace(Get(map, "Location Code", "LocationCode")))
                    errors.Add(new ImportRowError(row, "Location Code", "Required"));
                if (string.IsNullOrWhiteSpace(Get(map, "Location Name", "LocationName")))
                    errors.Add(new ImportRowError(row, "Location Name", "Required"));
                var locSiteCode = Get(map, "Site Code", "SiteCode");
                if (string.IsNullOrWhiteSpace(locSiteCode))
                    errors.Add(new ImportRowError(row, "Site Code", "Required"));
                else if (!await _context.Sites.AnyAsync(s => s.SiteCode == locSiteCode))
                    errors.Add(new ImportRowError(row, "Site Code", $"Site '{locSiteCode}' not found"));
                break;

            case "Department":
                if (string.IsNullOrWhiteSpace(Get(map, "Department Code", "DepartmentCode")))
                    errors.Add(new ImportRowError(row, "Department Code", "Required"));
                if (string.IsNullOrWhiteSpace(Get(map, "Department Name", "DepartmentName")))
                    errors.Add(new ImportRowError(row, "Department Name", "Required"));
                var deptCompanyCode = Get(map, "Company Code", "CompanyCode");
                if (string.IsNullOrWhiteSpace(deptCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", "Required"));
                else if (!await _context.Companies.AnyAsync(c => c.CompanyCode == deptCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", $"Company '{deptCompanyCode}' not found"));
                break;

            case "GlAccount":
                if (string.IsNullOrWhiteSpace(Get(map, "Account Number", "AccountNumber")))
                    errors.Add(new ImportRowError(row, "Account Number", "Required"));
                if (string.IsNullOrWhiteSpace(Get(map, "Account Name", "AccountName")))
                    errors.Add(new ImportRowError(row, "Account Name", "Required"));
                var glCompanyCode = Get(map, "Company Code", "CompanyCode");
                if (string.IsNullOrWhiteSpace(glCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", "Required"));
                else if (!await _context.Companies.AnyAsync(c => c.CompanyCode == glCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", $"Company '{glCompanyCode}' not found"));
                var glType = Get(map, "Type");
                if (!string.IsNullOrWhiteSpace(glType) && !Enum.TryParse<GlAccountType>(glType, true, out _))
                    errors.Add(new ImportRowError(row, "Type", $"Invalid type '{glType}'"));
                break;

            case "AssetCategory":
                if (string.IsNullOrWhiteSpace(Get(map, "Category Code", "CategoryCode")))
                    errors.Add(new ImportRowError(row, "Category Code", "Required"));
                if (string.IsNullOrWhiteSpace(Get(map, "Category Name", "CategoryName")))
                    errors.Add(new ImportRowError(row, "Category Name", "Required"));
                var catCompanyCode = Get(map, "Company Code", "CompanyCode");
                if (string.IsNullOrWhiteSpace(catCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", "Required"));
                else if (!await _context.Companies.AnyAsync(c => c.CompanyCode == catCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", $"Company '{catCompanyCode}' not found"));
                var assetGl = Get(map, "Asset GL Account Number", "AssetGLAccountNumber");
                if (string.IsNullOrWhiteSpace(assetGl))
                    errors.Add(new ImportRowError(row, "Asset GL Account Number", "Required"));
                var accumGl = Get(map, "Accum Depr GL Account Number", "AccumDeprGLAccountNumber");
                if (string.IsNullOrWhiteSpace(accumGl))
                    errors.Add(new ImportRowError(row, "Accum Depr GL Account Number", "Required"));
                var expGl = Get(map, "Depr Expense GL Account Number", "DeprExpenseGLAccountNumber");
                if (string.IsNullOrWhiteSpace(expGl))
                    errors.Add(new ImportRowError(row, "Depr Expense GL Account Number", "Required"));
                break;

            case "Vendor":
                if (string.IsNullOrWhiteSpace(Get(map, "Vendor Code", "VendorCode")))
                    errors.Add(new ImportRowError(row, "Vendor Code", "Required"));
                if (string.IsNullOrWhiteSpace(Get(map, "Vendor Name", "VendorName")))
                    errors.Add(new ImportRowError(row, "Vendor Name", "Required"));
                var vndCompanyCode = Get(map, "Company Code", "CompanyCode");
                if (string.IsNullOrWhiteSpace(vndCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", "Required"));
                else if (!await _context.Companies.AnyAsync(c => c.CompanyCode == vndCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", $"Company '{vndCompanyCode}' not found"));
                break;

            case "Manufacturer":
                if (string.IsNullOrWhiteSpace(Get(map, "Manufacturer Code", "ManufacturerCode")))
                    errors.Add(new ImportRowError(row, "Manufacturer Code", "Required"));
                if (string.IsNullOrWhiteSpace(Get(map, "Manufacturer Name", "ManufacturerName")))
                    errors.Add(new ImportRowError(row, "Manufacturer Name", "Required"));
                break;

            case "Item":
                if (string.IsNullOrWhiteSpace(Get(map, "Part Number", "PartNumber")))
                    errors.Add(new ImportRowError(row, "Part Number", "Required"));
                if (string.IsNullOrWhiteSpace(Get(map, "Description")))
                    errors.Add(new ImportRowError(row, "Description", "Required"));
                var itemCompanyCode = Get(map, "Company Code", "CompanyCode");
                if (string.IsNullOrWhiteSpace(itemCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", "Required"));
                else if (!await _context.Companies.AnyAsync(c => c.CompanyCode == itemCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", $"Company '{itemCompanyCode}' not found"));
                var pvCode = Get(map, "Primary Vendor Code", "PrimaryVendorCode");
                if (!string.IsNullOrWhiteSpace(pvCode) && !await _context.Vendors.AnyAsync(v => v.Code == pvCode))
                    errors.Add(new ImportRowError(row, "Primary Vendor Code", $"Vendor '{pvCode}' not found"));
                break;

            case "ApprovedVendorList":
                var avlPart = Get(map, "Part Number", "PartNumber");
                if (string.IsNullOrWhiteSpace(avlPart))
                    errors.Add(new ImportRowError(row, "Part Number", "Required"));
                else if (!await _context.Items.AnyAsync(i => i.PartNumber == avlPart))
                    errors.Add(new ImportRowError(row, "Part Number", $"Item '{avlPart}' not found"));
                var avlVendor = Get(map, "Vendor Code", "VendorCode");
                if (string.IsNullOrWhiteSpace(avlVendor))
                    errors.Add(new ImportRowError(row, "Vendor Code", "Required"));
                else if (!await _context.Vendors.AnyAsync(v => v.Code == avlVendor))
                    errors.Add(new ImportRowError(row, "Vendor Code", $"Vendor '{avlVendor}' not found"));
                break;

            case "Asset":
                if (string.IsNullOrWhiteSpace(Get(map, "Asset Number", "AssetNumber")))
                    errors.Add(new ImportRowError(row, "Asset Number", "Required"));
                if (string.IsNullOrWhiteSpace(Get(map, "Description")))
                    errors.Add(new ImportRowError(row, "Description", "Required"));
                var astCompanyCode = Get(map, "Company Code", "CompanyCode");
                if (string.IsNullOrWhiteSpace(astCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", "Required"));
                else if (!await _context.Companies.AnyAsync(c => c.CompanyCode == astCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", $"Company '{astCompanyCode}' not found"));
                var inServiceStr = Get(map, "In-Service Date", "InServiceDate", "In Service Date");
                if (string.IsNullOrWhiteSpace(inServiceStr))
                    errors.Add(new ImportRowError(row, "In-Service Date", "Required"));
                else if (!DateTime.TryParse(inServiceStr, out _))
                    errors.Add(new ImportRowError(row, "In-Service Date", $"Invalid date '{inServiceStr}'"));
                var acqCostStr = Get(map, "Acquisition Cost", "AcquisitionCost");
                if (string.IsNullOrWhiteSpace(acqCostStr))
                    errors.Add(new ImportRowError(row, "Acquisition Cost", "Required"));
                break;

            case "DepreciationBook":
                if (string.IsNullOrWhiteSpace(Get(map, "Book Name", "BookName")))
                    errors.Add(new ImportRowError(row, "Book Name", "Required"));
                var bookCompanyCode = Get(map, "Company Code", "CompanyCode");
                if (string.IsNullOrWhiteSpace(bookCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", "Required"));
                else if (!await _context.Companies.AnyAsync(c => c.CompanyCode == bookCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", $"Company '{bookCompanyCode}' not found"));
                break;

            case "Technician":
                if (string.IsNullOrWhiteSpace(Get(map, "Name")))
                    errors.Add(new ImportRowError(row, "Name", "Required"));
                var techCompanyCode = Get(map, "Company Code", "CompanyCode");
                if (string.IsNullOrWhiteSpace(techCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", "Required"));
                else if (!await _context.Companies.AnyAsync(c => c.CompanyCode == techCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", $"Company '{techCompanyCode}' not found"));
                break;

            case "PMTemplate":
                if (string.IsNullOrWhiteSpace(Get(map, "Template Code", "TemplateCode")))
                    errors.Add(new ImportRowError(row, "Template Code", "Required"));
                if (string.IsNullOrWhiteSpace(Get(map, "Template Name", "TemplateName")))
                    errors.Add(new ImportRowError(row, "Template Name", "Required"));
                break;

            case "CIPProject":
                if (string.IsNullOrWhiteSpace(Get(map, "Project Number", "ProjectNumber")))
                    errors.Add(new ImportRowError(row, "Project Number", "Required"));
                if (string.IsNullOrWhiteSpace(Get(map, "Project Name", "ProjectName")))
                    errors.Add(new ImportRowError(row, "Project Name", "Required"));
                var cipCompanyCode = Get(map, "Company Code", "CompanyCode");
                if (string.IsNullOrWhiteSpace(cipCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", "Required"));
                else if (!await _context.Companies.AnyAsync(c => c.CompanyCode == cipCompanyCode))
                    errors.Add(new ImportRowError(row, "Company Code", $"Company '{cipCompanyCode}' not found"));
                break;
        }

        return errors;
    }

    private async Task<bool> ImportRow(string entityType, Dictionary<string, string> map)
    {
        switch (entityType)
        {
            case "Company":
            {
                var code = Get(map, "Company Code", "CompanyCode");
                if (await _context.Companies.AnyAsync(c => c.CompanyCode == code)) return false;
                var company = new Company
                {
                    CompanyCode = code,
                    Name = Get(map, "Company Name", "CompanyName"),
                    Currency = Get(map, "Currency") is var cur && !string.IsNullOrEmpty(cur) ? cur : "USD",
                    Address = Get(map, "Address"),
                    City = Get(map, "City"),
                    StateProvince = Get(map, "State"),
                    PostalCode = Get(map, "Zip", "ZipCode", "PostalCode"),
                    Country = Get(map, "Country"),
                    ContactPhone = Get(map, "Phone"),
                    TaxId = Get(map, "Tax ID", "TaxID", "TaxId")
                };
                var parentCode = Get(map, "Parent Company Code", "ParentCompanyCode");
                if (!string.IsNullOrEmpty(parentCode))
                {
                    var parent = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyCode == parentCode);
                    if (parent != null) company.ParentCompanyId = parent.Id;
                }
                _context.Companies.Add(company);
                return true;
            }

            case "Site":
            {
                var code = Get(map, "Site Code", "SiteCode");
                if (await _context.Sites.AnyAsync(s => s.SiteCode == code)) return false;
                var companyCode = Get(map, "Company Code", "CompanyCode");
                var company = await _context.Companies.FirstAsync(c => c.CompanyCode == companyCode);
                var site = new Site
                {
                    SiteCode = code,
                    Name = Get(map, "Site Name", "SiteName"),
                    CompanyId = company.Id,
                    Address1 = Get(map, "Address"),
                    City = Get(map, "City"),
                    StateProvince = Get(map, "State"),
                    SiteManager = Get(map, "Manager Name", "ManagerName"),
                    MainPhone = Get(map, "Phone")
                };
                _context.Sites.Add(site);
                return true;
            }

            case "Location":
            {
                var code = Get(map, "Location Code", "LocationCode");
                var siteCode = Get(map, "Site Code", "SiteCode");
                var site = await _context.Sites.FirstAsync(s => s.SiteCode == siteCode);
                if (await _context.Locations.AnyAsync(l => l.Code == code && l.SiteId == site.Id)) return false;
                var loc = new Location
                {
                    Code = code,
                    Name = Get(map, "Location Name", "LocationName"),
                    SiteId = site.Id,
                    CompanyId = site.CompanyId,
                    Building = Get(map, "Building"),
                    Floor = Get(map, "Floor"),
                    Description = Get(map, "Description"),
                    IsActive = Get(map, "Is Active", "IsActive") is var ia && (string.IsNullOrEmpty(ia) || ia.Equals("Yes", StringComparison.OrdinalIgnoreCase) || ia == "1" || ia.Equals("true", StringComparison.OrdinalIgnoreCase))
                };
                _context.Locations.Add(loc);
                return true;
            }

            case "Department":
            {
                var code = Get(map, "Department Code", "DepartmentCode");
                var companyCode = Get(map, "Company Code", "CompanyCode");
                var company = await _context.Companies.FirstAsync(c => c.CompanyCode == companyCode);
                if (await _context.Departments.AnyAsync(d => d.Code == code && d.CompanyId == company.Id)) return false;
                var dept = new Department
                {
                    Code = code,
                    Name = Get(map, "Department Name", "DepartmentName"),
                    CompanyId = company.Id
                };
                _context.Departments.Add(dept);
                return true;
            }

            case "GlAccount":
            {
                var acctNum = Get(map, "Account Number", "AccountNumber");
                var companyCode = Get(map, "Company Code", "CompanyCode");
                var company = await _context.Companies.FirstAsync(c => c.CompanyCode == companyCode);
                if (await _context.GlAccounts.AnyAsync(g => g.AccountNumber == acctNum && g.CompanyId == company.Id)) return false;
                var acct = new GlAccount
                {
                    AccountNumber = acctNum,
                    Name = Get(map, "Account Name", "AccountName"),
                    CompanyId = company.Id,
                    Description = Get(map, "Description"),
                    IsActive = Get(map, "Is Active", "IsActive") is var ia && (string.IsNullOrEmpty(ia) || ia.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                };
                var typeStr = Get(map, "Type");
                if (Enum.TryParse<GlAccountType>(typeStr, true, out var glType))
                    acct.AccountType = glType;
                var balStr = Get(map, "Normal Balance", "NormalBalance");
                if (!string.IsNullOrEmpty(balStr))
                    acct.NormalBalance = balStr.Equals("Credit", StringComparison.OrdinalIgnoreCase) ? NormalBalance.Credit : NormalBalance.Debit;
                _context.GlAccounts.Add(acct);
                return true;
            }

            case "AssetCategory":
            {
                var code = Get(map, "Category Code", "CategoryCode");
                var companyCode = Get(map, "Company Code", "CompanyCode");
                var company = await _context.Companies.FirstAsync(c => c.CompanyCode == companyCode);
                if (await _context.AssetCategories.AnyAsync(ac => ac.Code == code && ac.CompanyId == company.Id)) return false;
                var assetGlNum = Get(map, "Asset GL Account Number", "AssetGLAccountNumber");
                var accumGlNum = Get(map, "Accum Depr GL Account Number", "AccumDeprGLAccountNumber");
                var expGlNum = Get(map, "Depr Expense GL Account Number", "DeprExpenseGLAccountNumber");
                var assetGl = await _context.GlAccounts.FirstOrDefaultAsync(g => g.AccountNumber == assetGlNum && g.CompanyId == company.Id);
                var accumGl = await _context.GlAccounts.FirstOrDefaultAsync(g => g.AccountNumber == accumGlNum && g.CompanyId == company.Id);
                var expGl = await _context.GlAccounts.FirstOrDefaultAsync(g => g.AccountNumber == expGlNum && g.CompanyId == company.Id);
                var cat = new AssetCategory
                {
                    Code = code,
                    Name = Get(map, "Category Name", "CategoryName"),
                    CompanyId = company.Id,
                    DefaultUsefulLifeMonths = GetInt(map, "Useful Life Years", "UsefulLifeYears") is var yrs && yrs > 0 ? yrs * 12 : 84,
                    AssetGlAccountId = assetGl?.Id,
                    AccumDepGlAccountId = accumGl?.Id,
                    DepExpGlAccountId = expGl?.Id
                };
                
                _context.AssetCategories.Add(cat);
                return true;
            }

            case "Vendor":
            {
                var code = Get(map, "Vendor Code", "VendorCode");
                var companyCode = Get(map, "Company Code", "CompanyCode");
                var company = await _context.Companies.FirstAsync(c => c.CompanyCode == companyCode);
                if (await _context.Vendors.AnyAsync(v => v.Code == code && v.CompanyId == company.Id)) return false;
                var vendor = new Vendor
                {
                    Code = code,
                    Name = Get(map, "Vendor Name", "VendorName"),
                    CompanyId = company.Id,
                    ContactName = Get(map, "Contact Name", "ContactName"),
                    Email = Get(map, "Email"),
                    Phone = Get(map, "Phone"),
                    Address = Get(map, "Address"),
                    City = Get(map, "City"),
                    State = Get(map, "State"),
                    PostalCode = Get(map, "Zip", "ZipCode"),
                    TaxId = Get(map, "Tax ID", "TaxID"),
                    IsActive = Get(map, "Is Active", "IsActive") is var ia && (string.IsNullOrEmpty(ia) || ia.Equals("Yes", StringComparison.OrdinalIgnoreCase)),
                    Currency = "USD",
                    Status = VendorStatus.Active
                };
                var termsStr = Get(map, "Payment Terms", "PaymentTerms");
                if (!string.IsNullOrEmpty(termsStr))
                {
                    var termsMap = new Dictionary<string, PaymentTerms>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Net 30"] = PaymentTerms.Net30, ["Net30"] = PaymentTerms.Net30,
                        ["Net 45"] = PaymentTerms.Net45, ["Net45"] = PaymentTerms.Net45,
                        ["Net 60"] = PaymentTerms.Net60, ["Net60"] = PaymentTerms.Net60,
                        ["Net 90"] = PaymentTerms.Net90, ["Net90"] = PaymentTerms.Net90,
                        ["COD"] = PaymentTerms.COD,
                        ["Due On Receipt"] = PaymentTerms.DueOnReceipt, ["DueOnReceipt"] = PaymentTerms.DueOnReceipt,
                        ["Prepaid"] = PaymentTerms.Prepaid
                    };
                    if (termsMap.TryGetValue(termsStr, out var terms))
                        vendor.PaymentTerms = terms;
                }
                _context.Vendors.Add(vendor);
                return true;
            }

            case "Manufacturer":
            {
                var code = Get(map, "Manufacturer Code", "ManufacturerCode");
                if (await _context.Set<Manufacturer>().AnyAsync(m => m.Code == code)) return false;
                var mfr = new Manufacturer
                {
                    Code = code,
                    Name = Get(map, "Manufacturer Name", "ManufacturerName"),
                    Website = Get(map, "Website"),
                    Country = Get(map, "Country"),
                    ContactName = Get(map, "Contact Name", "ContactName"),
                    ContactEmail = Get(map, "Contact Email", "ContactEmail"),
                    ContactPhone = Get(map, "Contact Phone", "ContactPhone")
                };
                _context.Set<Manufacturer>().Add(mfr);
                return true;
            }

            case "Item":
            {
                var partNum = Get(map, "Part Number", "PartNumber");
                if (await _context.Items.AnyAsync(i => i.PartNumber == partNum)) return false;
                var companyCode = Get(map, "Company Code", "CompanyCode");
                var company = await _context.Companies.FirstAsync(c => c.CompanyCode == companyCode);
                var item = new Item
                {
                    PartNumber = partNum,
                    Description = Get(map, "Description"),
                    CompanyId = company.Id,
                    StandardCost = GetDecimal(map, "Standard Cost", "StandardCost"),
                    ReorderPoint = GetDecimal(map, "Reorder Point", "ReorderPoint"),
                    ReorderQuantity = GetDecimal(map, "Reorder Qty", "ReorderQty", "ReorderQuantity"),
                    Warehouse = Get(map, "Warehouse"),
                    Aisle = Get(map, "Aisle"),
                    Rack = Get(map, "Rack"),
                    Shelf = Get(map, "Shelf"),
                    Bin = Get(map, "Bin"),
                    Status = ItemStatus.Active
                };
                var typeStr = Get(map, "Type");
                if (Enum.TryParse<ItemType>(typeStr, true, out var itemType))
                    item.Type = itemType;
                var uom = Get(map, "UOM");
                if (!string.IsNullOrEmpty(uom) && Enum.TryParse<UnitOfMeasure>(uom, true, out var uomVal))
                    item.UOM = uomVal;
                var pvCode = Get(map, "Primary Vendor Code", "PrimaryVendorCode");
                if (!string.IsNullOrEmpty(pvCode))
                {
                    var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Code == pvCode);
                    if (vendor != null) item.PrimaryVendorId = vendor.Id;
                }
                _context.Items.Add(item);
                return true;
            }

            case "ApprovedVendorList":
            {
                var partNum = Get(map, "Part Number", "PartNumber");
                var vendorCode = Get(map, "Vendor Code", "VendorCode");
                var item = await _context.Items.FirstAsync(i => i.PartNumber == partNum);
                var vendor = await _context.Vendors.FirstAsync(v => v.Code == vendorCode);
                if (await _context.Set<ItemApprovedVendor>().AnyAsync(a => a.ItemId == item.Id && a.VendorId == vendor.Id)) return false;
                var avl = new ItemApprovedVendor
                {
                    ItemId = item.Id,
                    VendorId = vendor.Id,
                    IsPreferred = GetBool(map, "Is Preferred", "IsPreferred")
                };
                _context.Set<ItemApprovedVendor>().Add(avl);
                return true;
            }

            case "Asset":
            {
                var assetNum = Get(map, "Asset Number", "AssetNumber");
                if (await _context.Assets.AnyAsync(a => a.AssetNumber == assetNum)) return false;
                var companyCode = Get(map, "Company Code", "CompanyCode");
                var company = await _context.Companies.FirstAsync(c => c.CompanyCode == companyCode);
                var asset = new Asset
                {
                    AssetNumber = assetNum,
                    Description = Get(map, "Description"),
                    CompanyId = company.Id,
                    SerialNumber = Get(map, "Serial Number", "SerialNumber"),
                    Model = Get(map, "Model"),
                    InServiceDate = GetDate(map, "In-Service Date", "InServiceDate", "In Service Date") ?? DateTime.UtcNow,
                    AcquisitionCost = GetDecimal(map, "Acquisition Cost", "AcquisitionCost"),
                    SalvageValue = GetDecimal(map, "Salvage Value", "SalvageValue"),
                    UsefulLifeMonths = GetInt(map, "Useful Life Months", "UsefulLifeMonths"),
                    Currency = "USD"
                };
                var statusStr = Get(map, "Status");
                if (Enum.TryParse<AssetStatus>(statusStr, true, out var status))
                    asset.Status = status;
                else
                    asset.Status = AssetStatus.Active;
                var catCode = Get(map, "Category Code", "CategoryCode");
                if (!string.IsNullOrEmpty(catCode))
                {
                    var cat = await _context.AssetCategories.FirstOrDefaultAsync(c => c.Code == catCode && c.CompanyId == company.Id);
                    if (cat != null) asset.AssetCategoryId = cat.Id;
                }
                var siteCode = Get(map, "Site Code", "SiteCode");
                if (!string.IsNullOrEmpty(siteCode))
                {
                    var site = await _context.Sites.FirstOrDefaultAsync(s => s.SiteCode == siteCode);
                    if (site != null) asset.SiteId = site.Id;
                }
                var locName = Get(map, "Location Name", "LocationName");
                if (!string.IsNullOrEmpty(locName))
                {
                    var loc = await _context.Locations.FirstOrDefaultAsync(l => l.Name == locName);
                    if (loc != null) asset.LocationId = loc.Id;
                }
                var deptCode = Get(map, "Department Code", "DepartmentCode");
                if (!string.IsNullOrEmpty(deptCode))
                {
                    var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Code == deptCode);
                    if (dept != null) asset.DepartmentId = dept.Id;
                }
                _context.Assets.Add(asset);
                return true;
            }

            case "DepreciationBook":
            {
                var bookName = Get(map, "Book Name", "BookName");
                var companyCode = Get(map, "Company Code", "CompanyCode");
                var company = await _context.Companies.FirstAsync(c => c.CompanyCode == companyCode);
                if (await _context.Books.AnyAsync(b => b.Name == bookName && b.CompanyId == company.Id)) return false;
                var book = new Book
                {
                    Name = bookName,
                    CompanyId = company.Id
                };
                var methodStr = Get(map, "Method");
                if (Enum.TryParse<DepreciationMethod>(methodStr, true, out var method))
                    book.Method = method;
                var convStr = Get(map, "Convention");
                if (Enum.TryParse<DepreciationConvention>(convStr, true, out var conv))
                    book.Convention = conv;
                _context.Books.Add(book);
                return true;
            }

            case "Technician":
            {
                var name = Get(map, "Name");
                var companyCode = Get(map, "Company Code", "CompanyCode");
                var company = await _context.Companies.FirstAsync(c => c.CompanyCode == companyCode);
                var tech = new Technician
                {
                    Name = name,
                    CompanyId = company.Id,
                    EmployeeId = Get(map, "Employee ID", "EmployeeID", "EmployeeId"),
                    Email = Get(map, "Email"),
                    Phone = Get(map, "Phone"),
                    PrimaryCraft = Get(map, "Craft", "PrimaryCraft"),
                    HourlyRate = GetDecimal(map, "Hourly Rate", "HourlyRate"),
                    Active = Get(map, "Is Active", "IsActive") is var ia && (string.IsNullOrEmpty(ia) || ia.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                };
                var siteCode = Get(map, "Site Code", "SiteCode");
                if (!string.IsNullOrEmpty(siteCode))
                {
                    var site = await _context.Sites.FirstOrDefaultAsync(s => s.SiteCode == siteCode);
                    if (site != null) tech.SiteId = site.Id;
                }
                _context.Technicians.Add(tech);
                return true;
            }

            case "PMTemplate":
            {
                var code = Get(map, "Template Code", "TemplateCode");
                if (await _context.PMTemplates.AnyAsync(t => t.Code == code)) return false;
                var template = new PMTemplate
                {
                    Code = code,
                    Name = Get(map, "Template Name", "TemplateName"),
                    Description = Get(map, "Description"),
                    CalendarIntervalValue = GetInt(map, "Frequency Days", "FrequencyDays") is var fd && fd > 0 ? fd : 1
                };
                var priority = Get(map, "Priority");
                if (Enum.TryParse<PMPriority>(priority, true, out var pmPri))
                    template.Priority = pmPri;
                _context.PMTemplates.Add(template);
                return true;
            }

            case "CIPProject":
            {
                var projNum = Get(map, "Project Number", "ProjectNumber");
                if (await _context.CipProjects.AnyAsync(p => p.ProjectNumber == projNum)) return false;
                var companyCode = Get(map, "Company Code", "CompanyCode");
                var company = await _context.Companies.FirstAsync(c => c.CompanyCode == companyCode);
                var project = new CipProject
                {
                    ProjectNumber = projNum,
                    Name = Get(map, "Project Name", "ProjectName"),
                    CompanyId = company.Id,
                    Location = Get(map, "Location"),
                    BudgetAmount = GetDecimal(map, "Budget"),
                    StartDate = GetDate(map, "Start Date", "StartDate") ?? DateTime.UtcNow,
                    EstimatedCompletionDate = GetDate(map, "Est Completion", "EstCompletion", "EstimatedCompletionDate"),
                    ProjectManagerName = Get(map, "Project Manager", "ProjectManager")
                };
                var statusStr = Get(map, "Status");
                if (Enum.TryParse<CipProjectStatus>(statusStr, true, out var status))
                    project.Status = status;
                else
                    project.Status = CipProjectStatus.Planned;
                var siteCode = Get(map, "Site Code", "SiteCode");
                if (!string.IsNullOrEmpty(siteCode))
                {
                    var site = await _context.Sites.FirstOrDefaultAsync(s => s.SiteCode == siteCode);
                    if (site != null) project.SiteId = site.Id;
                }
                _context.CipProjects.Add(project);
                return true;
            }

            default:
                return false;
        }
    }
}

public record ImportRowError(int Row, string Column, string Message);

public class ImportValidationResult
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public List<ImportRowError> Errors { get; set; } = new();
    public bool IsValid => !Errors.Any();
    public int ErrorRows => TotalRows - ValidRows;
}

public class MasterImportResult
{
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public List<ImportRowError> Errors { get; set; } = new();
    public bool HasErrors => Errors.Any();
}
