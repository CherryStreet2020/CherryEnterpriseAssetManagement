using System.IO.Compression;

namespace Abs.FixedAssets.Services;

public class TemplateService
{
    private readonly IWebHostEnvironment _env;

    private static readonly Dictionary<string, string> TemplateFiles = new()
    {
        ["Company"]            = "01_Company_Template.xlsx",
        ["Site"]               = "02_Site_Template.xlsx",
        ["Location"]           = "03_Location_Template.xlsx",
        ["Department"]         = "04_Department_Template.xlsx",
        ["GlAccount"]          = "05_GlAccount_Template.xlsx",
        ["Manufacturer"]       = "06_Manufacturer_Template.xlsx",
        ["AssetCategory"]      = "07_AssetCategory_Template.xlsx",
        ["Asset"]              = "08_Asset_Template.xlsx",
        ["DepreciationBook"]   = "09_DepreciationBook_Template.xlsx",
        ["Vendor"]             = "10_Vendor_Template.xlsx",
        ["Item"]               = "11_Item_Template.xlsx",
        ["ApprovedVendorList"] = "12_ApprovedVendorList_Template.xlsx",
        ["Technician"]         = "13_Technician_Template.xlsx",
        ["PMTemplate"]         = "14_PMTemplate_Template.xlsx",
        ["CIPProject"]         = "15_CIPProject_Template.xlsx",
    };

    public static readonly (string Key, string Label, string Icon, string Description)[] Steps = new[]
    {
        ("Company", "Companies", "fa-building", "Company records"),
        ("Site", "Sites", "fa-map-marker-alt", "Plant locations"),
        ("Location", "Locations", "fa-warehouse", "Areas within sites"),
        ("Department", "Departments", "fa-sitemap", "Organizational departments"),
        ("GlAccount", "GL Accounts", "fa-calculator", "Chart of accounts"),
        ("Manufacturer", "Manufacturers", "fa-industry", "Equipment manufacturers"),
        ("AssetCategory", "Asset Categories", "fa-tags", "Asset classification types"),
        ("Asset", "Assets", "fa-cogs", "Equipment and machinery"),
        ("DepreciationBook", "Dep. Books", "fa-book", "Depreciation book configurations"),
        ("Vendor", "Vendors", "fa-truck", "Suppliers and service providers"),
        ("Item", "Items", "fa-box", "Parts and inventory items"),
        ("ApprovedVendorList", "AVL Records", "fa-clipboard-check", "Approved vendor-part links"),
        ("Technician", "Technicians", "fa-hard-hat", "Maintenance technicians"),
        ("PMTemplate", "PM Templates", "fa-calendar-check", "Preventive maintenance templates"),
        ("CIPProject", "CIP Projects", "fa-project-diagram", "Capital improvement projects")
    };

    public TemplateService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public byte[] GenerateTemplate(string templateKey)
    {
        if (!TemplateFiles.TryGetValue(templateKey, out var filename))
            throw new ArgumentException($"Unknown template key: {templateKey}");

        var path = Path.Combine(_env.WebRootPath, "templates", filename);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Template file not found: {filename}");

        return File.ReadAllBytes(path);
    }

    public byte[] GenerateAllTemplatesZip()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var kvp in TemplateFiles)
            {
                var path = Path.Combine(_env.WebRootPath, "templates", kvp.Value);
                if (!File.Exists(path)) continue;

                var entry = archive.CreateEntry(kvp.Value, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(path);
                fileStream.CopyTo(entryStream);
            }
        }
        return ms.ToArray();
    }

    public byte[] GetMasterWorkbook()
    {
        var path = Path.Combine(_env.WebRootPath, "templates", "ABS_Machining_EAM_Import_Workbook.xlsx");
        if (!File.Exists(path))
            throw new FileNotFoundException("Master workbook not found");
        return File.ReadAllBytes(path);
    }
}
