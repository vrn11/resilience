namespace ResilienceTests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;
using Resilience.LoadShedder;
using Resilience.Caching;

public class ResponsiveLoadShedderTests
{
    private readonly Mock<IDistributedCache> _mockDistributedCache;
    private readonly Func<double> _mockLoadMonitor;
    private readonly LoadShedderOptions _options;

    public ResponsiveLoadShedderTests()
    {
        _mockDistributedCache = new Mock<IDistributedCache>();
        _mockLoadMonitor = () => 0.5; // Default load monitor returns 50% load
        _options = new LoadShedderOptions { LoadThreshold = 0.7 }; // Default threshold is 70%
    }

    [Fact]
    public void Execute_ShouldRunAction_WhenLoadIsBelowDynamicThreshold()
    {
        // Arrange
        var loadShedder = new ResponsiveLoadShedder(_mockLoadMonitor, _options, _mockDistributedCache.Object);

        // Act
        var result = loadShedder.Execute(RequestPriority.High, () => "Action executed");

        // Assert
        Assert.Equal("Action executed", result);
    }

    [Fact]
    public void Execute_ShouldRunFallback_WhenLoadIsAboveDynamicThresholdAndPriorityIsLow()
    {
        // Arrange
        _mockDistributedCache
            .Setup(cache => cache.Get(
                "responsive-current-load"))
            .Returns(System.Text.Encoding.UTF8.GetBytes("0.8")); // Cached load is 80%

        var loadShedder = new ResponsiveLoadShedder(_mockLoadMonitor, _options, _mockDistributedCache.Object);

        // Act
        var result = loadShedder.Execute(RequestPriority.Low, () => "Action executed", () => "Fallback executed");

        // Assert
        Assert.Equal("Fallback executed", result);
    }

    [Fact]
    public void Execute_ShouldThrowException_WhenLoadIsAboveDynamicThresholdAndNoFallbackProvided()
    {
        // Arrange
        _mockDistributedCache
            .Setup(cache => cache.Get(
                "responsive-current-load"))
            .Returns(System.Text.Encoding.UTF8.GetBytes("0.8")); // Cached load is 80%

        var loadShedder = new ResponsiveLoadShedder(_mockLoadMonitor, _options, _mockDistributedCache.Object);

        // Act & Assert
        Assert.Throws<Exception>(() =>
            loadShedder.Execute(RequestPriority.Low, () => "Action executed"));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRunAction_WhenLoadIsBelowDynamicThreshold()
    {
        // Arrange
        var loadShedder = new ResponsiveLoadShedder(_mockLoadMonitor, _options, _mockDistributedCache.Object);

        // Act
        var result = await loadShedder.ExecuteAsync(RequestPriority.High, async () =>
        {
            await Task.Delay(10);
            return "Action executed";
        });

        // Assert
        Assert.Equal("Action executed", result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRunFallback_WhenLoadIsAboveDynamicThresholdAndPriorityIsLow()
    {
        // Arrange
        _mockDistributedCache
            .Setup(cache => cache.GetAsync(
                "responsive-current-load",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes("0.8")); // Cached load is 80%

        var loadShedder = new ResponsiveLoadShedder(_mockLoadMonitor, _options, _mockDistributedCache.Object);

        // Act
        var result = await loadShedder.ExecuteAsync(RequestPriority.Low, async () =>
        {
            await Task.Delay(10);
            return "Action executed";
        }, async () =>
        {
            await Task.Delay(10);
            return "Fallback executed";
        });

        // Assert
        Assert.Equal("Fallback executed", result);
    }

    [Fact]
    public void GetCurrentLoad_ShouldReturnLoadFromDistributedCache_WhenCacheIsConfigured()
    {
        // Arrange
        _mockDistributedCache
            .Setup(cache => cache.Get(
                "responsive-current-load"))
            .Returns(System.Text.Encoding.UTF8.GetBytes("0.6")); // Cached load is 60%

        var loadShedder = new ResponsiveLoadShedder(_mockLoadMonitor, _options, _mockDistributedCache.Object);

        // Act
        var currentLoad = loadShedder.GetCurrentLoad();

        // Assert
        Assert.Equal(0.6, currentLoad, precision: 10); 
    }

    [Fact]
    public void GetCurrentLoad_ShouldFallbackToLoadMonitor_WhenCacheIsNotConfigured()
    {
        // Arrange
        var loadShedder = new ResponsiveLoadShedder(_mockLoadMonitor, _options);

        // Act
        var currentLoad = loadShedder.GetCurrentLoad();

        // Assert
        Assert.Equal(0.5, currentLoad); // Default load monitor returns 50%
    }

    [Fact]
    public async Task UpdateThresholdAsync_ShouldUpdateThresholdAndCache()
    {
        // Arrange
        var loadShedder = new ResponsiveLoadShedder(_mockLoadMonitor, _options, _mockDistributedCache.Object);

        // Act
        await loadShedder.UpdateThresholdAsync(0.9);

        // Assert
        Assert.Equal(0.9, loadShedder.BaseLoadThreshold);
        _mockDistributedCache.Verify(cache => cache.SetAsync(
            "responsive-load-threshold",
            It.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "0.9"),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void UpdateThreshold_ShouldUpdateThresholdAndCache()
    {
        // Arrange
        var loadShedder = new ResponsiveLoadShedder(_mockLoadMonitor, _options, _mockDistributedCache.Object);

        // Act
        loadShedder.UpdateThreshold(0.9);

        // Assert
        Assert.Equal(0.9, loadShedder.BaseLoadThreshold);
        _mockDistributedCache.Verify(cache => cache.Set(
            "responsive-load-threshold",
            It.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "0.9"),
            It.IsAny<DistributedCacheEntryOptions>()), Times.Once);
    }
}