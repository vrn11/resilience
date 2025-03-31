namespace Resilience;

public class CachingOptions
{
    public string ConnectionString { get; set; } = default!;
    public string CacheName { get; set; } = default!;
    public string CacheType { get; set; } = default!;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
}
