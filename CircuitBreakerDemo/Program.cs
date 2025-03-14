namespace CircuitBreakerDemo;
using CircuitBreaker;
using System;
using System.Threading.Tasks;

public class Program
{
    public static async Task Main(string[] args)
    {
        // ----------------------------------------------------------------------------------
        // Simulation setup:
        // For demonstration, we're simulating load using a random number generator.
        // In production, the loadMonitor should reflect real system metrics.
        // ----------------------------------------------------------------------------------
        Random random = new Random();
        Func<double> sampleLoad = () => random.NextDouble(); // Simulated load between 0.0 and 1.0

        // Create the load shedder with a threshold of 0.7 (i.e., if load > 70%, shed low or medium priority requests).
        var loadShedder = new LoadShedder(sampleLoad, loadThreshold: 0.7);

        // Create a circuit breaker with a threshold of 3 consecutive failures and an open timeout of 5 seconds.
        var circuitBreaker = new CircuitBreaker( failureThreshold: 3, openTimeout: TimeSpan.FromSeconds(5));

        // ----------------------------------------------------------------------------------
        // Define a "risky" action that might fail.
        // In a real system, this might be a call to a remote service or a resource-intensive operation.
        // ----------------------------------------------------------------------------------
        Func<Task<string>> riskyAction = async () =>
        {
            // Simulate some delay.
            await Task.Delay(100);

            // Simulate a 50% chance of failure.
            if (random.Next(0, 2) == 0)
                throw new Exception("Simulated failure in riskyAction");
            
            return "Action succeeded";
        };

        // Optionally, define a fallback action for the circuit breaker.
        Func<Task<string>> fallbackAction = async () =>
        {
            await Task.Delay(50);
            return "Circuit breaker fallback result";
        };

        // Optional fallback for load shedding when traffic is shed.
        Func<Task<string>> loadShedFallback = () => Task.FromResult("Load shed fallback result");

        // ----------------------------------------------------------------------------------
        // Combined execution: first apply load shedding, then use the circuit breaker to protect
        // the risky action.
        // ----------------------------------------------------------------------------------
        Console.WriteLine("Starting simulation of requests...\n");
        for (int i = 0; i < 10; i++)
        {
            try
            {
                string result = await loadShedder.ExecuteAsync(
                    priority: RequestPriority.Medium,
                    action: async () => await circuitBreaker.ExecuteAsync(riskyAction, fallbackAction),
                    fallback: loadShedFallback);

                Console.WriteLine($"Request {i + 1}: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request {i + 1}: Exception - {ex.Message}");
            }
        }

        Console.WriteLine("\nSimulation complete.");
    }
}