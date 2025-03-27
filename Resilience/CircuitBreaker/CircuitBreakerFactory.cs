namespace Resilience.CircuitBreaker;

public static class CircuitBreakerFactory
{
    public static ICircuitBreaker Create(string type, CircuitBreakerOptions options)
    {
        switch (type.ToLowerInvariant())
        {
            case "errorrate":
                return new ErrorRateCircuitBreaker(options);
            case "basic":
            default:
                return new BasicCircuitBreaker(options);
        }
    }
}