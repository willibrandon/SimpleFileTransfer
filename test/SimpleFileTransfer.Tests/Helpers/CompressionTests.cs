using SimpleFileTransfer.Helpers;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace SimpleFileTransfer.Tests.Helpers;

public class CompressionTests
{
    [Theory]
    [InlineData(CompressionHelper.CompressionAlgorithm.GZip)]
    [InlineData(CompressionHelper.CompressionAlgorithm.Brotli)]
    public void Compress_Decompress_RoundTrip_Success(CompressionHelper.CompressionAlgorithm algorithm)
    {
        // Arrange
        var originalData = new string('A', 10000); // Highly compressible data
        var originalBytes = Encoding.UTF8.GetBytes(originalData);
        
        // Create temporary files
        var tempOriginalFile = Path.GetTempFileName();
        var tempCompressedFile = Path.GetTempFileName();
        var tempDecompressedFile = Path.GetTempFileName();
        
        try
        {
            // Write original data to file
            File.WriteAllBytes(tempOriginalFile, originalBytes);
            
            // Act - Compress
            using (var sourceStream = File.OpenRead(tempOriginalFile))
            using (var destinationStream = File.Create(tempCompressedFile))
            {
                CompressionHelper.Compress(sourceStream, destinationStream, algorithm);
            }
            
            // Act - Decompress
            using (var sourceStream = File.OpenRead(tempCompressedFile))
            using (var destinationStream = File.Create(tempDecompressedFile))
            {
                CompressionHelper.Decompress(sourceStream, destinationStream, algorithm);
            }
            
            // Assert
            var decompressedBytes = File.ReadAllBytes(tempDecompressedFile);
            var decompressedData = Encoding.UTF8.GetString(decompressedBytes);
            
            Assert.Equal(originalData, decompressedData);
            
            // Verify compression actually reduced the size
            var originalSize = new FileInfo(tempOriginalFile).Length;
            var compressedSize = new FileInfo(tempCompressedFile).Length;
            Assert.True(compressedSize < originalSize, "Compressed data should be smaller than original");
            
            // Calculate compression ratio
            var ratio = CompressionHelper.GetCompressionRatio(originalSize, compressedSize);
            Console.WriteLine($"{algorithm} compression ratio: {ratio:F2}%");
            Assert.True(ratio > 0, "Compression ratio should be positive");
        }
        finally
        {
            // Clean up temporary files
            if (File.Exists(tempOriginalFile)) File.Delete(tempOriginalFile);
            if (File.Exists(tempCompressedFile)) File.Delete(tempCompressedFile);
            if (File.Exists(tempDecompressedFile)) File.Delete(tempDecompressedFile);
        }
    }
    
    [Theory]
    [InlineData(CompressionHelper.CompressionAlgorithm.GZip)]
    [InlineData(CompressionHelper.CompressionAlgorithm.Brotli)]
    public void Compression_Algorithm_Performance_Comparison(CompressionHelper.CompressionAlgorithm algorithm)
    {
        // Arrange
        var originalData = new string('A', 1000000); // 1MB of highly compressible data
        var originalBytes = Encoding.UTF8.GetBytes(originalData);
        
        // Create temporary files
        var tempOriginalFile = Path.GetTempFileName();
        var tempCompressedFile = Path.GetTempFileName();
        
        try
        {
            // Write original data to file
            File.WriteAllBytes(tempOriginalFile, originalBytes);
            
            // Act - Compress and measure time
            var compressionStopwatch = new System.Diagnostics.Stopwatch();
            compressionStopwatch.Start();
            
            using (var sourceStream = File.OpenRead(tempOriginalFile))
            using (var destinationStream = File.Create(tempCompressedFile))
            {
                CompressionHelper.Compress(sourceStream, destinationStream, algorithm);
            }
            
            compressionStopwatch.Stop();
            
            // Calculate compression ratio
            var originalSize = new FileInfo(tempOriginalFile).Length;
            var compressedSize = new FileInfo(tempCompressedFile).Length;
            var ratio = CompressionHelper.GetCompressionRatio(originalSize, compressedSize);
            
            // Output results
            Console.WriteLine($"{algorithm} compression:");
            Console.WriteLine($"  Original size: {originalSize:N0} bytes");
            Console.WriteLine($"  Compressed size: {compressedSize:N0} bytes");
            Console.WriteLine($"  Compression ratio: {ratio:F2}%");
            Console.WriteLine($"  Compression time: {compressionStopwatch.ElapsedMilliseconds} ms");
            
            // Assert
            Assert.True(compressedSize < originalSize, "Compressed data should be smaller than original");
        }
        finally
        {
            // Clean up temporary files
            if (File.Exists(tempOriginalFile)) File.Delete(tempOriginalFile);
            if (File.Exists(tempCompressedFile)) File.Delete(tempCompressedFile);
        }
    }
    
    [Fact]
    public void GetCompressionRatio_CalculatesCorrectly()
    {
        // Arrange
        var originalSize = 1000L;
        var compressedSize = 250L;
        
        // Act
        var ratio = CompressionHelper.GetCompressionRatio(originalSize, compressedSize);
        
        // Assert
        Assert.Equal(75.0, ratio);
    }
    
    [Fact]
    public void GetCompressionRatio_HandlesZeroOriginalSize()
    {
        // Arrange
        var originalSize = 0L;
        var compressedSize = 0L;
        
        // Act
        var ratio = CompressionHelper.GetCompressionRatio(originalSize, compressedSize);
        
        // Assert
        Assert.Equal(0.0, ratio);
    }
} 