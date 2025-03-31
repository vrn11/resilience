using Microsoft.Extensions.Caching.Distributed;

namespace Resilience.Caching;

/// <summary>
/// An extended interface for distributed caching with added resilience.
/// </summary>
public interface IResilienceDistributedCache: IDistributedCache
{
        /// <summary>
        /// Atomically updates a cache key with a new value.
        /// </summary>
        Task<bool> AtomicUpdateAsync(string key, string newValue);

        /// <summary>
        /// Gets the current version of the cache data for a given key.
        /// </summary>
        Task<int> GetVersionAsync(string key);

        /// <summary>
        /// Increment the version for a cache key.
        /// </summary>
        Task IncrementVersionAsync(string key);

        Task IncrementFailuresAsync();
}
