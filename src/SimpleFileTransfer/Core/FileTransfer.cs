using System;
using System.IO;

namespace SimpleFileTransfer.Core;

/// <summary>
/// Base abstract class for file transfer operations.
/// Provides core functionality for transferring files from one location to another.
/// </summary>
/// <remarks>
/// This class serves as the base component in the decorator pattern implementation
/// for file transfer operations. It can be extended with additional behaviors
/// such as compression and encryption.
/// </remarks>
public abstract class FileTransfer
{
    /// <summary>
    /// Transfers a file from source to destination.
    /// </summary>
    /// <param name="sourcePath">The full path to the source file.</param>
    /// <param name="destinationPath">The full path where the file should be copied to.</param>
    /// <exception cref="ArgumentException">Thrown when source or destination path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the source file does not exist.</exception>
    public virtual void Transfer(string sourcePath, string destinationPath)
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

        // Perform basic file copy
        File.Copy(sourcePath, destinationPath, true);
    }
}
