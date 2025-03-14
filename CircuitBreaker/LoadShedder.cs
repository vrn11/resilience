namespace CircuitBreaker;

public class LoadShedder
{
    // Delegate that returns a current load metric (e.g., CPU, queue length)
    private readonly Func<double> loadMonitor;

    /// <summary>
    /// Configured load threshold. If the load exceeds this level, lower priority requests may be shed.
    /// </summary>
    public double LoadThreshold { get; set; }

    public LoadShedder(Func<double> loadMonitor, double loadThreshold)
    {
        this.loadMonitor = loadMonitor;
        this.LoadThreshold = loadThreshold;
    }

    /// <summary>
    /// Executes an action synchronously under the load shedding policy.
    /// </summary>
    public T Execute<T>(RequestPriority priority, Func<T> action, Func<T> fallback = default!)
    {
        double currentLoad = loadMonitor();
        if (currentLoad > LoadThreshold && (priority == RequestPriority.Low || priority == RequestPriority.Medium))
        {
            if (fallback != null)
                return fallback();
            else
                throw new Exception("Load shedding: Request dropped due to high system load.");
        }
        return action();
    }

    /// <summary>
    /// Executes an asynchronous action under the load shedding policy.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(RequestPriority priority, Func<Task<T>> action, Func<Task<T>> fallback = default!)
    {
        double currentLoad = loadMonitor();
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
