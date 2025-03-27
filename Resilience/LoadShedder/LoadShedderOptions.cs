namespace Resilience.LoadShedder;

/// <summary>
/// Options used to configure the load shedder.
/// </summary>
public class LoadShedderOptions
{
    public double LoadThreshold { get; set; } = 0.7;
}