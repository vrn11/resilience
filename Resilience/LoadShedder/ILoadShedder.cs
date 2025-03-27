namespace Resilience.LoadShedder;

/// <summary>
    /// A generic load shedder interface designed for scalability and extensibility.
    /// </summary>
    public interface ILoadShedder
    {
        /// <summary>
        /// Executes a synchronous action under the load-shedding policy.
        /// </summary>
        /// <typeparam name="T">The return type of the action.</typeparam>
        /// <param name="priority">The priority of the request.</param>
        /// <param name="action">The main action to execute.</param>
        /// <param name="fallback">Optional fallback action if shedding is triggered.</param>
        /// <returns>The result of the action or fallback.</returns>
        T Execute<T>(RequestPriority priority, Func<T> action, Func<T> fallback = default!);

        /// <summary>
        /// Executes an asynchronous action under the load-shedding policy.
        /// </summary>
        /// <typeparam name="T">The return type of the action.</typeparam>
        /// <param name="priority">The priority of the request.</param>
        /// <param name="action">The main asynchronous action to execute.</param>
        /// <param name="fallback">Optional fallback asynchronous action if shedding is triggered.</param>
        /// <returns>The result of the action or fallback.</returns>
        Task<T> ExecuteAsync<T>(RequestPriority priority, Func<Task<T>> action, Func<Task<T>> fallback = default!);

        /// <summary>
        /// Gets the current load value.
        /// </summary>
        /// <returns>A double representing the current load (e.g., 0.0 to 1.0).</returns>
        double GetCurrentLoad();

        /// <summary>
        /// Dynamically updates the load threshold for the shedder.
        /// </summary>
        /// <param name="newThreshold">The new load threshold value.</param>
        void UpdateThreshold(double newThreshold);

        /// <summary>
        /// Dynamically updates the load threshold for the shedder.
        /// </summary>
        /// <param name="newThreshold">The new load threshold value.</param>
        Task UpdateThresholdAsync(double newThreshold);
    }
