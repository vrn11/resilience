namespace Resilience.LoadShedder;

public class ResponsiveLoadShedder : ILoadShedder
{
    private readonly Func<double> _loadMonitor;
    public double BaseLoadThreshold { get; set; }

    public ResponsiveLoadShedder(Func<double> loadMonitor, LoadShedderOptions options)
    {
        _loadMonitor = loadMonitor;
        BaseLoadThreshold = options.LoadThreshold;
    }

    // In a real implementation, you might adjust the threshold based on usage history.
    private double GetDynamicThreshold()
    {
        // For illustration, we simply return the base value.
        return BaseLoadThreshold;
    }

    public T Execute<T>(RequestPriority priority, Func<T> action, Func<T> fallback = default!)
    {
        double currentLoad = _loadMonitor();
        if (currentLoad > GetDynamicThreshold() && (priority == RequestPriority.Low || priority == RequestPriority.Medium))
        {
            if (fallback != null)
                return fallback();
            throw new Exception("ResponsiveLoadShedder: Request dropped due to high system load.");
        }
        return action();
    }

    public async Task<T> ExecuteAsync<T>(RequestPriority priority, Func<Task<T>> action, Func<Task<T>> fallback = default!)
    {
        double currentLoad = _loadMonitor();
        if (currentLoad > GetDynamicThreshold() && (priority == RequestPriority.Low || priority == RequestPriority.Medium))
        {
            if (fallback != null)
                return await fallback();
            throw new Exception("ResponsiveLoadShedder: Request dropped due to high system load.");
        }
        return await action();
    }
}