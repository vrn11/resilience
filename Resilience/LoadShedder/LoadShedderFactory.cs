namespace Resilience.LoadShedder;

using Microsoft.Extensions.Caching.Distributed;

public static class LoadShedderFactory
{
    public static ILoadShedder Create(string type, Func<double> loadMonitor, LoadShedderOptions options, IDistributedCache? distributedCache = null)
    {
        switch (type.ToLowerInvariant())
        {
            case "responsive":
                return new ResponsiveLoadShedder(loadMonitor, options , distributedCache);
            case "static":
            default:
                return new StaticLoadShedder(loadMonitor, options, distributedCache);
        }
    }
}