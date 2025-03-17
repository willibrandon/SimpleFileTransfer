using SimpleFileTransfer.Core;

namespace SimpleFileTransfer.Tests.Core;

public class BasicFileTransferTests : IDisposable
{
    private readonly string _testDir;

    public BasicFileTransferTests()
    {
        // Create a unique test directory
        _testDir = Path.Combine(Path.GetTempPath(), "BasicFileTransferTests_" + Guid.NewGuid());
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
    public void Transfer_CopiesFileCorrectly()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "source.txt");
        var destFile = Path.Combine(_testDir, "destination.txt");
        var content = "Test content for basic file transfer";
        File.WriteAllText(sourceFile, content);

        var transfer = new BasicFileTransfer();

        // Act
        transfer.Transfer(sourceFile, destFile);

        // Assert
        Assert.True(File.Exists(destFile));
        Assert.Equal(content, File.ReadAllText(destFile));
    }

    [Fact]
    public void Transfer_CreatesDestinationDirectory()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "source.txt");
        var destDir = Path.Combine(_testDir, "nested", "directory");
        var destFile = Path.Combine(destDir, "destination.txt");
        var content = "Test content for directory creation";
        File.WriteAllText(sourceFile, content);

        var transfer = new BasicFileTransfer();

        // Act
        transfer.Transfer(sourceFile, destFile);

        // Assert
        Assert.True(Directory.Exists(destDir));
        Assert.True(File.Exists(destFile));
        Assert.Equal(content, File.ReadAllText(destFile));
    }

    [Fact]
    public void Transfer_OverwritesExistingFile()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "source.txt");
        var destFile = Path.Combine(_testDir, "destination.txt");
        var initialContent = "Initial content";
        var newContent = "New content for overwrite test";

        File.WriteAllText(sourceFile, newContent);
        File.WriteAllText(destFile, initialContent);

        var transfer = new BasicFileTransfer();

        // Act
        transfer.Transfer(sourceFile, destFile);

        // Assert
        Assert.Equal(newContent, File.ReadAllText(destFile));
    }

    [Fact]
    public void Transfer_ThrowsArgumentException_WhenSourcePathIsEmpty()
    {
        // Arrange
        var transfer = new BasicFileTransfer();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => transfer.Transfer("", "destination.txt"));
        Assert.Equal("sourcePath", ex.ParamName);
    }

    [Fact]
    public void Transfer_ThrowsArgumentException_WhenDestinationPathIsEmpty()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "source.txt");
        File.WriteAllText(sourceFile, "Test content");
        var transfer = new BasicFileTransfer();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => transfer.Transfer(sourceFile, ""));
        Assert.Equal("destinationPath", ex.ParamName);
    }

    [Fact]
    public void Transfer_ThrowsFileNotFoundException_WhenSourceFileDoesNotExist()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDir, "nonexistent.txt");
        var destFile = Path.Combine(_testDir, "destination.txt");
        var transfer = new BasicFileTransfer();

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => transfer.Transfer(nonExistentFile, destFile));
    }
}
