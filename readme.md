# Resilience SDK

The **Resilience SDK** is a .NET-based library designed to provide robust and configurable resilience strategies for applications. It includes implementations for **Circuit Breakers** and **Load Shedders**, enabling applications to handle failures gracefully and maintain stability under high load conditions.

## Features

- **Circuit Breaker**: Protects systems from cascading failures by monitoring failures and opening the circuit when thresholds are exceeded.
  - Basic Circuit Breaker
  - Error Rate Circuit Breaker

- **Load Shedder**: Prevents overload by rejecting low-priority requests when the system load exceeds a defined threshold.
  - Static Load Shedder
  - Responsive Load Shedder

- **Configuration-Driven**: Easily configure resilience strategies using JSON files.

- **Extensibility**: Add custom circuit breakers or load shedders by implementing the provided interfaces.

## Project Structure
resilience.sln 
CircuitBreakerDemo/
  Program.cs 
  resilienceConfig.json 
Resilence/ 
  CircuitBreaker/ 
    BasicCircuitBreaker.cs 
    CircuitBreaker.cs 
    CircuitBreakerFactory.cs 
    ErrorRateCircuitBreaker.cs 
  Configuration/ 
    ComponentConfiguration.cs 
    ResilienceConfigParser.cs 
    ResilienceConfiguration.cs 
  LoadShedder/ 
    StaticLoadShedder.cs 
    ResponsiveLoadShedder.cs 
    LoadShedderFactory.cs


## Getting Started

### Prerequisites

- .NET 9.0 SDK or later
- Visual Studio or Visual Studio Code

### Build and Run

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd circuit_breaker
2. Build the solution:
dotnet build resilience.sln
3. Run the demo application:
dotnet run --project CircuitBreakerDemo/CircuitBreakerDemo.csproj

### Configuration
The resilience strategies are configured using a JSON file (resilienceConfig.json). Below is an example configuration:

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

### Example Usage
The demo application demonstrates how to use the SDK:

string configPath = "resilienceConfig.json";
ResilienceConfiguration config = await ResilienceConfigParser.ParseConfigurationAsync(configPath);

ICircuitBreaker circuitBreaker = CircuitBreakerFactory.Create(config.Gateways.CircuitBreaker.Type, config.Gateways.CircuitBreaker.Options);

ILoadShedder loadShedder = LoadShedderFactory.Create(config.Gateways.LoadShedder.Type, () => new Random().NextDouble(), config.Gateways.LoadShedder.Options);

string result = await loadShedder.ExecuteAsync(
    RequestPriority.Medium,
    async () => await circuitBreaker.ExecuteAsync(() => Task.FromResult("Success")),
    () => Task.FromResult("Fallback")
);

### Extending the SDK
#### Adding a Custom Circuit Breaker
1. Implement the ICircuitBreaker interface.
2. Add your implementation to the CircuitBreakerFactory.

#### Adding a Custom Load Shedder
1. Implement the ILoadShedder interface.
2. Add your implementation to the LoadShedderFactory.

#### Contributing
Contributions are welcome! Please follow these steps:

1. Fork the repository.
2. Create a feature branch.
3. Submit a pull request.

#### License
This project is licensed under the MIT License. See the LICENSE file for details.

### Acknowledgments
1. Inspired by resilience patterns such as Circuit Breaker and Load Shedding.
2. Built with .NET 9.0.
