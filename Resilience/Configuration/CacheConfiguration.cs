namespace Resilience.Configuration;
using Resilience.Caching;

public class CacheConfiguration
{
    public string Type { get; set; } = default!;
    public CachingOptions Options { get; set; } = default!;
}
