namespace Resilience.CircuitBreaker;

/// <summary>
/// A generic circuit breaker interface.
/// </summary>
public interface ICircuitBreaker
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, Func<Task<T>> fallback = default!);
}
