namespace Resilience.Configuration;

public class CacheConfiguration
{
    public string Type { get; set; } = default!;
    public CachingOptions Options { get; set; } = default!;
}
