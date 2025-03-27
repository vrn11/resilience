namespace Resilience.Configuration;

public class ComponentConfiguration
{
    public CircuitBreakerConfiguration CircuitBreaker { get; set; } = default!; 
    public LoadShedderConfiguration LoadShedder { get; set; } = default!;
}
