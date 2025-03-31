# Resilience SDK

## Overview
The **Resilience SDK** is a .NET-based library designed to provide robust and configurable resilience strategies for applications handling billions of requests per second. It includes implementations for **Circuit Breakers** and **Load Shedders**, enabling applications to gracefully handle failures and maintain stability under high-load conditions.

---

## Features

### Circuit Breaker
- Protects systems from cascading failures by monitoring failures and opening circuits when thresholds are exceeded:
  - **Basic Circuit Breaker**: Monitors failure counts and enforces timeouts.
  - **Error Rate Circuit Breaker**: Tracks error rates over a sliding window to trigger state transitions.

### Load Shedder
- Prevents overload by rejecting low-priority requests when the system load exceeds a defined threshold:
  - **Static Load Shedder**: Uses a fixed threshold to shed low-priority traffic.
  - **Responsive Load Shedder**: Dynamically adjusts thresholds based on historical metrics and current conditions.

## Caching
- Provides distributed state management for resilience components.
  - Supports Redis for distributed caching.
  - Customizable caching interface for other backends.

### Configuration-Driven Strategies
- Resilience strategies can be fully customized through JSON-based configuration, allowing runtime adjustments without redeployments.

### Scalability and Distributed Support
- **Distributed State Management**: Integrates with Redis or other distributed caches to synchronize thresholds, failure metrics, and load states.
- **Thread-Safe Updates**: Supports safe updates to thresholds and metrics in both synchronous and asynchronous contexts.

### Extensibility
- Add custom circuit breakers, load shedders or caching strategies by implementing the provided interfaces:
  - **ICircuitBreaker**
  - **ILoadShedder**
  - **IResilienceDistributedCache**

---

## Project Structure
```bash
resilience.sln 
CircuitBreakerDemo/
  Program.cs 
  resilienceConfig.json 
Resilience/ 
  CircuitBreaker/ 
    BasicCircuitBreaker.cs 
    CircuitBreaker.cs 
    CircuitBreakerFactory.cs 
    ErrorRateCircuitBreaker.cs 
  Configuration/ 
    ComponentConfiguration.cs 
    ResilienceConfigParser.cs 
    ResilienceConfiguration.cs 
  Caching/
    IResilienceDistributedCache.cs
    RedisResilienceDistributedCache.cs
  LoadShedder/ 
    StaticLoadShedder.cs 
    ResponsiveLoadShedder.cs 
    LoadShedderFactory.cs
```

## Getting Started

### Prerequisites

- .NET 9.0 SDK or later
- Visual Studio or Visual Studio Code

### Build and Run

1. Clone the repository:
   ```bash
   git clone https://github.com/vrn11/resilience.git
   cd resilience
2. Build the solution:
```bash
  dotnet build resilience.sln
```
3. Run the demo application:
```bash
  dotnet run --project ResilienceDemo/ResilienceDemo.csproj
```

### Configuration
The resilience strategies are configured using a JSON file (resilienceConfig.json). Below is an example configuration:
```bash
{
  "Gateways": {
    "CircuitBreaker": {
      "Type": "basic",
      "Options": {
        "FailureThreshold": 3,
        "OpenTimeout": "00:00:05"
      }
    },
    "LoadShedder": {
      "Type": "static",
      "Options": {
        "LoadThreshold": 0.7
      }
    }
  }
}
```

### Example Usage
The demo application demonstrates how to use the SDK:
```bash
string configPath = "resilienceConfig.json";
ResilienceConfiguration config = await ResilienceConfigParser.ParseConfigurationAsync(configPath);

// Circuit Breaker Initialization
ICircuitBreaker circuitBreaker = CircuitBreakerFactory.Create(
    config.Gateways.CircuitBreaker.Type,
    config.Gateways.CircuitBreaker.Options
);

// Load Shedder Initialization
ILoadShedder loadShedder = LoadShedderFactory.Create(
    config.Gateways.LoadShedder.Type,
    () => new Random().NextDouble(), // Load monitor function
    config.Gateways.LoadShedder.Options
);
```

### Executing Protected Actions
```bash
// Execute an action using Load Shedder and Circuit Breaker
string result = await loadShedder.ExecuteAsync(
    RequestPriority.Medium,
    async () => await circuitBreaker.ExecuteAsync(
        async () => "Success",
        async () => "Circuit Breaker Fallback"
    ),
    async () => "Load Shedder Fallback"
);

Console.WriteLine(result);
```

## Extending the SDK
### Adding a Custom Circuit Breaker
1. Implement the ICircuitBreaker interface.
2. Add your implementation to the CircuitBreakerFactory.

### Adding a Custom Load Shedder
1. Implement the ILoadShedder interface.
2. Add your implementation to the LoadShedderFactory.

### Adding a custom caching strategy
1. Implement the IResilienceDistributedCache interface.
2. Use your implementation in the circuit breaker or load shedder.

### Contributing
Contributions are welcome! Please follow these steps:

1. Fork the repository.
2. Create a feature branch.
3. Submit a pull request.

## Acknowledgments
- Inspired by resilience patterns such as Circuit Breaker and Load Shedding.
- Built with .NET 9.0.
- Redis integration powered by StackExchange.Redis.
