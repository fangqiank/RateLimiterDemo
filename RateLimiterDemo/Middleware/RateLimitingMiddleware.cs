using RateLimiterDemo.Models;

namespace RateLimiterDemo.Middleware
{
    public class RateLimitingMiddleware(
        RequestDelegate next, 
        ILogger<RateLimitingMiddleware> logger
        )
    {
        public async Task InvokeAsync(
            HttpContext context,
            FixedWindowRateLimiter rateLimiter)
        {
            var clientId = GetClientIdentifier(context);
            var configuration = GetRateLimitConfiguration(context);

            var result = await rateLimiter.CheckRateLimitAsync(
                clientId,
                configuration.MaxRequests,
                configuration.WindowSizeInSeconds);

            context.Response.Headers["X-RateLimit-Limit"] = configuration.MaxRequests.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = result.RemainingRequests.ToString();

            if (!result.IsAllowed)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (result.RetryAfter.HasValue)
                    context.Response.Headers.RetryAfter = result.RetryAfter.Value.ToString("R");

                context.Response.ContentType = "application/json";

                var response = new
                {
                    error = "Too Many Requests",
                    retryAfter = result.RetryAfter?.ToString("o"),
                    message = $"Rate limit exceeded. Try again in {result.RetryAfter?.Subtract(DateTime.UtcNow).TotalSeconds ?? 0:F0} seconds"
                };

                await context.Response.WriteAsJsonAsync(response);
                return;
            }

            await next(context);
        }

        private static RateLimitConfiguration GetRateLimitConfiguration(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            var policyName = endpoint?.Metadata
                .GetMetadata<RateLimitPolicyAttribute>()?.PolicyName ?? "default";

            return policyName switch
            {
                "strict" => new RateLimitConfiguration { MaxRequests = 10, WindowSizeInSeconds = 60, PolicyName = "strict" },
                "relaxed" => new RateLimitConfiguration { MaxRequests = 1000, WindowSizeInSeconds = 60, PolicyName = "relaxed" },
                _ => new RateLimitConfiguration { MaxRequests = 100, WindowSizeInSeconds = 60, PolicyName = "default" },
            };
        }

        private static string GetClientIdentifier(HttpContext context)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
                return context.User.Identity.Name;

            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            return forwardedFor ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public class RateLimitPolicyAttribute: Attribute
    {
        public string PolicyName { get; set; } = "default";
    }
}


