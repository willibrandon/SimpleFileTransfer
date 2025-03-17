using Moq;
using SimpleFileTransfer.Core;

namespace SimpleFileTransfer.Tests.Core;

public class CompressedFileTransferTests : IDisposable
{
    private readonly string _testDir;

    public CompressedFileTransferTests()
    {
        // Create a unique test directory
        _testDir = Path.Combine(Path.GetTempPath(), "CompressedFileTransferTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenDecoratedTransferIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CompressedFileTransfer(null!));
    }

    [Fact]
    public void Transfer_CallsDecoratedTransfer()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "source.txt");
        var destFile = Path.Combine(_testDir, "destination.txt");
        var content = "Test content for compressed transfer";
        File.WriteAllText(sourceFile, content);

        // Create a mock decorated transfer
        var mockTransfer = new Mock<FileTransfer>();
        mockTransfer.Setup(t => t.Transfer(It.IsAny<string>(), It.Is<string>(s => s == destFile)));

        var compressedTransfer = new CompressedFileTransfer(mockTransfer.Object);

        // Act
        compressedTransfer.Transfer(sourceFile, destFile);

        // Assert
        mockTransfer.Verify(t => t.Transfer(It.IsAny<string>(), It.Is<string>(s => s == destFile)), Times.Once);
    }

    [Fact]
    public void Transfer_ThrowsArgumentException_WhenSourcePathIsEmpty()
    {
        // Arrange
        var mockTransfer = new Mock<FileTransfer>();
        var compressedTransfer = new CompressedFileTransfer(mockTransfer.Object);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => compressedTransfer.Transfer("", "destination.txt"));
        Assert.Equal("sourcePath", ex.ParamName);
    }

    [Fact]
    public void Transfer_ThrowsArgumentException_WhenDestinationPathIsEmpty()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "source.txt");
        File.WriteAllText(sourceFile, "Test content");
        
        var mockTransfer = new Mock<FileTransfer>();
        var compressedTransfer = new CompressedFileTransfer(mockTransfer.Object);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => compressedTransfer.Transfer(sourceFile, ""));
        Assert.Equal("destinationPath", ex.ParamName);
    }

    [Fact]
    public void Transfer_DeletesTempFile_AfterTransfer()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "source.txt");
        var destFile = Path.Combine(_testDir, "destination.txt");
        var content = "Test content for temp file cleanup";
        File.WriteAllText(sourceFile, content);

        string? tempFilePath = null;
        
        // Create a mock decorated transfer that captures the temp file path
        var mockTransfer = new Mock<FileTransfer>();
        mockTransfer.Setup(t => t.Transfer(It.IsAny<string>(), It.Is<string>(s => s == destFile)))
            .Callback<string, string>((source, dest) => tempFilePath = source);

        var compressedTransfer = new CompressedFileTransfer(mockTransfer.Object);

        // Act
        compressedTransfer.Transfer(sourceFile, destFile);

        // Assert
        Assert.NotNull(tempFilePath);
        Assert.False(File.Exists(tempFilePath), "Temporary file should be deleted after transfer");
    }

    [Fact]
    public void Transfer_HandlesExceptions_AndCleansTempFile()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "source.txt");
        var destFile = Path.Combine(_testDir, "destination.txt");
        var content = "Test content for exception handling";
        File.WriteAllText(sourceFile, content);

        string? tempFilePath = null;
        
        // Create a mock decorated transfer that captures the temp file path and throws an exception
        var mockTransfer = new Mock<FileTransfer>();
        mockTransfer.Setup(t => t.Transfer(It.IsAny<string>(), It.Is<string>(s => s == destFile)))
            .Callback<string, string>((source, dest) => tempFilePath = source)
            .Throws(new IOException("Test exception"));

        var compressedTransfer = new CompressedFileTransfer(mockTransfer.Object);

        // Act & Assert
        Assert.Throws<IOException>(() => compressedTransfer.Transfer(sourceFile, destFile));
        
        // Verify temp file is cleaned up even when an exception occurs
        Assert.NotNull(tempFilePath);
        Assert.False(File.Exists(tempFilePath), "Temporary file should be deleted after exception");
    }

    [Fact]
    public void Transfer_UsesCompression()
    {
        // This test verifies that the compressed content is different from the original
        // Arrange
        var sourceFile = Path.Combine(_testDir, "source.txt");
        var destFile = Path.Combine(_testDir, "destination.txt");
        var tempFile = Path.Combine(_testDir, "temp.txt");
        var content = new string('A', 1000); // Create a string that will compress well
        File.WriteAllText(sourceFile, content);

        string? tempFilePath = null;
        
        // Create a mock decorated transfer that copies the temp file to a location we can examine
        var mockTransfer = new Mock<FileTransfer>();
        mockTransfer.Setup(t => t.Transfer(It.IsAny<string>(), It.Is<string>(s => s == destFile)))
            .Callback<string, string>((source, dest) => 
            {
                tempFilePath = source;
                File.Copy(source, tempFile, true);
            });

        var compressedTransfer = new CompressedFileTransfer(mockTransfer.Object);

        // Act
        compressedTransfer.Transfer(sourceFile, destFile);

        // Assert
        Assert.NotNull(tempFilePath);
        Assert.True(File.Exists(tempFile), "Temp file should have been copied for verification");
        
        // The compressed content should be smaller than the original
        var compressedSize = new FileInfo(tempFile).Length;
        var originalSize = new FileInfo(sourceFile).Length;
        
        Assert.True(compressedSize < originalSize, 
            $"Compressed size ({compressedSize}) should be less than original size ({originalSize})");
        
        // Clean up the temp file
        File.Delete(tempFile);
    }
}
