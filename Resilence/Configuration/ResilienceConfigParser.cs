namespace Resilience.Configuration;

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public static class ResilienceConfigParser
{
    public static async Task<ResilienceConfiguration> ParseConfigurationAsync(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        var config = await JsonSerializer.DeserializeAsync<ResilienceConfiguration>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize the resilience configuration.");
        }

        return config;
    }
}