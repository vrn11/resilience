namespace Resilience.CircuitBreaker;

public class CircuitBreakerCacheState
{
    public string State { get; set; } = string.Empty;
    public string LastChangedUtc { get; set; } = string.Empty;
}
