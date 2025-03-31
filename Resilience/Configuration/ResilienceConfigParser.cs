namespace Resilience.Configuration;

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public static class ResilienceConfigParser
{
    static JsonSerializerOptions _jsonSerializerOptions;
    
    static ResilienceConfigParser()
    {
        // Register custom converters if needed
         _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }
    public static async Task<ResilienceConfiguration> ParseConfigurationAsync(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        var config = await JsonSerializer.DeserializeAsync<ResilienceConfiguration>(stream,_jsonSerializerOptions) ?? throw new InvalidOperationException("Failed to deserialize the resilience configuration.");
        return config;
    }

    private static void MergeCommonSettingsIntoGateways(ResilienceConfiguration config)
    {
        if (config != null)
        {
            // Apply shared FailureThreshold to CircuitBreaker and Cache
            if (config.Gateways.CircuitBreaker?.Options != null)
            {
                config.Gateways.CircuitBreaker.Options.FailureThreshold = config.Gateways.Common.FailureThreshold;
            }

            // Apply shared ConnectionString to Cache
            if (config.Gateways.Cache?.Options != null)
            {
                config.Gateways.Cache.Options.CircuitBreakerFailureThreshold = config.Gateways.Common.FailureThreshold;
            }

            // Apply shared FailureThreshold to CircuitBreaker and Cache
            if (config.Microservices.CircuitBreaker?.Options != null)
            {
                config.Microservices.CircuitBreaker.Options.FailureThreshold = config.Microservices.Common.FailureThreshold;
            }

            // Apply shared ConnectionString to Cache
            if (config.Gateways.Cache?.Options != null)
            {
                config.Microservices.Cache.Options.CircuitBreakerFailureThreshold = config.Microservices.Common.FailureThreshold;
            }
        }
    }
}