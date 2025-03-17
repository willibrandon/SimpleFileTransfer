using SimpleFileTransfer.Transfer;
using System;
using System.IO;

namespace SimpleFileTransfer.Core;

/// <summary>
/// Decorator for file transfer operations that limits the transfer speed.
/// </summary>
/// <remarks>
/// This class extends the functionality of a <see cref="FileTransfer"/> by adding
/// speed limiting capabilities. It uses the <see cref="ThrottledStream"/> to control
/// the rate at which data is transferred.
/// </remarks>
public class ThrottledFileTransfer : FileTransfer
{
    private readonly FileTransfer _decoratedTransfer;
    private readonly int _speedLimitKBs;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ThrottledFileTransfer"/> class.
    /// </summary>
    /// <param name="decoratedTransfer">The file transfer to decorate.</param>
    /// <param name="speedLimitKBs">The speed limit in KB/s.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="decoratedTransfer"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="speedLimitKBs"/> is less than or equal to zero.</exception>
    public ThrottledFileTransfer(FileTransfer decoratedTransfer, int speedLimitKBs)
    {
        _decoratedTransfer = decoratedTransfer ?? throw new ArgumentNullException(nameof(decoratedTransfer));
        
        if (speedLimitKBs <= 0)
            throw new ArgumentOutOfRangeException(nameof(speedLimitKBs), "Speed limit must be greater than zero.");
            
        _speedLimitKBs = speedLimitKBs;
    }
    
    /// <summary>
    /// Transfers a file from source to destination with speed limiting.
    /// </summary>
    /// <param name="sourcePath">The full path to the source file.</param>
    /// <param name="destinationPath">The full path where the file should be copied to.</param>
    /// <exception cref="ArgumentException">Thrown when source or destination path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the source file does not exist.</exception>
    public override void Transfer(string sourcePath, string destinationPath)
    {
        if (string.IsNullOrEmpty(sourcePath))
            throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));

        if (string.IsNullOrEmpty(destinationPath))
            throw new ArgumentException("Destination path cannot be empty", nameof(destinationPath));

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Source file not found", sourcePath);

        // Create destination directory if it doesn't exist
        var destinationDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        // Convert KB/s to bytes per second
        long bytesPerSecond = _speedLimitKBs * 1024;
        
        // Perform throttled file copy
        using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
        using (var throttledStream = new ThrottledStream(sourceStream, bytesPerSecond))
        using (var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
        {
            throttledStream.CopyTo(destinationStream);
        }
    }
} 