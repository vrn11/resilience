namespace ResilienceDemo;

using System;
using System.Threading.Tasks;
using Resilience;
using Resilience.Configuration;
using Resilience.LoadShedder;
using Resilience.CircuitBreaker;
using Resilience.Caching;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Example configuration JSON file path. In production, adjust the path as necessary.
        string configPath = "resilienceConfig.json";

        // Parse the configuration from JSON.
        ResilienceConfiguration config = await ResilienceConfigParser.ParseConfigurationAsync(configPath);

        // For demonstration, we use the Gateway configuration.
        var gatewayCBConfig = config.Gateways.CircuitBreaker;
        var gatewayLSConfig = config.Gateways.LoadShedder;

        // For demonstration, we use the Microservices configuration.
        // var microservicesCBConfig = config.Microservices.CircuitBreaker;
        // var microservicesLSConfig = config.Microservices.LoadShedder;

        // Initialize Redis cache for distributed resilience.
        RedisResilienceDistributedCache redisCache = new RedisResilienceDistributedCache("localhost:6379", gatewayCBConfig.Options.FailureThreshold);

        // Create resilience components using factories.
        ICircuitBreaker circuitBreaker = CircuitBreakerFactory.Create(gatewayCBConfig.Type, gatewayCBConfig.Options, redisCache);

        // For load monitoring, we simulate using a random value between 0.0 and 1.0.
        Random random = new Random();
        Func<double> loadMonitor = () => random.NextDouble();
        ILoadShedder loadShedder = LoadShedderFactory.Create(gatewayLSConfig.Type, loadMonitor, gatewayLSConfig.Options, redisCache);

        // Define a risky action that may fail.
        Func<Task<string>> riskyAction = async () =>
        {
            await Task.Delay(100);
            if (random.Next(0, 2) == 0)
                throw new Exception("Simulated risky action failure.");
            return "Risky action succeeded.";
        };

        // Define fallback actions.
        Func<Task<string>> cbFallback = async () =>
        {
            await Task.Delay(50);
            return "Circuit breaker fallback result.";
        };

        Func<Task<string>> lsFallback = () => Task.FromResult("Load shedder fallback result.");

        Console.WriteLine("Starting MyResilienceSDK Demo with configuration-driven strategies...\n");

        int errorCount = 0;
        for (int i = 0; i < 50; i++)
        {
            try
            {
                string result = await loadShedder.ExecuteAsync(
                    RequestPriority.Medium,
                    async () => await circuitBreaker.ExecuteAsync(riskyAction, cbFallback),
                    lsFallback);
                Console.WriteLine($"Request {i + 1}: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request {i + 1}: *Error* - {ex.Message}");
                errorCount++;
            }
        }

        Console.WriteLine($"\nTotal errors encountered: {errorCount}");

        Console.WriteLine("\nDemo complete.");
    }
}