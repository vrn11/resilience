using System;
using System.Text.Json;
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
        private int _state = (int)CircuitBreakerState.Closed;
        private long _lastStateChangedUtcTicks = DateTime.UtcNow.Ticks;

        private readonly IResilienceDistributedCache? _resilienceCache;
        public event Action<CircuitBreakerState>? OnStateChange;

        private DateTime LastStateChangedUtc
        {
            get => new DateTime(Interlocked.Read(ref _lastStateChangedUtcTicks));
            set => Interlocked.Exchange(ref _lastStateChangedUtcTicks, value.Ticks);
        }

        public BasicCircuitBreaker(CircuitBreakerOptions options, IResilienceDistributedCache? resilienceCache = null)
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

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, Func<Task<T>> fallback = default!)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action), "Action cannot be null.");
            }

            if (await GetCurrentStateAsync() == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - LastStateChangedUtc > _openTimeout)
                {
                    await SetStateAsync(CircuitBreakerState.HalfOpen);
                }
                else
                {
                    return await HandleFallbackAsync(fallback);
                }
            }

            try
            {
                T result = await action();
                await ResetAsync();
                return result;
            }
            catch (Exception)
            {
                await IncrementFailureAsync();
                return await HandleFallbackAsync(fallback);
            }
        }

        private async Task ResetAsync()
        {
            await SetStateAsync(CircuitBreakerState.Closed);
            Interlocked.Exchange(ref _failureCount, 0);

            if (_resilienceCache != null)
            {
                await _resilienceCache.RemoveAsync("circuit-breaker-failures");
            }
        }

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
                    throw;
                }
            }

            throw new CircuitBreakerOpenException("Circuit breaker is open.");
        }

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
                        else
                        {
                            Console.WriteLine("Invalid circuit breaker state in cache. Defaulting to Closed.");
                            _state = (int)CircuitBreakerState.Closed;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializing circuit breaker state: {ex.Message}");
                        _state = (int)CircuitBreakerState.Closed;
                    }
                }
            }

            return (CircuitBreakerState)_state;
        }

        private async Task SetStateAsync(CircuitBreakerState newState)
        {
            Interlocked.Exchange(ref _state, (int)newState);
            LastStateChangedUtc = DateTime.UtcNow;

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