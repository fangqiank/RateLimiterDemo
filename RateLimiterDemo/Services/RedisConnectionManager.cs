using StackExchange.Redis;

namespace RateLimiterDemo.Services
{
    public class RedisConnectionManager
    {
        private readonly IConnectionMultiplexer _connection;
        private readonly ILogger<RedisConnectionManager> _logger;

        public RedisConnectionManager(
            IConnectionMultiplexer connection, 
            ILogger<RedisConnectionManager> logger)
        {
            _connection = connection;
            _logger = logger;

            _connection.ConnectionFailed += (sender, args) =>
            {
                _logger.LogError($"Redis connection failed: {args.FailureType}, {args.Exception}");
            };

            _connection.ConnectionRestored += (sender, args) =>
            {
                _logger.LogInformation("Redis connection restored.");
            };
        }

        public IConnectionMultiplexer Connection => _connection;
        public IDatabase Database => _connection.GetDatabase();
    }
}
