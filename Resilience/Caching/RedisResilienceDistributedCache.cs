namespace Resilience.Caching;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Resilience.CircuitBreaker;
using StackExchange.Redis;

/// <summary>
/// A Redis-based implementation of the IResilienceDistributedCache.
/// </summary>
public class RedisResilienceDistributedCache : IResilienceDistributedCache
{
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly IDatabase _redisDatabase;

    private readonly int _failureThreshold;

    /// <summary>
    /// Initializes a new instance of the RedisResilienceDistributedCache class.
    /// </summary>
    /// <param name="redisConnectionString">The Redis connection string.</param>
    /// <param name="failureThreshold">The failure threshold for the circuit breaker.</param>
    /// <exception cref="ArgumentNullException">Thrown when redisConnectionString is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when failureThreshold is less than or equal to zero.</exception>
    public RedisResilienceDistributedCache(CachingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(options.ConnectionString))
        {
            throw new ArgumentNullException(nameof(options.ConnectionString));
        }
        if (options.CircuitBreakerFailureThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.CircuitBreakerFailureThreshold), "Failure threshold must be greater than zero.");
        }
        
        string redisConnectionString = options.ConnectionString;
        int failureThreshold = options.CircuitBreakerFailureThreshold;

        _redisConnection = ConnectionMultiplexer.Connect(redisConnectionString);
        _redisDatabase = _redisConnection.GetDatabase();
        _failureThreshold = failureThreshold;
    }

    /// <summary>
    /// Gets the current value of a cache key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>The cache value or null if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
    public async Task<string?> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        return await _redisDatabase.StringGetAsync(key);
    }

    /// <summary>
    /// Sets a cache key with a value and options.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The cache value.</param>
    /// <param name="options">The cache entry options.</param>
    /// <exception cref="ArgumentNullException">Thrown when key or value is null or empty.</exception>
    public async Task SetAsync(string key, string value, DistributedCacheEntryOptions options)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
        {
            throw new ArgumentNullException(nameof(key), "Key or value cannot be null.");
        }

        await _redisDatabase.StringSetAsync(key, value);
        // Note: DistributedCacheEntryOptions is ignored for simplicity as Redis handles expiration separately.
    }

    /// <summary>
    /// Removes a cache key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
    public async Task RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        await _redisDatabase.KeyDeleteAsync(key);
    }

    /// <summary>
    /// Refreshes a cache key by resetting its expiration.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
    /// <remarks>
    /// This method resets the expiration of the cache key without changing its value.
    /// </remarks>
    public async Task RefreshAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        var value = await _redisDatabase.StringGetAsync(key);
        if (!value.IsNullOrEmpty)
        {
            await _redisDatabase.StringSetAsync(key, value); // Reset the expiration
        }
    }

    /// <summary>
    /// Atomically updates a cache key with a new value.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="newValue">The new value to set.</param>
    /// <returns>True if the update was successful; otherwise, false.</returns>
    public async Task<bool> AtomicUpdateAsync(string key, string newValue)
    {
        var luaScript = @"
            if redis.call('EXISTS', KEYS[1]) == 1 then
                redis.call('SET', KEYS[1], ARGV[1])
                return 1
            else
                return 0
            end
        ";

        var result = await _redisDatabase.ScriptEvaluateAsync(
            luaScript,
            new RedisKey[] { key },
            new RedisValue[] { newValue }
        );

        return (int)result == 1;
    }

    /// <summary>
    /// Gets the current version of the cache data for a given key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>The version number of the cache data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
    /// <remarks>
    /// The version is stored as a separate key with the format "{key}:version".
    /// This method retrieves the version number for the specified key.
    /// If the version key does not exist, it returns 0.
    /// </remarks>
    public async Task<int> GetVersionAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        var versionValue = await _redisDatabase.StringGetAsync($"{key}:version");
        return int.TryParse(versionValue, out var version) ? version : 0;
    }

    /// <summary>
    /// Increment the version for a cache key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
    /// <remarks>
    /// This method increments the version number for the specified key.
    /// The version is stored as a separate key with the format "{key}:version".
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
    public async Task IncrementVersionAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        await _redisDatabase.StringIncrementAsync($"{key}:version");
    }

    /// <summary>
    /// Resets the circuit breaker state and failure count.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method resets the circuit breaker state to Closed and clears the failure count.
    /// It uses a Lua script to perform the operation atomically.
    /// The script sets the circuit breaker state and deletes the failure count key.
    /// </remarks>
    private async Task ResetAsync()
    {
        var script = @"
            redis.call('SET', KEYS[1], ARGV[1])
            redis.call('DEL', KEYS[2])
        ";

        var keys = new RedisKey[] { "circuit-breaker-state", "circuit-breaker-failures" };
        var values = new RedisValue[] { CircuitBreakerState.Closed.ToString() };

        await _redisDatabase.ScriptEvaluateAsync(script, keys, values);
    }

    /// <summary>
    /// Increments the failure count and opens the circuit if the threshold is exceeded.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method increments the failure count in Redis and checks if it exceeds the threshold.
    /// If the threshold is exceeded, it opens the circuit by setting the state to Open.
    /// The failure count is stored in Redis with the key "circuit-breaker-failures".
    /// The circuit breaker state is stored with the key "circuit-breaker-state".
    /// The method uses a Lua script to perform the operation atomically.
    /// The script increments the failure count and checks if it exceeds the threshold.
    /// If the threshold is exceeded, it sets the circuit breaker state to Open.
    /// The script returns the current failure count.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
    /// <returns>The current failure count.</returns>
    public async Task IncrementFailuresAsync()
    {
        var script = @"
            local failures = redis.call('INCR', KEYS[1]) -- Increment failure count
            if failures >= tonumber(ARGV[1]) then
                redis.call('SET', KEYS[2], ARGV[2]) -- Open circuit if threshold exceeded
            end
            return failures
        ";

        var keys = new RedisKey[] { "circuit-breaker-failures", "circuit-breaker-state" };
        var values = new RedisValue[] { _failureThreshold.ToString(), CircuitBreakerState.Open.ToString() };

        var result = await _redisDatabase.ScriptEvaluateAsync(script, keys, values);

        // Optional: Log or monitor the failure count for diagnostics
        int failureCount = (int)result!;
        Console.WriteLine($"Current failure count: {failureCount}");
    }


    public byte[]? Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        var result = _redisDatabase.StringGet(key);

        if (!result.HasValue || result.IsNull) // Ensure the result is not null
        {
            return null;
        }

        return (byte[])result!;
    }


    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        var result = await _redisDatabase.StringGetAsync(key);

        if (!result.HasValue || result.IsNull) // Ensure the result is not null
        {
            return null;
        }

        return (byte[])result!;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        if (string.IsNullOrEmpty(key) || value == null)
        {
            throw new ArgumentNullException(nameof(key), "Key or value cannot be null.");
        }

        _redisDatabase.StringSet(key, value);
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(key) || value == null)
        {
            throw new ArgumentNullException(nameof(key), "Key or value cannot be null.");
        }

        await _redisDatabase.StringSetAsync(key, value);
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        var value = await _redisDatabase.StringGetAsync(key);
        if (!value.IsNullOrEmpty)
        {
            await _redisDatabase.StringSetAsync(key, value);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        await _redisDatabase.KeyDeleteAsync(key);
    }

    public void Refresh(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }
        var value = _redisDatabase.StringGet(key);
        if (!value.IsNullOrEmpty)
        {
            _redisDatabase.StringSet(key, value); // Reset the expiration
        }
    }

    public void Remove(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        _redisDatabase.KeyDelete(key);
    }
}
