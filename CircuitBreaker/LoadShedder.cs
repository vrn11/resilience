namespace CircuitBreaker;

    /// <summary>
    /// A generic load shedder that uses a load monitor function to decide if a request should be processed.
    /// If the measured load exceeds a threshold, low or medium priority requests are rejected or fallback is used.
    /// </summary>
    public class LoadShedder : ILoadShedder
    {
        private readonly Func<double> _loadMonitor;

        /// <summary>
        /// Gets or sets the threshold at which the load shedder will start shedding requests.
        /// </summary>
        public double LoadThreshold { get; set; }

        public LoadShedder(Func<double> loadMonitor, LoadShedderOptions options)
        {
            _loadMonitor = loadMonitor;
            LoadThreshold = options.LoadThreshold;
        }

        public T Execute<T>(RequestPriority priority, Func<T> action, Func<T> fallback = default!)
        {
            double currentLoad = _loadMonitor();
            if (currentLoad > LoadThreshold && (priority == RequestPriority.Low || priority == RequestPriority.Medium))
            {
                if (fallback != null)
                    return fallback();
                else
                    throw new Exception("Load shedding: Request dropped due to high system load.");
            }
            return action();
        }

        public async Task<T> ExecuteAsync<T>(RequestPriority priority, Func<Task<T>> action, Func<Task<T>> fallback = default!)
        {
            double currentLoad = _loadMonitor();
            if (currentLoad > LoadThreshold && (priority == RequestPriority.Low || priority == RequestPriority.Medium))
            {
                if (fallback != null)
                    return await fallback();
                else
                    throw new Exception("Load shedding: Request dropped due to high system load.");
            }
            return await action();
        }
    }
