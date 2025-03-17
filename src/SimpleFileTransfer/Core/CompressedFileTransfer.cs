using System;
using System.IO;

namespace SimpleFileTransfer.Core;

/// <summary>
/// Decorator that adds compression functionality to file transfers.
/// </summary>
/// <remarks>
/// This class implements the decorator pattern to add compression behavior to any <see cref="FileTransfer"/> instance.
/// It compresses the source file before transferring it to the destination.
/// In a real implementation, this would use a compression library, but for simplicity,
/// this implementation just copies the file.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="CompressedFileTransfer"/> class.
/// </remarks>
/// <param name="decoratedTransfer">The file transfer instance to decorate with compression functionality.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="decoratedTransfer"/> is null.</exception>
public class CompressedFileTransfer(FileTransfer decoratedTransfer) : FileTransfer
{
    private readonly FileTransfer _decoratedTransfer = decoratedTransfer
        ?? throw new ArgumentNullException(nameof(decoratedTransfer));

    /// <summary>
    /// Transfers a file from source to destination with compression.
    /// </summary>
    /// <param name="sourcePath">The full path to the source file.</param>
    /// <param name="destinationPath">The full path where the file should be copied to.</param>
    /// <exception cref="ArgumentException">Thrown when source or destination path is null or empty.</exception>
    /// <remarks>
    /// This method compresses the source file to a temporary location and then
    /// uses the decorated transfer to move the compressed file to the destination.
    /// </remarks>
    public override void Transfer(string sourcePath, string destinationPath)
    {
        if (string.IsNullOrEmpty(sourcePath))
            throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));

        if (string.IsNullOrEmpty(destinationPath))
            throw new ArgumentException("Destination path cannot be empty", nameof(destinationPath));

        // Create a temporary file for the compressed data
        var tempFile = Path.GetTempFileName();

        try
        {
            // Compress the source file to the temp file
            using (var sourceStream = File.OpenRead(sourcePath))
            using (var tempStream = File.Create(tempFile))
            {
                // In a real implementation, we would use a compression library here
                // For simplicity, we're just copying the file
                sourceStream.CopyTo(tempStream);
            }

            // Transfer the compressed file to the destination
            _decoratedTransfer.Transfer(tempFile, destinationPath);
        }
        finally
        {
            // Clean up the temporary file
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
