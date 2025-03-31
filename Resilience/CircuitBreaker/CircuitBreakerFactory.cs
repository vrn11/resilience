namespace Resilience.CircuitBreaker;

using Microsoft.Extensions.Caching.Distributed;
using Resilience.Caching;

public static class CircuitBreakerFactory
{
    public static ICircuitBreaker Create(string type, CircuitBreakerOptions options, IResilienceDistributedCache? distributedCache = null)
    {
        switch (type.ToLowerInvariant())
        {
            case "errorrate":
                return new ErrorRateCircuitBreaker(options, distributedCache);
            case "basic":
            default:
                return new BasicCircuitBreaker(options, distributedCache);
        }
    }
}