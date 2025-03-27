namespace Resilience.Configuration;

public class LoadShedderConfiguration
{
    public string Type { get; set; } = default!;
    public LoadShedder.LoadShedderOptions Options { get; set; } = default!; 
}