namespace CircuitBreaker;

/// <summary>
/// A generic load shedder interface.
/// </summary>
public interface ILoadShedder
{
    T Execute<T>(RequestPriority priority, Func<T> action, Func<T> fallback = default!);
    Task<T> ExecuteAsync<T>(RequestPriority priority, Func<Task<T>> action, Func<Task<T>> fallback = default!);
}
