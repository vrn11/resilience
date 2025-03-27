namespace Resilience.CircuitBreaker;

/// <summary>
/// Represents the current state of the circuit breaker.
/// </summary>
public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}
