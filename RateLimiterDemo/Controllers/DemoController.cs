using Microsoft.AspNetCore.Mvc;
using RateLimiterDemo.Middleware;
using RateLimiterDemo.Models;

namespace RateLimiterDemo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DemoController(
        FixedWindowRateLimiter fixedWindowRateLimiter,
        SlideWindowRateLimiter slidingWindowRateLimiter,
        LuaScriptRateLimiter luaScriptRateLimiter,
        ILogger<DemoController> logger
        ) : ControllerBase
    {
        [HttpGet("fixed-window")]
        [RateLimitPolicy(PolicyName = "strict")]
        public async Task<IActionResult> FixedWindowDemo()
        {
            var clientId = HttpContext.User?.Identity?.IsAuthenticated == true
                ? HttpContext.User.Identity.Name!
                : HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                  ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                  ?? "unknown";

            var result = await fixedWindowRateLimiter.CheckRateLimitAsync(clientId, 10, 60);

            logger.LogInformation("FixedWindow check: clientId={ClientId}, allowed={IsAllowed}, remaining={Remaining}",
                clientId, result.IsAllowed, result.RemainingRequests);

            if (!result.IsAllowed)
            {
                Response.StatusCode = 429;
                Response.Headers.RetryAfter = result.RetryAfter?.ToString("R");

                return new JsonResult(new
                {
                    error = "Too Many Requests",
                    retryAfter = result.RetryAfter?.ToString("o")
                });
            }

            return Ok(new
            {
                message = "Request processed successfully",
                algorithm = "Fixed Window",
                remaining = result.RemainingRequests,
                clientId,
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }

        [HttpGet("sliding-window-test")]
        public async Task<IActionResult> SlidingWindowTest([FromQuery] string clientId = "test-user", [FromQuery] int max = 5)
        {
            var config = new RateLimitConfiguration
            {
                MaxRequests = max,
                WindowSizeInSeconds = 60
            };

            var result = await slidingWindowRateLimiter.CheckRateLimitAsync(
                clientId,
                config.MaxRequests,
                config.WindowSizeInSeconds);

            if (!result.IsAllowed)
            {
                Response.StatusCode = 429;
                Response.Headers.RetryAfter = result.RetryAfter?.ToString("R");

                return new JsonResult(new
                {
                    error = "Too Many Requests",
                    retryAfter = result.RetryAfter?.ToString("o")
                });
            }

            return Ok(new
            {
                message = "Request successful",
                algorithm = "Sliding Window",
                remaining = result.RemainingRequests,
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }

        [HttpGet("token-bucket-test")]
        public async Task<IActionResult> TokenBucketTest([FromQuery] string clientId = "test-user")
        {
            var result = await luaScriptRateLimiter.CheckRateLimitAsync(
                clientId,
                maxTokens: 10,
                refillRate: 5,
                refillIntervalInSeconds: 60);

            if (!result.IsAllowed)
            {
                Response.StatusCode = 429;
                Response.Headers.RetryAfter = result.RetryAfter?.ToString("R");

                return new JsonResult(new
                {
                    error = "Too Many Requests",
                    retryAfter = result.RetryAfter?.ToString("o")
                });
            }

            return Ok(new
            {
                message = "Request successful",
                algorithm = "Token Bucket (Lua Script)",
                remaining = result.RemainingRequests,
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            return Ok(new
            {
                server = Environment.MachineName,
                timestamp = DateTime.UtcNow.ToString("o"),
                message = "Server is running"
            });
        }
    }
}
