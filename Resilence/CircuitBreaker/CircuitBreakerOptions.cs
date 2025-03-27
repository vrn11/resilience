namespace Resilience.CircuitBreaker;

/// <summary>
/// Options used to configure the circuit breaker.
/// </summary>
public class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 3;
    public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromSeconds(5);
}