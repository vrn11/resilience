using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Resilience.CircuitBreaker
{
    /// <summary>
    /// A generic circuit breaker that monitors an asynchronous action, 
    /// tracks failures, and opens the circuit when failures exceed a threshold.
    /// Optimized for scalability and performance.
    /// </summary>
    public class CircuitBreaker : ICircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _openTimeout;

        // Using atomic variables to reduce contention
        private int _failureCount = 0;
        private int _state = (int)CircuitBreakerState.Closed;
        private DateTime _lastStateChangedUtc = DateTime.UtcNow;

        // Optional distributed store for state sharing (e.g., Redis)
        private readonly IDistributedCache? _distributedCache;

        // Event for monitoring state changes
        public event Action<CircuitBreakerState>? OnStateChange;

        public CircuitBreaker(CircuitBreakerOptions options, IDistributedCache? distributedCache = null)
        {
            _failureThreshold = options.FailureThreshold;
            _openTimeout = options.OpenTimeout;
            _distributedCache = distributedCache;
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
            // Check circuit breaker state
            if (await GetCurrentStateAsync() == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - _lastStateChangedUtc > _openTimeout)
                {
                    await SetStateAsync(CircuitBreakerState.HalfOpen); // Transition to Half-Open
                }
                else
                {
                    return await HandleFallbackAsync(fallback);
                }
            }

            try
            {
                // Execute the protected action
                T result = await action();

                // Reset the circuit breaker state on successful execution
                await ResetAsync();
                return result;
            }
            catch (Exception)
            {
                // Increment failure count and possibly open the circuit
                await IncrementFailureAsync();
                return await HandleFallbackAsync(fallback);
            }
        }

        /// <summary>
        /// Resets the circuit breaker to the Closed state.
        /// </summary>
        private async Task ResetAsync()
        {
            await SetStateAsync(CircuitBreakerState.Closed);
            Interlocked.Exchange(ref _failureCount, 0);

            // Clear distributed cache for failures
            if (_distributedCache != null)
            {
                await _distributedCache.RemoveAsync("circuit-breaker-failures");
            }
        }

        /// <summary>
        /// Increments the failure count and opens the circuit if the failure threshold is exceeded.
        /// </summary>
        private async Task IncrementFailureAsync()
        {
            int currentFailureCount = Interlocked.Increment(ref _failureCount);

            if (_distributedCache != null)
            {
                // Sync failure count in Redis
                string failureCountStr = await _distributedCache.GetStringAsync("circuit-breaker-failures") ?? "0";
                int distributedFailureCount = int.Parse(failureCountStr) + 1;
                await _distributedCache.SetStringAsync("circuit-breaker-failures", distributedFailureCount.ToString());
            }

            if (currentFailureCount >= _failureThreshold)
            {
                await SetStateAsync(CircuitBreakerState.Open);
            }
        }

        /// <summary>
        /// Handles fallback logic when the circuit breaker is open or the protected action fails.
        /// </summary>
        private async Task<T> HandleFallbackAsync<T>(Func<Task<T>> fallback)
        {
            if (fallback != null)
            {
                return await fallback();
            }

            throw new CircuitBreakerOpenException("Circuit breaker is open.");
        }

        /// <summary>
        /// Gets the current circuit breaker state, optionally synced with distributed storage.
        /// </summary>
        private async Task<CircuitBreakerState> GetCurrentStateAsync()
        {
            if (_distributedCache != null)
            {
                string? stateValue = await _distributedCache.GetStringAsync("circuit-breaker-state");
                if (!string.IsNullOrEmpty(stateValue) &&
                    Enum.TryParse<CircuitBreakerState>(stateValue, out var state))
                {
                    return state;
                }
            }
            return (CircuitBreakerState)_state;
        }

        /// <summary>
        /// Sets the circuit breaker state and notifies state change subscribers.
        /// </summary>
        private async Task SetStateAsync(CircuitBreakerState newState)
        {
            Interlocked.Exchange(ref _state, (int)newState);
            _lastStateChangedUtc = DateTime.UtcNow;

            // Notify monitoring systems asynchronously
            if (OnStateChange != null)
            {
                foreach (var handler in OnStateChange.GetInvocationList())
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            handler.DynamicInvoke(newState);
                        }
                        catch (Exception ex)
                        {
                            // Log the exception (optional)
                            Console.WriteLine($"Error in OnStateChange handler: {ex.Message}");
                        }
                    });
                }
            }

            // Persist state to distributed storage
            if (_distributedCache != null)
            {
                await _distributedCache.SetStringAsync("circuit-breaker-state", newState.ToString());
                await _distributedCache.SetStringAsync("circuit-breaker-last-changed", _lastStateChangedUtc.ToString("O"));
            }
        }
    }
}
