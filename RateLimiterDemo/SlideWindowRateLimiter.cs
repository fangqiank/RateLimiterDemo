using RateLimiterDemo.Models;
using RateLimiterDemo.Services;
using StackExchange.Redis;
using System.Threading.RateLimiting;

public class SlideWindowRateLimiter
{
    private readonly IDatabase _redis;
    private readonly ILogger<SlidingWindowRateLimiter> _logger;

    public SlideWindowRateLimiter(
        RedisConnectionManager redisManager, 
        ILogger<SlidingWindowRateLimiter> logger
        )
    {
        _redis = redisManager.Database;
        _logger = logger;
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(
        string clientId,
        int maxRequests,
        int windowSizeInSeconds)
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddSeconds(-windowSizeInSeconds);
        var key = $"ratelimit:sliding:{clientId}";

        var member = $"{now.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}";
        var score = now.ToUnixTimeMilliseconds();

        var batch = _redis.CreateBatch();
        var removeOldTask = batch.SortedSetRemoveRangeByScoreAsync(
            key, 
            0, 
            windowStart.ToUnixTimeMilliseconds());
        var addTask = batch.SortedSetAddAsync(key, member, score);
        var countTask = batch.SortedSetLengthAsync(key);
        var expireTask = batch.KeyExpireAsync(key, TimeSpan.FromSeconds(windowSizeInSeconds));

        batch.Execute();

        await Task.WhenAll(removeOldTask, addTask, countTask, expireTask);
        var requestCount = (int)await countTask;

        var isAllowed = requestCount <= maxRequests;
        var remaining = Math.Max(0, maxRequests - requestCount);

        DateTime? retryAfter = null;

        if (!isAllowed)
        {
            // Remove the last added request since it exceeded the limit
            await _redis.SortedSetRemoveAsync(key, member);

            // Calculate retry-after based on oldest request in the window
            var oldestRequest = await _redis.SortedSetRangeByScoreWithScoresAsync(key, 0, double.MaxValue, take: 1);
            if (oldestRequest.Length > 0)
            {
                var oldestTimestamp = oldestRequest[0].Score;
                var retryAfterSeconds = (oldestTimestamp / 1000) + windowSizeInSeconds -
                                        DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                retryAfter = DateTime.UtcNow.AddSeconds(Math.Max(0, retryAfterSeconds));
            }
        }

        return new RateLimitResult
        {
            IsAllowed = isAllowed,
            RemainingRequests = remaining,
            RetryAfter = retryAfter
        };
    }
}
