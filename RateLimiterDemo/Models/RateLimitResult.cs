namespace RateLimiterDemo.Models
{
    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public int RemainingRequests { get; set; }
        public DateTime? RetryAfter { get; set; }
    }
}
