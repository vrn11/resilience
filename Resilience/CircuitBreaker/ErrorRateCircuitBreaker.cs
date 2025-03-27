namespace Resilience.CircuitBreaker;
using Microsoft.Extensions.Caching.Distributed;

public class ErrorRateCircuitBreaker : ICircuitBreaker
{
    private readonly BasicCircuitBreaker _innerBreaker;

    public ErrorRateCircuitBreaker(CircuitBreakerOptions options, IDistributedCache? distributedCache = null)
    {
        // In a real implementation, you might track errors over a sliding window.
        // Here, we simply reuse the logic from BasicCircuitBreaker.
        _innerBreaker = new BasicCircuitBreaker(options, distributedCache);
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, Func<Task<T>> fallback = default!)
    {
        // In a more detailed implementation, you would calculate error rates
        // and apply additional logic before delegating to the inner breaker.
        return await _innerBreaker.ExecuteAsync(action, fallback);
    }
}
