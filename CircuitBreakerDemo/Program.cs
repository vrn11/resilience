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
        // For demonstration, we simulate system load using a random number generator.
        // A real load monitor should return actual metrics (e.g., CPU usage or queue length).
        // ----------------------------------------------------------------------------------
        Random random = new Random();
        Func<double> sampleLoad = () => random.NextDouble();  // Simulated load range: 0.0 to 1.0

        // Configure the load shedder.
        var loadShedderOptions = new LoadShedderOptions { LoadThreshold = 0.7 };
        var loadShedder = new LoadShedder(sampleLoad, loadShedderOptions);

        // Configure the circuit breaker.
        var circuitBreakerOptions = new CircuitBreakerOptions { FailureThreshold = 3, OpenTimeout = TimeSpan.FromSeconds(5) };
        var circuitBreaker = new CircuitBreaker(circuitBreakerOptions);

        // ----------------------------------------------------------------------------------
        // Define a "risky" action that might fail.
        // In a real system, this might call a remote service or perform a resource-intensive operation.
        // ----------------------------------------------------------------------------------
        Func<Task<string>> riskyAction = async () =>
        {
            // Simulate delay.
            await Task.Delay(100);
            // Simulate a 50% chance of failure.
            if (random.Next(0, 2) == 0)
                throw new Exception("Simulated risky action failure");
            return "Risky action succeeded";
        };

        // Define fallback action for the circuit breaker.
        Func<Task<string>> circuitBreakerFallback = async () =>
        {
            await Task.Delay(50);
            return "Fallback: Circuit breaker response";
        };

        // Define fallback action for load shedding.
        Func<Task<string>> loadShedderFallback = () => Task.FromResult("Fallback: Load shedder response");

        // ----------------------------------------------------------------------------------
        // Combined execution: Apply load shedding first, then protect the risky action with the circuit breaker.
        // ----------------------------------------------------------------------------------
        Console.WriteLine("Starting MyResilienceSDK Demo...\n");

        for (int i = 0; i < 10; i++)
        {
            try
            {
                string result = await loadShedder.ExecuteAsync(
                    RequestPriority.Medium,
                    async () => await circuitBreaker.ExecuteAsync(riskyAction, circuitBreakerFallback),
                    loadShedderFallback);
                Console.WriteLine($"Request {i + 1}: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request {i + 1}: Error - {ex.Message}");
            }
        }

        Console.WriteLine("\nDemo complete.");
    }
}