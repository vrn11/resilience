namespace Resilience.Configuration;

public class CircuitBreakerConfiguration
{
    public string Type { get; set; } = default!;
    public CircuitBreaker.CircuitBreakerOptions Options { get; set; } = default!;
}