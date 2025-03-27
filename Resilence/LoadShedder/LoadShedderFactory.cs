namespace Resilience.LoadShedder;

public static class LoadShedderFactory
{
    public static ILoadShedder Create(string type, Func<double> loadMonitor, LoadShedderOptions options)
    {
        switch (type.ToLowerInvariant())
        {
            case "responsive":
                return new ResponsiveLoadShedder(loadMonitor, options);
            case "static":
            default:
                return new StaticLoadShedder(loadMonitor, options);
        }
    }
}