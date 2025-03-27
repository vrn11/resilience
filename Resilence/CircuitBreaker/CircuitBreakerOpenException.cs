namespace Resilience.CircuitBreaker;

/// <summary>
/// Exception thrown when the circuit breaker is open.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}
