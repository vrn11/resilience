using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Resilience.LoadShedder
{
    /// <summary>
    /// A static load shedder that enforces a fixed threshold and sheds requests based on system load.
    /// Optimized for scalability and distributed environments.
    /// </summary>
    public class StaticLoadShedder : ILoadShedder
    {
        private readonly Func<double> _loadMonitor;

        // Semaphore for thread-safe updates to the threshold
        private readonly SemaphoreSlim _updateSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Gets or sets the threshold at which the load shedder starts shedding requests.
        /// </summary>
        public double LoadThreshold { get; private set; }

        // Optional distributed cache for global threshold synchronization
        private readonly IDistributedCache? _distributedCache;

        public StaticLoadShedder(Func<double> loadMonitor, LoadShedderOptions options, IDistributedCache? distributedCache = null)
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
                throw new Exception("StaticLoadShedder: Request dropped due to high system load.");
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
                throw new Exception("StaticLoadShedder: Request dropped due to high system load.");
            }

            return await action();
        }

        /// <summary>
        /// Gets the current load value from either the distributed cache or the local load monitor.
        /// </summary>
        public double GetCurrentLoad()
        {
            if (_distributedCache != null)
            {
                string? cachedLoad = _distributedCache.GetString("static-load-shedder-current-load");
                if (!string.IsNullOrEmpty(cachedLoad) && double.TryParse(cachedLoad, out var distributedLoad))
                {
                    return distributedLoad;
                }
            }

            return _loadMonitor(); // Fallback to the provided load monitor
        }

        /// <summary>
        /// Updates the load threshold for the shedder, ensuring thread safety and optional distributed state synchronization.
        /// </summary>
        public void UpdateThreshold(double newThreshold)
        {
            _updateSemaphore.Wait(); // Synchronously wait for exclusive access
            try
            {
                LoadThreshold = newThreshold;

                // Update the distributed cache with the new threshold
                if (_distributedCache != null)
                {
                    _distributedCache.SetString("static-load-shedder-threshold", newThreshold.ToString());
                }
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        /// <summary>
        /// Asynchronously updates the load threshold for the shedder and synchronizes it globally.
        /// </summary>
        public async Task UpdateThresholdAsync(double newThreshold)
        {
            await _updateSemaphore.WaitAsync(); // Asynchronously wait for exclusive access
            try
            {
                LoadThreshold = newThreshold;

                // Update the distributed cache with the new threshold
                if (_distributedCache != null)
                {
                    await _distributedCache.SetStringAsync("static-load-shedder-threshold", newThreshold.ToString());
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
    }
}
