using RateLimiterDemo.Models;
using RateLimiterDemo.Services;
using StackExchange.Redis;

public class LuaScriptRateLimiter
{
    private readonly IDatabase _redis;
    private readonly ILogger<LuaScriptRateLimiter> _logger;
    private readonly string _tokenBucketScript;
    private readonly LoadedLuaScript _preparedScript;

    public LuaScriptRateLimiter(
        RedisConnectionManager redisConnectionManager,
        ILogger<LuaScriptRateLimiter> logger)
    {
        _redis = redisConnectionManager.Database;
        _logger = logger;

        // Lua script for token bucket algorithm
        _tokenBucketScript = @"
            local key = KEYS[1]
            local max_tokens = tonumber(ARGV[1])
            local refill_rate = tonumber(ARGV[2])
            local refill_interval = tonumber(ARGV[3])
            local current_time = tonumber(ARGV[4])
            
            -- Get current bucket state
            local bucket = redis.call('HMGET', key, 'tokens', 'last_refill')
            local tokens = tonumber(bucket[1]) or max_tokens
            local last_refill = tonumber(bucket[2]) or current_time
            
            -- Calculate tokens to add
            local elapsed = current_time - last_refill
            local tokens_to_add = elapsed * (refill_rate / refill_interval)
            tokens = math.min(max_tokens, tokens + tokens_to_add)
            
            -- Check if request can be processed
            if tokens >= 1 then
                tokens = tokens - 1
                redis.call('HMSET', key, 'tokens', tokens, 'last_refill', current_time)
                redis.call('EXPIRE', key, math.ceil(max_tokens / (refill_rate / refill_interval)))
                return {1, tokens}
            else
                redis.call('EXPIRE', key, math.ceil(max_tokens / (refill_rate / refill_interval)))
                return {0, 0}
            end
        ";

        var server = redisConnectionManager.Connection.GetServer(
            redisConnectionManager.Connection.GetEndPoints().First());

        _preparedScript = LuaScript.Prepare(_tokenBucketScript).Load(server);
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(
        string clientId,
        int maxTokens,
        int refillRate,
        int refillIntervalInSeconds)
    {
        var key = $"ratelimit:tokenbucket:{clientId}";
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await _redis.ScriptEvaluateAsync(
            _preparedScript.Hash,
            new RedisKey[] { key },
            new RedisValue[]
            {
                maxTokens,
                refillRate,
                refillIntervalInSeconds,
                currentTime
            });

        var values = (RedisResult[])result;
        var isAllowed = (int)values[0] == 1;
        var remainingTokens = (int)values[1];

        return new RateLimitResult
        {
            IsAllowed = isAllowed,
            RemainingRequests = remainingTokens,
            RetryAfter = isAllowed ? null : DateTime.UtcNow.AddSeconds(1)
        };
    }
}