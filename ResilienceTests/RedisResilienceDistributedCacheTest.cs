namespace ResilienceTests;

using Resilience.Caching;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using StackExchange.Redis;
using Xunit;
    public class RedisResilienceDistributedCacheTests
    {
        private readonly Mock<IConnectionMultiplexer> _mockConnectionMultiplexer;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly RedisResilienceDistributedCache _cache;

        public RedisResilienceDistributedCacheTests()
        {
            _mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();

            _mockConnectionMultiplexer
                .Setup(conn => conn.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDatabase.Object);

            var options = new CachingOptions
            {
                ConnectionString = "localhost",
                CircuitBreakerFailureThreshold = 5
            };

            _cache = new RedisResilienceDistributedCache(options, _mockConnectionMultiplexer.Object);
        }

        [Fact]
        public async Task GetAsync_ShouldReturnValue_WhenKeyExists()
        {
            // Arrange
            string key = "test-key";
            string expectedValue = "test-value";

            _mockDatabase
                .Setup(db => db.StringGetAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(expectedValue);

            // Act
            var result = await _cache.GetAsync(key);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public async Task GetAsync_ShouldThrowArgumentNullException_WhenKeyIsNull()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _cache.GetAsync(null!));
        }

        [Fact]
        public async Task SetAsync_ShouldStoreValue_WhenCalled()
        {
            // Arrange
            string key = "test-key";
            byte[] value = System.Text.Encoding.UTF8.GetBytes("test-value");
            var options = new DistributedCacheEntryOptions();

            _mockDatabase
                .Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), null, When.Always, CommandFlags.None))
                .ReturnsAsync(true);

            // Act
            await _cache.SetAsync(key, value, options);

            // Assert
            _mockDatabase.Verify(db => db.StringSetAsync(key, value, null, When.Always, It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task SetAsync_ShouldThrowArgumentNullException_WhenKeyOrValueIsNull()
        {
            // Arrange
            var options = new DistributedCacheEntryOptions();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _cache.SetAsync(null!, "value", options));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _cache.SetAsync("key", null!, options));
        }

        [Fact]
        public async Task RemoveAsync_ShouldDeleteKey_WhenCalled()
        {
            // Arrange
            string key = "test-key";

            _mockDatabase
                .Setup(db => db.KeyDeleteAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _cache.RemoveAsync(key);

            // Assert
            _mockDatabase.Verify(db => db.KeyDeleteAsync(key, It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task RemoveAsync_ShouldThrowArgumentNullException_WhenKeyIsNull()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _cache.RemoveAsync(null!));
        }

        [Fact]
        public async Task RefreshAsync_ShouldResetExpiration_WhenKeyExists()
        {
            // Arrange
            string key = "test-key";
            string value = "test-value";

            _mockDatabase
                .Setup(db => db.StringGetAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(value);

            _mockDatabase
                .Setup(db => db.StringSetAsync(key, value, null, When.Always, It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _cache.RefreshAsync(key);

            // Assert
            _mockDatabase.Verify(db => db.StringSetAsync(key, value, null, When.Always, It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_ShouldThrowArgumentNullException_WhenKeyIsNull()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _cache.RefreshAsync(null!));
        }

        [Fact]
        public async Task AtomicUpdateAsync_ShouldUpdateValue_WhenKeyExists()
        {
            // Arrange
            string key = "test-key";
            string newValue = "new-value";

            // Mock the RedisResult to simulate a successful update
            RedisResult redisResult = RedisResult.Create(1);
            _mockDatabase
                .Setup(db => db.ScriptEvaluateAsync(
                    It.IsAny<string>(), 
                    It.IsAny<RedisKey[]>(), 
                    It.IsAny<RedisValue[]>(), 
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(redisResult);

            // Act
            var result = await _cache.AtomicUpdateAsync(key, newValue);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task AtomicUpdateAsync_ShouldReturnFalse_WhenKeyDoesNotExist()
        {
            // Arrange
            string key = "test-key";
            string newValue = "new-value";

            // Mock the RedisResult to simulate a failed update
            var redisResult = RedisResult.Create(0);

            _mockDatabase
                .Setup(db => db.ScriptEvaluateAsync(
                    It.IsAny<string>(), 
                    It.IsAny<RedisKey[]>(), 
                    It.IsAny<RedisValue[]>(), 
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(redisResult);


            // Act
            var result = await _cache.AtomicUpdateAsync(key, newValue);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IncrementFailuresAsync_ShouldIncrementFailureCount()
        {
            // Arrange
            var redisResult = RedisResult.Create(1);

            _mockDatabase
                .Setup(db => db.ScriptEvaluateAsync(
                    It.IsAny<string>(), 
                    It.IsAny<RedisKey[]>(), 
                    It.IsAny<RedisValue[]>(), 
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(redisResult);

            // Act
            await _cache.IncrementFailuresAsync();

            // Assert
            _mockDatabase.Verify(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(), 
                It.IsAny<RedisKey[]>(), 
                It.IsAny<RedisValue[]>(), 
                It.IsAny<CommandFlags>()), Times.Once);
        }
    }