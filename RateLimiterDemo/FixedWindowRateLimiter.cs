using RateLimiterDemo.Models;
using RateLimiterDemo.Services;
using StackExchange.Redis;
using System.Threading.RateLimiting;

public class FixedWindowRateLimiter
{
    private readonly IDatabase _redis;
    private readonly ILogger<System.Threading.RateLimiting.FixedWindowRateLimiter> _logger;

    public FixedWindowRateLimiter(
        RedisConnectionManager redisManager, 
        ILogger<System.Threading.RateLimiting.FixedWindowRateLimiter> logger
        )
    {
        _redis = redisManager.Database;
        _logger = logger;
    }
    public async Task<RateLimitResult> CheckRateLimitAsync(
        string clientId,
        int maxRequests,
        int windowSizeInSeconds
        )
    {
        var currentTime = DateTimeOffset.UtcNow;

        var windowKey = GenerateWindowKey(clientId, currentTime, windowSizeInSeconds);

        var requestCount = await _redis.StringIncrementAsync(windowKey);

        if (requestCount == 1)
            await _redis.KeyExpireAsync(windowKey, TimeSpan.FromSeconds(windowSizeInSeconds));

        var remaining = Math.Max(0, maxRequests - (int)requestCount);
        var isAllowed = requestCount <= maxRequests;

        DateTime? retryAfter = null;
        if(!isAllowed)
        {
            var ttl = await _redis.KeyTimeToLiveAsync(windowKey);
            retryAfter = currentTime.Add(ttl ?? TimeSpan.FromSeconds(windowSizeInSeconds)).DateTime;
        }

        return new RateLimitResult
        {
            IsAllowed = isAllowed,
            RemainingRequests = remaining,
            RetryAfter = retryAfter
        };
    }

    private static string GenerateWindowKey(
        string clientId, 
        DateTimeOffset currentTime, 
        int windowSizeInSeconds)
    {
        var windowStart = currentTime.ToUnixTimeSeconds();
        return $"ratelimit:fixed:{clientId}:{windowStart}";
    }
}