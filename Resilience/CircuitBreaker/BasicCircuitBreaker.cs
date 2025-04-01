using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Resilience.Caching;

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
        private readonly IResilienceDistributedCache? _resilienceCache;

        // Event for monitoring state changes
        public event Action<CircuitBreakerState>? OnStateChange;

        public BasicCircuitBreaker(CircuitBreakerOptions options, IResilienceDistributedCache? resilienceCache = null)
        {
            _failureThreshold = options.FailureThreshold;
            _openTimeout = options.OpenTimeout;
            _resilienceCache = resilienceCache;
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

                // Execute fallback if provided
                if (fallback != null)
                {
                    return await fallback();
                }

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

                if (_resilienceCache != null)
                {
                    await _resilienceCache.RemoveAsync("circuit-breaker-failures");
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

                if (_failureCount >= _failureThreshold)
                {
                    await SetStateAsync(CircuitBreakerState.Open);
                }

                if (_resilienceCache != null)
                {
                    await _resilienceCache.IncrementFailuresAsync();
                }
            }
            finally
            {
                _stateSemaphore.Release();
            }
        }

        private async Task<CircuitBreakerState> GetCurrentStateAsync()
        {
            if (_resilienceCache != null)
            {
                var stateValue = await _resilienceCache.GetStringAsync("circuit-breaker-state");
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

            // Notify listeners asynchronously
            if (OnStateChange != null)
            {
                foreach (var handler in OnStateChange.GetInvocationList())
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            handler.DynamicInvoke(_state);
                        }
                        catch (Exception ex)
                        {
                            // Log the exception (optional)
                            Console.WriteLine($"Error in OnStateChange handler: {ex.Message}");
                        }
                    });
                }
            }

            if (_resilienceCache != null)
            {
                await _resilienceCache.SetStringAsync("circuit-breaker-state", _state.ToString());
                await _resilienceCache.SetStringAsync("circuit-breaker-last-changed", _lastStateChangedUtc.ToString("O"));
            }
        }
    }
}
