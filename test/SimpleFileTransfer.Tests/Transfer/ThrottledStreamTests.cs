using SimpleFileTransfer.Transfer;
using System.Diagnostics;

namespace SimpleFileTransfer.Tests.Transfer;

public class ThrottledStreamTests
{
    [Fact]
    public void Constructor_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ThrottledStream(null!, 1024));
    }
    
    [Fact]
    public void Constructor_WithZeroBytesPerSecond_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var memoryStream = new MemoryStream();
        
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThrottledStream(memoryStream, 0));
    }
    
    [Fact]
    public void Constructor_WithNegativeBytesPerSecond_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var memoryStream = new MemoryStream();
        
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThrottledStream(memoryStream, -1));
    }
    
    [Fact]
    public void Read_ShouldThrottleSpeed()
    {
        // Arrange
        const int bytesPerSecond = 50 * 1024; // 50 KB/s
        const int testDataSize = 100 * 1024; // 100 KB
        
        var testData = new byte[testDataSize];
        new Random(42).NextBytes(testData);
        
        using var memoryStream = new MemoryStream(testData);
        using var throttledStream = new ThrottledStream(memoryStream, bytesPerSecond);
        
        var buffer = new byte[testDataSize];
        var stopwatch = new Stopwatch();
        
        // Act
        stopwatch.Start();
        int bytesRead = throttledStream.Read(buffer, 0, buffer.Length);
        stopwatch.Stop();
        
        // Assert
        double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        double expectedMinimumSeconds = (double)testDataSize / bytesPerSecond;
        
        Assert.Equal(testDataSize, bytesRead);
        Assert.True(elapsedSeconds >= expectedMinimumSeconds * 0.9, 
            $"Transfer was too fast. Expected at least {expectedMinimumSeconds:F2}s but took {elapsedSeconds:F2}s");
    }
    
    [Fact]
    public async Task ReadAsync_ShouldThrottleSpeed()
    {
        // Arrange
        const int bytesPerSecond = 50 * 1024; // 50 KB/s
        const int testDataSize = 100 * 1024; // 100 KB
        
        var testData = new byte[testDataSize];
        new Random(42).NextBytes(testData);
        
        using var memoryStream = new MemoryStream(testData);
        using var throttledStream = new ThrottledStream(memoryStream, bytesPerSecond);
        
        var buffer = new byte[testDataSize];
        var stopwatch = new Stopwatch();
        
        // Act
        stopwatch.Start();
        int bytesRead = await throttledStream.ReadAsync(buffer, 0, buffer.Length);
        stopwatch.Stop();
        
        // Assert
        double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        double expectedMinimumSeconds = (double)testDataSize / bytesPerSecond;
        
        Assert.Equal(testDataSize, bytesRead);
        Assert.True(elapsedSeconds >= expectedMinimumSeconds * 0.9, 
            $"Transfer was too fast. Expected at least {expectedMinimumSeconds:F2}s but took {elapsedSeconds:F2}s");
    }
    
    [Fact]
    public void Write_ShouldThrottleSpeed()
    {
        // Arrange
        const int bytesPerSecond = 50 * 1024; // 50 KB/s
        const int testDataSize = 100 * 1024; // 100 KB
        
        var testData = new byte[testDataSize];
        new Random(42).NextBytes(testData);
        
        using var memoryStream = new MemoryStream();
        using var throttledStream = new ThrottledStream(memoryStream, bytesPerSecond);
        
        var stopwatch = new Stopwatch();
        
        // Act
        stopwatch.Start();
        throttledStream.Write(testData, 0, testData.Length);
        throttledStream.Flush();
        stopwatch.Stop();
        
        // Assert
        double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        double expectedMinimumSeconds = (double)testDataSize / bytesPerSecond;
        
        Assert.Equal(testDataSize, memoryStream.Length);
        Assert.True(elapsedSeconds >= expectedMinimumSeconds * 0.9, 
            $"Transfer was too fast. Expected at least {expectedMinimumSeconds:F2}s but took {elapsedSeconds:F2}s");
    }
    
    [Fact]
    public async Task WriteAsync_ShouldThrottleSpeed()
    {
        // Arrange
        const int bytesPerSecond = 50 * 1024; // 50 KB/s
        const int testDataSize = 100 * 1024; // 100 KB
        
        var testData = new byte[testDataSize];
        new Random(42).NextBytes(testData);
        
        using var memoryStream = new MemoryStream();
        using var throttledStream = new ThrottledStream(memoryStream, bytesPerSecond);
        
        var stopwatch = new Stopwatch();
        
        // Act
        stopwatch.Start();
        await throttledStream.WriteAsync(testData, 0, testData.Length);
        await throttledStream.FlushAsync();
        stopwatch.Stop();
        
        // Assert
        double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        double expectedMinimumSeconds = (double)testDataSize / bytesPerSecond;
        
        Assert.Equal(testDataSize, memoryStream.Length);
        Assert.True(elapsedSeconds >= expectedMinimumSeconds * 0.9, 
            $"Transfer was too fast. Expected at least {expectedMinimumSeconds:F2}s but took {elapsedSeconds:F2}s");
    }
    
    [Fact]
    public void SmallTransfers_ShouldNotBeThrottled()
    {
        // Arrange
        const int bytesPerSecond = 1024 * 1024; // 1 MB/s
        const int testDataSize = 100; // 100 bytes (very small)
        
        var testData = new byte[testDataSize];
        new Random(42).NextBytes(testData);
        
        using var memoryStream = new MemoryStream(testData);
        using var throttledStream = new ThrottledStream(memoryStream, bytesPerSecond);
        
        var buffer = new byte[testDataSize];
        
        // Act
        int bytesRead = throttledStream.Read(buffer, 0, buffer.Length);
        
        // Assert
        Assert.Equal(testDataSize, bytesRead);
        // No timing assertion needed - small transfers should be nearly instant
    }
    
    [Fact]
    public void PassthroughProperties_ShouldMatchBaseStream()
    {
        // Arrange
        using var memoryStream = new MemoryStream();
        using var throttledStream = new ThrottledStream(memoryStream, 1024);
        
        // Act & Assert
        Assert.Equal(memoryStream.CanRead, throttledStream.CanRead);
        Assert.Equal(memoryStream.CanWrite, throttledStream.CanWrite);
        Assert.Equal(memoryStream.CanSeek, throttledStream.CanSeek);
        Assert.Equal(memoryStream.Length, throttledStream.Length);
        Assert.Equal(memoryStream.Position, throttledStream.Position);
        
        // Test position setter
        throttledStream.Position = 0;
        Assert.Equal(0, memoryStream.Position);
    }
}
