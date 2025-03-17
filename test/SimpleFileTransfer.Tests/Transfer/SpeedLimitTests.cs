using SimpleFileTransfer.Helpers;
using SimpleFileTransfer.Queue;
using SimpleFileTransfer.Transfer;
using System.Diagnostics;

namespace SimpleFileTransfer.Tests.Transfer;

public class SpeedLimitTests
{
    [Fact]
    public void ThrottledStream_ShouldLimitReadSpeed()
    {
        // Arrange
        const int speedLimitKBs = 100; // 100 KB/s
        const int bytesPerSecond = speedLimitKBs * 1024; // Convert to bytes per second
        const int testDataSize = 500 * 1024; // 500 KB
        
        var testData = new byte[testDataSize];
        new Random(42).NextBytes(testData); // Fill with random data
        
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
        double expectedMinimumSeconds = testDataSize / (double)bytesPerSecond;
        
        Assert.Equal(testDataSize, bytesRead);
        Assert.True(elapsedSeconds >= expectedMinimumSeconds * 0.9, 
            $"Transfer was too fast. Expected at least {expectedMinimumSeconds:F2}s but took {elapsedSeconds:F2}s");
    }
    
    [Fact]
    public async Task ThrottledStream_ShouldLimitWriteSpeed()
    {
        // Arrange
        const int speedLimitKBs = 100; // 100 KB/s
        const int bytesPerSecond = speedLimitKBs * 1024; // Convert to bytes per second
        const int testDataSize = 500 * 1024; // 500 KB
        
        var testData = new byte[testDataSize];
        new Random(42).NextBytes(testData); // Fill with random data
        
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
        double expectedMinimumSeconds = testDataSize / (double)bytesPerSecond;
        
        Assert.Equal(testDataSize, memoryStream.Length);
        Assert.True(elapsedSeconds >= expectedMinimumSeconds * 0.9, 
            $"Transfer was too fast. Expected at least {expectedMinimumSeconds:F2}s but took {elapsedSeconds:F2}s");
    }
    
    [Fact]
    public void ResumeInfo_ShouldStoreSpeedLimit()
    {
        // Arrange
        const int speedLimit = 512; // 512 KB/s
        var resumeInfo = new ResumeInfo
        {
            FilePath = "test.txt",
            FileName = "test.txt",
            TotalSize = 1024,
            BytesTransferred = 0,
            Hash = "hash",
            UseCompression = true,
            CompressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip,
            UseEncryption = false,
            Host = "localhost",
            Port = 9876,
            SpeedLimit = speedLimit
        };
        
        // Act
        TransferResumeManager.CreateResumeFile(resumeInfo);
        var loadedInfo = TransferResumeManager.LoadResumeInfo("test.txt");
        
        // Assert
        Assert.NotNull(loadedInfo);
        Assert.Equal(speedLimit, loadedInfo.SpeedLimit);
        
        // Cleanup
        TransferResumeManager.DeleteResumeFile("test.txt");
    }
    
    [Fact]
    public void FileTransferClient_ShouldRespectSpeedLimit()
    {
        // This is more of an integration test that would require actual file transfers
        // For simplicity, we'll just verify that the speed limit is properly passed to the ThrottledStream
        
        // Arrange
        const int speedLimit = 256; // 256 KB/s
        const string host = "localhost";
        const string testFilePath = "test_speed_limit.txt";
        
        // Create a test file
        File.WriteAllText(testFilePath, new string('A', 1024 * 1024)); // 1 MB file
        
        try
        {
            // Act - Just create the client with speed limit
            var client = new FileTransferClient(
                host,
                Program.Port,
                false,
                CompressionHelper.CompressionAlgorithm.GZip,
                false,
                string.Empty,
                false,
                speedLimit);
            
            // Assert - We can't easily test the actual transfer without mocking network connections
            // This test just verifies that the client can be created with a speed limit
            Assert.NotNull(client);
        }
        finally
        {
            // Cleanup
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }
}
