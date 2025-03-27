using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Resilience.CircuitBreaker
{
    public class BasicCircuitBreaker : ICircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _openTimeout;
        private int _failureCount = 0;
        private CircuitBreakerState _state = CircuitBreakerState.Closed;
        private DateTime _lastStateChangedUtc = DateTime.UtcNow;

        // Async-friendly locking mechanism
        private readonly SemaphoreSlim _stateSemaphore = new SemaphoreSlim(1, 1);

        // Optional distributed store integration (e.g., Redis)
        private readonly IDistributedCache? _distributedCache;

        // Event for monitoring state changes
        public event Action<CircuitBreakerState>? OnStateChange;

        public BasicCircuitBreaker(CircuitBreakerOptions options, IDistributedCache? distributedCache = null)
        {
            _failureThreshold = options.FailureThreshold;
            _openTimeout = options.OpenTimeout;
            _distributedCache = distributedCache;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, Func<Task<T>> fallback = default!)
        {
            await _stateSemaphore.WaitAsync();
            try
            {
                // Check and transition state
                if (await GetCurrentStateAsync() == CircuitBreakerState.Open)
                {
                    if (DateTime.UtcNow - _lastStateChangedUtc > _openTimeout)
                    {
                        await SetStateAsync(CircuitBreakerState.HalfOpen); // Move to Half-Open
                    }
                    else
                    {
                        if (fallback != null)
                        {
                            return await fallback();
                        }
                        throw new CircuitBreakerOpenException("Circuit breaker is open.");
                    }
                }
            }
            finally
            {
                _stateSemaphore.Release();
            }

            try
            {
                T result = await action();
                await ResetAsync(); // Reset state if action succeeds
                return result;
            }
            catch
            {
                await IncrementFailureAsync(); // Increment failure count
                throw;
            }
        }

        private async Task ResetAsync()
        {
            await _stateSemaphore.WaitAsync();
            try
            {
                await SetStateAsync(CircuitBreakerState.Closed);
                _failureCount = 0;

                if (_distributedCache != null)
                {
                    await _distributedCache.RemoveAsync("circuit-breaker-failures");
                }
            }
            finally
            {
                _stateSemaphore.Release();
            }
        }

        private async Task IncrementFailureAsync()
        {
            await _stateSemaphore.WaitAsync();
            try
            {
                _failureCount++;

                if (_distributedCache != null)
                {
                    var currentFailureCount = await _distributedCache.GetStringAsync("circuit-breaker-failures");
                    int failureCount = string.IsNullOrEmpty(currentFailureCount) ? 0 : int.Parse(currentFailureCount);
                    failureCount++;
                    await _distributedCache.SetStringAsync("circuit-breaker-failures", failureCount.ToString());
                }

                if (_failureCount >= _failureThreshold)
                {
                    await SetStateAsync(CircuitBreakerState.Open);
                }
            }
            finally
            {
                _stateSemaphore.Release();
            }
        }

        private async Task<CircuitBreakerState> GetCurrentStateAsync()
        {
            if (_distributedCache != null)
            {
                var stateValue = await _distributedCache.GetStringAsync("circuit-breaker-state");
                if (Enum.TryParse<CircuitBreakerState>(stateValue, out var state))
                {
                    _state = state;
                }
            }
            return _state;
        }

        private async Task SetStateAsync(CircuitBreakerState newState)
        {
            _state = newState;
            _lastStateChangedUtc = DateTime.UtcNow;

            OnStateChange?.Invoke(_state); // Notify listeners of state change

            if (_distributedCache != null)
            {
                await _distributedCache.SetStringAsync("circuit-breaker-state", _state.ToString());
                await _distributedCache.SetStringAsync("circuit-breaker-last-changed", _lastStateChangedUtc.ToString("O"));
            }
        }
    }
}
