namespace Abs.FixedAssets.Middleware
{
    public class ApiHeaderEnforcementMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiHeaderEnforcementMiddleware> _logger;

        public ApiHeaderEnforcementMiddleware(RequestDelegate next, ILogger<ApiHeaderEnforcementMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";

            if (path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
            {
                var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
                var userId = context.Request.Headers["X-User-Id"].FirstOrDefault();
                var orgNodeId = context.Request.Headers["X-Org-Node-Id"].FirstOrDefault();

                var missing = new List<string>();
                if (string.IsNullOrEmpty(tenantId)) missing.Add("X-Tenant-Id");
                if (string.IsNullOrEmpty(userId)) missing.Add("X-User-Id");
                if (string.IsNullOrEmpty(orgNodeId)) missing.Add("X-Org-Node-Id");

                if (missing.Count > 0)
                {
                    _logger.LogWarning("API request to {Path} rejected: missing headers {Headers}", path, string.Join(", ", missing));
                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            error = "Missing required headers",
                            missing = missing,
                            path = path
                        }));
                    return;
                }
            }

            await _next(context);
        }
    }

    public static class ApiHeaderEnforcementMiddlewareExtensions
    {
        public static IApplicationBuilder UseApiHeaderEnforcement(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiHeaderEnforcementMiddleware>();
        }
    }
}
