namespace Resilience.CircuitBreaker;

/// <summary>
    /// A basic circuit breaker implementation.
    /// </summary>
    public class BasicCircuitBreaker : ICircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _openTimeout;
        private int _failureCount = 0;
        private CircuitBreakerState _state = CircuitBreakerState.Closed;
        private DateTime _lastStateChangedUtc = DateTime.UtcNow;
        private readonly object _stateLock = new object();

        public BasicCircuitBreaker(CircuitBreakerOptions options)
        {
            _failureThreshold = options.FailureThreshold;
            _openTimeout = options.OpenTimeout;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, Func<Task<T>> fallback = default!)
        {
            Func<Task<T>> localFallback = default!;
            lock (_stateLock)
            {
                if (_state == CircuitBreakerState.Open)
                {
                    if (DateTime.UtcNow - _lastStateChangedUtc > _openTimeout)
                    {
                        _state = CircuitBreakerState.HalfOpen;
                    }
                    else
                    {
                        if (fallback != null)
                        {
                            localFallback = fallback;
                        }
                        else
                        {
                            throw new CircuitBreakerOpenException("BasicCircuitBreaker is open.");
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
