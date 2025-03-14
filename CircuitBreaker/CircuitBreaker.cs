namespace CircuitBreaker;

public class CircuitBreaker
{
    private readonly int failureThreshold;
    private readonly TimeSpan openTimeout;
    private int failureCount = 0;
    private CircuitBreakerState state = CircuitBreakerState.Closed;
    private DateTime lastStateChangedUtc = DateTime.UtcNow;
    private readonly object stateLock = new object();

    public CircuitBreaker(int failureThreshold, TimeSpan openTimeout)
    {
        this.failureThreshold = failureThreshold;
        this.openTimeout = openTimeout;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, Func<Task<T>> fallback = default!)
    {
        Func<Task<T>> localFallback = default!;

        // Lock for checking/updating state
        lock (stateLock)
        {
            if (state == CircuitBreakerState.Open)
            {
                // Check if timeout elapsed to allow half-open trial execution.
                if (DateTime.UtcNow - lastStateChangedUtc > openTimeout)
                {
                    state = CircuitBreakerState.HalfOpen;
                }
                else
                {
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

            lock (stateLock)
            {
                if (state == CircuitBreakerState.HalfOpen)
                {
                    Reset();
                }
                else if (state == CircuitBreakerState.Closed)
                {
                    failureCount = 0;
                }
            }

            return result;
        }
        catch (Exception)
        {
            lock (stateLock)
            {
                failureCount++;
                if (failureCount >= failureThreshold)
                {
                    state = CircuitBreakerState.Open;
                    lastStateChangedUtc = DateTime.UtcNow;
                }
            }
            if (fallback != null)
                return await fallback();

            throw;
        }
    }

    private void Reset()
    {
        state = CircuitBreakerState.Closed;
        failureCount = 0;
        lastStateChangedUtc = DateTime.UtcNow;
    }
}


