using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services
{
    public record ImageValidationResult(bool Success, string? ErrorMessage = null);

    public enum ItemImageSource
    {
        Internal,
        PreferredVendor,
        OtherVendor,
        Placeholder
    }

    public interface IItemImageService
    {
        Task<string?> UploadImageAsync(int itemId, IFormFile file, int? tenantId = null);
        bool DeleteImage(string? imagePath);
        string GetImageUrl(string? imagePath, string? externalImageUrl, string? legacyImageUrl);
        (string path, ItemImageSource source, string? caption) GetItemImageWithSource(int itemId, string? imagePath, string? preferredVendorImageUrl, string? otherVendorImageUrl = null);
        bool IsLabEnvironment();
        ImageValidationResult ValidateImageFile(IFormFile? file);
        string GetSanitizedFileName(string fileName);
    }

    public class ItemImageService : IItemImageService
    {
        private readonly ILogger<ItemImageService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly string _basePath;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024;

        public ItemImageService(ILogger<ItemImageService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
            _basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "items");
        }

        public bool IsLabEnvironment() => _env.IsDevelopment();

        public ImageValidationResult ValidateImageFile(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return new ImageValidationResult(false, "No file provided");

            if (file.Length > MaxFileSizeBytes)
                return new ImageValidationResult(false, $"File too large. Max size is {MaxFileSizeBytes / (1024 * 1024)}MB");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
                return new ImageValidationResult(false, $"Invalid file extension. Allowed: {string.Join(", ", _allowedExtensions)}");

            return new ImageValidationResult(true);
        }

        public string GetSanitizedFileName(string fileName) => SanitizeFileName(fileName);

        public async Task<string?> UploadImageAsync(int itemId, IFormFile file, int? tenantId = null)
        {
            try
            {
                var validation = ValidateImageFile(file);
                if (!validation.Success)
                {
                    _logger.LogWarning("Image validation failed for item {ItemId}: {Error}", itemId, validation.ErrorMessage);
                    return null;
                }

                var extension = Path.GetExtension(file!.FileName).ToLowerInvariant();
                var tenantFolder = tenantId?.ToString() ?? "default";
                var uploadPath = Path.Combine(_basePath, tenantFolder, itemId.ToString());
                
                var normalizedBasePath = Path.GetFullPath(_basePath);
                var normalizedUploadPath = Path.GetFullPath(uploadPath);
                if (!normalizedUploadPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Path traversal attempt detected in upload path for item {ItemId}", itemId);
                    return null;
                }
                
                Directory.CreateDirectory(normalizedUploadPath);

                var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(normalizedUploadPath, uniqueFileName);
                
                var normalizedFilePath = Path.GetFullPath(filePath);
                if (!normalizedFilePath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Path traversal attempt detected in file path for item {ItemId}", itemId);
                    return null;
                }

                using (var stream = new FileStream(normalizedFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = $"/uploads/items/{tenantFolder}/{itemId}/{uniqueFileName}";
                _logger.LogInformation("Image uploaded for item {ItemId}: {Path}", itemId, relativePath);
                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image for item {ItemId}", itemId);
                return null;
            }
        }

        public bool DeleteImage(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return false;

            try
            {
                if (!imagePath.StartsWith("/uploads/items/"))
                {
                    _logger.LogWarning("DeleteImage rejected - invalid path prefix: {Path}", imagePath);
                    return false;
                }

                var relativePath = imagePath.TrimStart('/');
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);
                
                var normalizedBasePath = Path.GetFullPath(_basePath);
                var normalizedFullPath = Path.GetFullPath(fullPath);

                if (!normalizedFullPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("DeleteImage rejected - path traversal attempt: {Path} -> {Normalized}", imagePath, normalizedFullPath);
                    return false;
                }

                if (File.Exists(normalizedFullPath))
                {
                    File.Delete(normalizedFullPath);
                    _logger.LogInformation("Deleted image: {Path}", imagePath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deleting image: {Path}", imagePath);
            }

            return false;
        }

        public string GetImageUrl(string? imagePath, string? externalImageUrl, string? legacyImageUrl)
        {
            if (!string.IsNullOrEmpty(imagePath))
                return imagePath;
            if (!string.IsNullOrEmpty(externalImageUrl))
                return externalImageUrl;
            if (!string.IsNullOrEmpty(legacyImageUrl))
                return legacyImageUrl;
            return string.Empty;
        }

        public (string path, ItemImageSource source, string? caption) GetItemImageWithSource(int itemId, string? imagePath, string? preferredVendorImageUrl, string? otherVendorImageUrl = null)
        {
            const string PlaceholderPath = "/images/item-placeholder.png";

            if (!string.IsNullOrWhiteSpace(imagePath))
            {
                return (imagePath, ItemImageSource.Internal, null);
            }

            if (!string.IsNullOrWhiteSpace(preferredVendorImageUrl))
            {
                return (preferredVendorImageUrl, ItemImageSource.PreferredVendor, "Image from preferred vendor");
            }

            if (!string.IsNullOrWhiteSpace(otherVendorImageUrl))
            {
                return (otherVendorImageUrl, ItemImageSource.OtherVendor, "Image from vendor");
            }

            return (PlaceholderPath, ItemImageSource.Placeholder, null);
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            return sanitized.Replace("..", "").Replace("//", "").Replace("\\\\", "");
        }
    }
}
