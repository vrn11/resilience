namespace Resilience.LoadShedder;

public class StaticLoadShedder : ILoadShedder
{
    private readonly Func<double> _loadMonitor;
    public double LoadThreshold { get; set; }

    public StaticLoadShedder(Func<double> loadMonitor, LoadShedderOptions options)
    {
        _loadMonitor = loadMonitor;
        LoadThreshold = options.LoadThreshold;
    }

    public T Execute<T>(RequestPriority priority, Func<T> action, Func<T> fallback = default!)
    {
        double load = _loadMonitor();
        if (load > LoadThreshold && (priority == RequestPriority.Low || priority == RequestPriority.Medium))
        {
            if (fallback != null)
                return fallback();
            throw new Exception("StaticLoadShedder: Request dropped due to high system load.");
        }
        return action();
    }

    public async Task<T> ExecuteAsync<T>(RequestPriority priority, Func<Task<T>> action, Func<Task<T>> fallback = default!)
    {
        double load = _loadMonitor();
        if (load > LoadThreshold && (priority == RequestPriority.Low || priority == RequestPriority.Medium))
        {
            if (fallback != null)
                return await fallback();
            throw new Exception("StaticLoadShedder: Request dropped due to high system load.");
        }
        return await action();
    }
}
