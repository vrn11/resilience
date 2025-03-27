namespace Resilience.Configuration;

public class ResilienceConfiguration
{
    public ComponentConfiguration Gateways { get; set; } = default!;
    public ComponentConfiguration Microservices { get; set; } = default!;
}
