using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Resilience.LoadShedder
{
    /// <summary>
    /// A responsive load shedder that dynamically adjusts the threshold based on historical trends or real-time metrics.
    /// Optimized for scalability and distributed environments.
    /// </summary>
    public class ResponsiveLoadShedder : ILoadShedder
    {
        private readonly Func<double> _loadMonitor;

        // Semaphore for thread-safe updates to threshold or load data
        private readonly SemaphoreSlim _updateSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Base threshold for shedding low or medium priority requests.
        /// </summary>
        public double BaseLoadThreshold { get; private set; }

        // Optional distributed cache for global threshold synchronization
        private readonly IDistributedCache? _distributedCache;

        public ResponsiveLoadShedder(Func<double> loadMonitor, LoadShedderOptions options, IDistributedCache? distributedCache = null)
        {
            _loadMonitor = loadMonitor;
            BaseLoadThreshold = options.LoadThreshold;
            _distributedCache = distributedCache;
        }

        /// <summary>
        /// Dynamically calculates the load threshold based on historical usage or real-time metrics.
        /// </summary>
        private double GetDynamicThreshold()
        {
            if (_distributedCache != null)
            {
                // Retrieve a dynamically calculated threshold from distributed storage (e.g., historical trends)
                string? cachedThreshold = _distributedCache.GetString("responsive-load-threshold");
                if (!string.IsNullOrEmpty(cachedThreshold) && double.TryParse(cachedThreshold, out var dynamicThreshold))
                {
                    return dynamicThreshold;
                }
            }

            return BaseLoadThreshold; // Fallback to base threshold if no distributed data is available
        }

        /// <summary>
        /// Updates the base load threshold asynchronously and synchronizes it globally if distributed cache is enabled.
        /// </summary>
        public async Task UpdateThresholdAsync(double newThreshold)
        {
            await _updateSemaphore.WaitAsync(); // Wait for exclusive access
            try
            {
                BaseLoadThreshold = newThreshold;

                // Synchronize with distributed storage
                if (_distributedCache != null)
                {
                    await _distributedCache.SetStringAsync("responsive-load-threshold", newThreshold.ToString());
                }
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes a synchronous action under the load-shedding policy.
        /// </summary>
        public T Execute<T>(RequestPriority priority, Func<T> action, Func<T> fallback = default!)
        {
            double currentLoad = GetCurrentLoad();
            double dynamicThreshold = GetDynamicThreshold();

            if (currentLoad > dynamicThreshold && ShouldShed(priority))
            {
                if (fallback != null)
                    return fallback();
                throw new Exception("ResponsiveLoadShedder: Request dropped due to high system load.");
            }

            return action();
        }

        /// <summary>
        /// Executes an asynchronous action under the load-shedding policy.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(RequestPriority priority, Func<Task<T>> action, Func<Task<T>> fallback = default!)
        {
            double currentLoad = GetCurrentLoad();
            double dynamicThreshold = GetDynamicThreshold();

            if (currentLoad > dynamicThreshold && ShouldShed(priority))
            {
                if (fallback != null)
                    return await fallback();
                throw new Exception("ResponsiveLoadShedder: Request dropped due to high system load.");
            }

            return await action();
        }

        /// <summary>
        /// Retrieves the current system load from the monitor or distributed storage.
        /// </summary>
        public double GetCurrentLoad()
        {
            if (_distributedCache != null)
            {
                string? cachedLoad = _distributedCache.GetString("responsive-current-load");
                if (!string.IsNullOrEmpty(cachedLoad) && double.TryParse(cachedLoad, out var distributedLoad))
                {
                    return distributedLoad;
                }
            }

            return _loadMonitor(); // Fallback to local load monitor
        }

        /// <summary>
        /// Updates the current system load dynamically for distributed environments.
        /// </summary>
        /// <param name="currentLoad">The current load value to update.</param>
        public void UpdateCurrentLoad(double currentLoad)
        {
            _updateSemaphore.Wait();
            try
            {
                if (_distributedCache != null)
                {
                    _distributedCache.SetString("responsive-current-load", currentLoad.ToString());
                }
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        /// <summary>
        /// Determines whether the given request should be shed based on its priority.
        /// </summary>
        private bool ShouldShed(RequestPriority priority)
        {
            return priority == RequestPriority.Low || priority == RequestPriority.Medium;
        }

        /// <summary>
        /// Updates the base load threshold and synchronizes it globally if distributed cache is enabled.
        /// </summary>
        public void UpdateThreshold(double newThreshold)
        {
            _updateSemaphore.Wait(); // Synchronously wait for exclusive access
            try
            {
                BaseLoadThreshold = newThreshold;

                // Synchronize with distributed storage
                if (_distributedCache != null)
                {
                    _distributedCache.SetString("responsive-load-threshold", newThreshold.ToString());
                }
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }
    }
}
