using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Resilience.Caching;

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

        private long _lastStateChangedUtcTicks = DateTime.UtcNow.Ticks;

        private DateTime LastStateChangedUtc
        {
            get => new DateTime(Interlocked.Read(ref _lastStateChangedUtcTicks));
            set => Interlocked.Exchange(ref _lastStateChangedUtcTicks, value.Ticks);
        }

        // Optional distributed store for state sharing (e.g., Redis)
        private readonly IResilienceDistributedCache? _resilienceCache;

        // Event for monitoring state changes
        public event Action<CircuitBreakerState>? OnStateChange;

        public CircuitBreaker(CircuitBreakerOptions options, IResilienceDistributedCache? resilienceCache = null)
        {
            if (options.FailureThreshold <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.FailureThreshold), "FailureThreshold must be greater than zero.");
            }

            if (options.OpenTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options.OpenTimeout), "OpenTimeout must be greater than zero.");
            }

            _failureThreshold = options.FailureThreshold;
            _openTimeout = options.OpenTimeout;
            _resilienceCache = resilienceCache;
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
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action), "Action cannot be null.");
            }

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
            if (_resilienceCache != null)
            {
                await _resilienceCache.RemoveAsync("circuit-breaker-failures");
            }
        }

        /// <summary>
        /// Increments the failure count and opens the circuit if the failure threshold is exceeded.
        /// </summary>
        private async Task IncrementFailureAsync()
        {
            int currentFailureCount = Interlocked.Increment(ref _failureCount);

            if (_resilienceCache != null)
            {
                try
                {
                    await _resilienceCache.IncrementFailuresAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error incrementing failure count in distributed cache: {ex.Message}");
                }
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
                try
                {
                    return await fallback();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in fallback: {ex.Message}");
                }
            }

            throw new CircuitBreakerOpenException("Circuit breaker is open.");
        }

        /// <summary>
        /// Gets the current circuit breaker state, optionally synced with distributed storage.
        /// </summary>
        private async Task<CircuitBreakerState> GetCurrentStateAsync()
        {
            if (_resilienceCache != null)
            {
                var serializedState = await _resilienceCache.GetStringAsync("circuit-breaker-state");
                if (!string.IsNullOrEmpty(serializedState))
                {
                    try
                    {
                        var cacheState = JsonSerializer.Deserialize<CircuitBreakerCacheState>(serializedState);
                        if (cacheState != null && Enum.TryParse<CircuitBreakerState>(cacheState.State, out var state))
                        {
                            _state = (int)state;
                            LastStateChangedUtc = DateTime.Parse(cacheState.LastChangedUtc);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializing circuit breaker state: {ex.Message}");
                    }
                }
            }

            return (CircuitBreakerState)_state;
        }

        /// <summary>
        /// Sets the circuit breaker state and notifies state change subscribers.
        /// This method must always be called from within a lock or semaphore to ensure thread safety.
        /// </summary>
        private async Task SetStateAsync(CircuitBreakerState newState)
        {
            Interlocked.Exchange(ref _state, (int)newState);
            LastStateChangedUtc = DateTime.UtcNow;

            // Notify monitoring systems asynchronously
            if (OnStateChange != null)
            {
                var tasks = OnStateChange.GetInvocationList()
                    .Select(handler => Task.Run(() =>
                    {
                        try
                        {
                            handler.DynamicInvoke(newState);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in OnStateChange handler: {ex.Message}");
                        }
                    }));

                await Task.WhenAll(tasks);
            }

            // Persist state to distributed storage
            if (_resilienceCache != null)
            {
                try
                {
                    var cacheState = new CircuitBreakerCacheState
                    {
                        State = ((CircuitBreakerState)_state).ToString(),
                        LastChangedUtc = LastStateChangedUtc.ToString("O")
                    };

                    var serializedState = JsonSerializer.Serialize(cacheState);
                    await _resilienceCache.SetStringAsync("circuit-breaker-state", serializedState);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating distributed cache: {ex.Message}");
                }
            }
        }
    }
}
