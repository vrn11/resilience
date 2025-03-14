namespace CircuitBreaker;

/// <summary>
/// A generic circuit breaker that monitors an asynchronous action, 
/// tracks failures, and opens the circuit when failures exceed a threshold.
/// </summary>
public class CircuitBreaker : ICircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openTimeout;
    private int _failureCount = 0;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private DateTime _lastStateChangedUtc = DateTime.UtcNow;
    private readonly object _stateLock = new object();

    public CircuitBreaker(CircuitBreakerOptions options)
    {
        _failureThreshold = options.FailureThreshold;
        _openTimeout = options.OpenTimeout;
    }

    /// <summary>
    /// Executes an asynchronous action under the protection of the circuit breaker.
    /// </summary>
    /// <typeparam name="T">Return type of the action.</typeparam>
    /// <param name="action">The protected action.</param>
    /// <param name="fallback">Optional fallback action used when the circuit is open or the action fails.</param>
    /// <returns>The result of the action or fallback.</returns>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, Func<Task<T>> fallback = default!)
    {
        Func<Task<T>> localFallback = default!;

        // Use a lock for checking/updating state.
        lock (_stateLock)
        {
            if (_state == CircuitBreakerState.Open)
            {
                // If the timeout has elapsed, allow a trial execution.
                if (DateTime.UtcNow - _lastStateChangedUtc > _openTimeout)
                {
                    _state = CircuitBreakerState.HalfOpen;
                }
                else
                {
                    // If fallback is provided, capture it for asynchronous call outside the lock.
                    if (fallback != null)
                    {
                        localFallback = fallback;
                    }
                    else
                    {
                        throw new CircuitBreakerOpenException("Circuit breaker is open.");
                    }
                }
            }
        }

        if (localFallback != null)
            return await localFallback();

        try
        {
            T result = await action();

            lock (_stateLock)
            {
                if (_state == CircuitBreakerState.HalfOpen)
                {
                    Reset();
                }
                else if (_state == CircuitBreakerState.Closed)
                {
                    _failureCount = 0;
                }
            }
            return result;
        }
        catch (Exception)
        {
            lock (_stateLock)
            {
                _failureCount++;
                if (_failureCount >= _failureThreshold)
                {
                    _state = CircuitBreakerState.Open;
                    _lastStateChangedUtc = DateTime.UtcNow;
                }
            }
            if (fallback != null)
                return await fallback();

            throw;
        }
    }

    private void Reset()
    {
        _state = CircuitBreakerState.Closed;
        _failureCount = 0;
        _lastStateChangedUtc = DateTime.UtcNow;
    }
}


