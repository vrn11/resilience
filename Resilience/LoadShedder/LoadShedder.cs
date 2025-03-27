using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Resilience.LoadShedder
{
    /// <summary>
    /// A generic load shedder that uses a load monitor function to decide if a request should be processed.
    /// Optimized for scalability and distributed environments.
    /// </summary>
    public class LoadShedder : ILoadShedder
    {
        private readonly Func<double> _loadMonitor;

        // Asynchronous locking mechanism for updates
        private readonly SemaphoreSlim _updateSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Gets or sets the threshold at which the load shedder starts shedding requests.
        /// Supports dynamic updates.
        /// </summary>
        public double LoadThreshold { get; private set; }

        // Distributed cache for global coordination (optional)
        private readonly IDistributedCache? _distributedCache;

        public LoadShedder(Func<double> loadMonitor, LoadShedderOptions options, IDistributedCache? distributedCache = null)
        {
            _loadMonitor = loadMonitor;
            LoadThreshold = options.LoadThreshold;
            _distributedCache = distributedCache;
        }

        /// <summary>
        /// Executes a synchronous action under the load-shedding policy.
        /// </summary>
        public T Execute<T>(RequestPriority priority, Func<T> action, Func<T> fallback = default!)
        {
            double currentLoad = GetCurrentLoad();

            if (currentLoad > LoadThreshold && ShouldShed(priority))
            {
                if (fallback != null)
                    return fallback();
                else
                    throw new Exception("Load shedding: Request dropped due to high system load.");
            }

            return action();
        }

        /// <summary>
        /// Executes an asynchronous action under the load-shedding policy.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(RequestPriority priority, Func<Task<T>> action, Func<Task<T>> fallback = default!)
        {
            double currentLoad = GetCurrentLoad();

            if (currentLoad > LoadThreshold && ShouldShed(priority))
            {
                if (fallback != null)
                    return await fallback();
                else
                    throw new Exception("Load shedding: Request dropped due to high system load.");
            }

            return await action();
        }

        /// <summary>
        /// Gets the current load value, optionally synced with distributed storage.
        /// </summary>
        public double GetCurrentLoad()
        {
            if (_distributedCache != null)
            {
                string? cachedLoad = _distributedCache.GetString("load-shedder-current-load");
                if (!string.IsNullOrEmpty(cachedLoad) && double.TryParse(cachedLoad, out var distributedLoad))
                {
                    return distributedLoad;
                }
            }

            return _loadMonitor();
        }

        /// <summary>
        /// Dynamically updates the load threshold for the shedder.
        /// </summary>
        public async Task UpdateThresholdAsync(double newThreshold)
        {
            await _updateSemaphore.WaitAsync();
            try
            {
                LoadThreshold = newThreshold;

                // Update distributed storage for threshold if configured
                if (_distributedCache != null)
                {
                    await _distributedCache.SetStringAsync("load-shedder-threshold", newThreshold.ToString());
                }
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        /// <summary>
        /// Updates the load threshold for the shedder, ensuring thread safety and optional distributed state synchronization.
        /// This is a synchronous version without async/await.
        /// </summary>
        /// <param name="newThreshold">The new threshold value to set.</param>
        public void UpdateThreshold(double newThreshold)
        {
            _updateSemaphore.Wait(); // Synchronously wait for semaphore lock
            try
            {
                LoadThreshold = newThreshold;

                // Update the distributed cache with the new threshold
                if (_distributedCache != null)
                {
                    _distributedCache.SetString("load-shedder-threshold", newThreshold.ToString());
                }
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        /// <summary>
        /// Determines whether a request should be shed based on its priority.
        /// </summary>
        private bool ShouldShed(RequestPriority priority)
        {
            return priority == RequestPriority.Low || priority == RequestPriority.Medium;
        }
    }
}
