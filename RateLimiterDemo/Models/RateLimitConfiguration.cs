namespace RateLimiterDemo.Models
{
    public class RateLimitConfiguration
    {
        public int MaxRequests { get; set; } = 100;
        public int WindowSizeInSeconds { get; set; } = 60;
        public string PolicyName { get; set; } = "default";
    }
}
