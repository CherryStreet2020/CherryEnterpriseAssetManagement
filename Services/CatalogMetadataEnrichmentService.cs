using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services
{
    public class CatalogMetadata
    {
        public string? CanonicalUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string? Title { get; set; }
        public string? Mpn { get; set; }
        public string? Sku { get; set; }
        public string Status { get; set; } = "Unknown";
        public string? ErrorMessage { get; set; }
    }

    public interface ICatalogMetadataEnrichmentService
    {
        Task<CatalogMetadata> EnrichFromUrlAsync(string url);
        CatalogMetadata ParseHtml(string html, string originalUrl);
        bool IsLabEnvironment();
    }

    public class CatalogMetadataEnrichmentService : ICatalogMetadataEnrichmentService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CatalogMetadataEnrichmentService> _logger;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        public CatalogMetadataEnrichmentService(
            IHttpClientFactory httpClientFactory,
            ILogger<CatalogMetadataEnrichmentService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public bool IsLabEnvironment()
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            return env == "Development" || env == "LAB";
        }

        public async Task<CatalogMetadata> EnrichFromUrlAsync(string url)
        {
            var result = new CatalogMetadata();

            if (!IsLabEnvironment())
            {
                result.Status = "Disabled";
                result.ErrorMessage = "Catalog enrichment is disabled in this environment.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                result.Status = "InvalidUrl";
                result.ErrorMessage = "URL is empty.";
                return result;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                result.Status = "InvalidUrl";
                result.ErrorMessage = "Invalid URL format.";
                return result;
            }

            if (uri.Scheme != "https")
            {
                result.Status = "InvalidUrl";
                result.ErrorMessage = "Only HTTPS URLs are allowed.";
                return result;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = RequestTimeout;
                client.DefaultRequestHeaders.Add("User-Agent", "CherryAI-Catalog-Enrichment/1.0");
                client.DefaultRequestHeaders.Add("Accept", "text/html");

                var response = await client.GetAsync(uri);
                
                if (!response.IsSuccessStatusCode)
                {
                    result.Status = "FetchFailed";
                    result.ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                    return result;
                }

                var html = await response.Content.ReadAsStringAsync();
                return ParseHtml(html, url);
            }
            catch (TaskCanceledException)
            {
                result.Status = "Timeout";
                result.ErrorMessage = "Request timed out.";
                return result;
            }
            catch (HttpRequestException ex)
            {
                result.Status = "FetchFailed";
                result.ErrorMessage = $"Network error: {ex.Message}";
                _logger.LogWarning(ex, "Failed to fetch catalog URL: {Url}", url);
                return result;
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.ErrorMessage = "An unexpected error occurred.";
                _logger.LogError(ex, "Error enriching from catalog URL: {Url}", url);
                return result;
            }
        }

        public CatalogMetadata ParseHtml(string html, string originalUrl)
        {
            var result = new CatalogMetadata { CanonicalUrl = originalUrl };

            try
            {
                result.CanonicalUrl = ExtractMetaContent(html, @"<link[^>]+rel=[""']canonical[""'][^>]+href=[""']([^""']+)[""']") 
                                   ?? ExtractMetaContent(html, @"<link[^>]+href=[""']([^""']+)[""'][^>]+rel=[""']canonical[""']")
                                   ?? originalUrl;

                result.ImageUrl = ExtractOpenGraphContent(html, "og:image");
                result.Title = ExtractOpenGraphContent(html, "og:title");

                var jsonLd = ExtractJsonLd(html);
                if (jsonLd != null)
                {
                    result.Mpn = jsonLd.Mpn ?? result.Mpn;
                    result.Sku = jsonLd.Sku ?? result.Sku;
                    if (string.IsNullOrEmpty(result.ImageUrl) && !string.IsNullOrEmpty(jsonLd.Image))
                        result.ImageUrl = jsonLd.Image;
                    if (string.IsNullOrEmpty(result.Title) && !string.IsNullOrEmpty(jsonLd.Name))
                        result.Title = jsonLd.Name;
                }

                result.Status = !string.IsNullOrEmpty(result.ImageUrl) || !string.IsNullOrEmpty(result.Mpn)
                    ? "Success"
                    : "NoMetadata";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing HTML for catalog metadata");
                result.Status = "ParseError";
                result.ErrorMessage = "Failed to parse page content.";
            }

            return result;
        }

        private static string? ExtractOpenGraphContent(string html, string property)
        {
            var pattern = $@"<meta[^>]+property=[""']{Regex.Escape(property)}[""'][^>]+content=[""']([^""']+)[""']";
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
                return DecodeHtmlEntities(match.Groups[1].Value);

            pattern = $@"<meta[^>]+content=[""']([^""']+)[""'][^>]+property=[""']{Regex.Escape(property)}[""']";
            match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? DecodeHtmlEntities(match.Groups[1].Value) : null;
        }

        private static string? ExtractMetaContent(string html, string pattern)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? DecodeHtmlEntities(match.Groups[1].Value) : null;
        }

        private JsonLdProduct? ExtractJsonLd(string html)
        {
            try
            {
                var pattern = @"<script[^>]+type=[""']application/ld\+json[""'][^>]*>(.*?)</script>";
                var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match match in matches)
                {
                    var json = match.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(json)) continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in root.EnumerateArray())
                            {
                                var product = TryParseProduct(item);
                                if (product != null) return product;
                            }
                        }
                        else
                        {
                            var product = TryParseProduct(root);
                            if (product != null) return product;
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error extracting JSON-LD");
            }

            return null;
        }

        private static JsonLdProduct? TryParseProduct(JsonElement element)
        {
            if (!element.TryGetProperty("@type", out var typeElement))
                return null;

            var type = typeElement.GetString();
            if (type != "Product") return null;

            var product = new JsonLdProduct();

            if (element.TryGetProperty("mpn", out var mpn))
                product.Mpn = mpn.GetString();

            if (element.TryGetProperty("sku", out var sku))
                product.Sku = sku.GetString();

            if (element.TryGetProperty("name", out var name))
                product.Name = name.GetString();

            if (element.TryGetProperty("image", out var image))
            {
                if (image.ValueKind == JsonValueKind.String)
                    product.Image = image.GetString();
                else if (image.ValueKind == JsonValueKind.Array && image.GetArrayLength() > 0)
                    product.Image = image[0].GetString();
                else if (image.ValueKind == JsonValueKind.Object && image.TryGetProperty("url", out var imgUrl))
                    product.Image = imgUrl.GetString();
            }

            return product;
        }

        private static string DecodeHtmlEntities(string value)
        {
            return System.Net.WebUtility.HtmlDecode(value);
        }

        private class JsonLdProduct
        {
            public string? Mpn { get; set; }
            public string? Sku { get; set; }
            public string? Name { get; set; }
            public string? Image { get; set; }
        }
    }
}
