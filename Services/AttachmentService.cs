using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public class AttachmentService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ITenantContext _tenantContext;
        private readonly string _uploadsPath;

        public AttachmentService(AppDbContext context, IWebHostEnvironment environment, ITenantContext tenantContext)
        {
            _context = context;
            _environment = environment;
            _tenantContext = tenantContext;
            _uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            
            if (!Directory.Exists(_uploadsPath))
            {
                Directory.CreateDirectory(_uploadsPath);
            }
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

        public async Task<Attachment> UploadAsync(
            Stream fileStream, 
            string fileName, 
            string contentType,
            long fileSize,
            int? assetId,
            AttachmentSource source,
            int? sourceId = null,
            AttachmentCategory category = AttachmentCategory.Other,
            string? description = null,
            string? uploadedBy = null)
        {
            if (assetId.HasValue)
            {
                var companyId = GetCompanyId();
                var assetBelongsToTenant = await _context.Assets.AnyAsync(a => a.Id == assetId.Value && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0));
                if (!assetBelongsToTenant)
                    throw new InvalidOperationException("Asset not found for this tenant");
            }

            var ext = Path.GetExtension(fileName);
            var storedFileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(_uploadsPath, storedFileName);

            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                await fileStream.CopyToAsync(fs);
            }

            var attachment = new Attachment
            {
                AssetId = assetId,
                Source = source,
                FileName = fileName,
                StoredFileName = storedFileName,
                ContentType = contentType,
                FileSize = fileSize,
                Category = category,
                Description = description,
                UploadedBy = uploadedBy,
                UploadedAt = DateTime.UtcNow,
                CompanyId = GetCompanyId(),
                TenantId = _tenantContext.TenantId
            };

            switch (source)
            {
                case AttachmentSource.WorkOrder:
                    attachment.WorkOrderId = sourceId;
                    break;
                case AttachmentSource.CipProject:
                    attachment.CipProjectId = sourceId;
                    break;
                case AttachmentSource.CipCost:
                    attachment.CipCostId = sourceId;
                    break;
                case AttachmentSource.AssetTransfer:
                    attachment.AssetTransferId = sourceId;
                    break;
                case AttachmentSource.CapitalImprovement:
                    attachment.CapitalImprovementId = sourceId;
                    break;
            }

            _context.Attachments.Add(attachment);
            await _context.SaveChangesAsync();

            return attachment;
        }

        public async Task<List<Attachment>> GetByAssetAsync(int assetId)
        {
            var companyId = GetCompanyId();
            return await _context.Attachments
                .Where(a => a.AssetId == assetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();
        }

        public async Task<List<Attachment>> GetByWorkOrderAsync(int eventId)
        {
            var companyId = GetCompanyId();
            return await _context.Attachments
                .Where(a => a.WorkOrderId == eventId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();
        }

        public async Task<List<Attachment>> GetByCipProjectAsync(int projectId)
        {
            var companyId = GetCompanyId();
            return await _context.Attachments
                .Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (a.CipProjectId == projectId || a.CipCostId != null && a.CipCost!.CipProjectId == projectId))
                .Include(a => a.CipCost)
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();
        }

        public async Task<List<Attachment>> GetByAssetTransferAsync(int transferId)
        {
            var companyId = GetCompanyId();
            return await _context.Attachments
                .Where(a => a.AssetTransferId == transferId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();
        }

        public async Task<List<Attachment>> GetByCapitalImprovementAsync(int improvementId)
        {
            var companyId = GetCompanyId();
            return await _context.Attachments
                .Where(a => a.CapitalImprovementId == improvementId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();
        }

        public async Task<Attachment?> GetByIdAsync(int id)
        {
            var companyId = GetCompanyId();
            return await _context.Attachments
                .Where(a => a.Id == id && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .FirstOrDefaultAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var companyId = GetCompanyId();
            var attachment = await _context.Attachments
                .Where(a => a.Id == id && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (attachment == null)
                return false;

            var filePath = Path.Combine(_uploadsPath, attachment.StoredFileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _context.Attachments.Remove(attachment);
            await _context.SaveChangesAsync();
            return true;
        }

        public bool IsImageFile(string contentType)
        {
            return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }

        public string GetFileIcon(string contentType)
        {
            if (contentType.StartsWith("image/")) return "fa-file-image";
            if (contentType.Contains("pdf")) return "fa-file-pdf";
            if (contentType.Contains("word") || contentType.Contains("document")) return "fa-file-word";
            if (contentType.Contains("excel") || contentType.Contains("spreadsheet")) return "fa-file-excel";
            return "fa-file";
        }

        public string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }
}
