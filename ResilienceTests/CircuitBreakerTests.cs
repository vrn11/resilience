namespace ResilienceTests;
using Resilience.CircuitBreaker;
using Resilience.Configuration;
using Resilience.Caching;
using Moq;
using Xunit;

public class CircuitBreakerTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenCircuitIsClosed()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            OpenTimeout = TimeSpan.FromSeconds(5)
        };
        var mockCache = new Mock<IResilienceDistributedCache>();
        var circuitBreaker = new CircuitBreaker(options, mockCache.Object);

        // Act
        var result = await circuitBreaker.ExecuteAsync(() => Task.FromResult("Success"));

        // Assert
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenCircuitIsOpen()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            OpenTimeout = TimeSpan.FromSeconds(5)
        };
        var mockCache = new Mock<IResilienceDistributedCache>();
        var circuitBreaker = new BasicCircuitBreaker(options, mockCache.Object);

        // Simulate circuit breaker state as Open
        var setStateMethod = circuitBreaker.GetType()
            .GetMethod("SetStateAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (setStateMethod == null)
        {
            throw new InvalidOperationException("Method 'SetStateAsync' not found.");
        }

        var setStateTask = setStateMethod.Invoke(circuitBreaker, new object[] { CircuitBreakerState.Open });

        if (setStateTask == null)
        {
            throw new InvalidOperationException("Failed to invoke 'SetStateAsync'.");
        }

        await (Task)setStateTask;

        // Act & Assert
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(async () =>
            await circuitBreaker.ExecuteAsync(() => Task.FromResult("Success")));
    }

    [Fact]
    public async Task IncrementFailureAsync_ShouldOpenCircuit_WhenFailureThresholdIsExceeded()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            OpenTimeout = TimeSpan.FromSeconds(5)
        };
        var mockCache = new Mock<IResilienceDistributedCache>();
        var circuitBreaker = new BasicCircuitBreaker(options, mockCache.Object);

        // Cache reflection methods
        var incrementFailureMethod = circuitBreaker.GetType()
            .GetMethod("IncrementFailureAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (incrementFailureMethod == null)
        {
            throw new InvalidOperationException("Method 'IncrementFailureAsync' not found.");
        }

        var getStateMethod = circuitBreaker.GetType()
            .GetMethod("GetCurrentStateAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (getStateMethod == null)
        {
            throw new InvalidOperationException("Method 'GetCurrentStateAsync' not found.");
        }

        // Act
        for (int i = 0; i < 3; i++)
        {
            var incrementFailureTask = incrementFailureMethod.Invoke(circuitBreaker, null);

            if (incrementFailureTask == null)
            {
                throw new InvalidOperationException("Failed to invoke 'IncrementFailureAsync'.");
            }

            await (Task)incrementFailureTask;
        }

        var getStateTask = getStateMethod.Invoke(circuitBreaker, null);

        if (getStateTask == null)
        {
            throw new InvalidOperationException("Failed to invoke 'GetCurrentStateAsync'.");
        }

        CircuitBreakerState state = await (Task<CircuitBreakerState>)getStateTask;

        // Assert
        Assert.Equal(CircuitBreakerState.Open, state);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFallback_WhenActionFails()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            OpenTimeout = TimeSpan.FromSeconds(5)
        };
        var mockCache = new Mock<IResilienceDistributedCache>();
        var circuitBreaker = new BasicCircuitBreaker(options, mockCache.Object);

        // Act
        var result = await circuitBreaker.ExecuteAsync(
            () => Task.FromException<string>(new Exception("Action failed")),
            () => Task.FromResult("Fallback"));

        // Assert
        Assert.Equal("Fallback", result);
    }
}