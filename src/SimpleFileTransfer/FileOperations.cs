using System;
using System.IO;
using System.Security.Cryptography;

namespace SimpleFileTransfer;

/// <summary>
/// Provides utility methods for file operations in the SimpleFileTransfer application.
/// </summary>
public static class FileOperations
{
    /// <summary>
    /// Calculates the SHA256 hash of a file.
    /// </summary>
    /// <param name="filepath">The path to the file to hash.</param>
    /// <returns>A lowercase hexadecimal string representation of the file's SHA256 hash.</returns>
    public static string CalculateHash(string filepath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filepath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Displays the progress of a file transfer operation on the console.
    /// </summary>
    /// <param name="current">The number of bytes transferred so far.</param>
    /// <param name="total">The total number of bytes to transfer.</param>
    /// <param name="bytesPerSecond">The current transfer speed in bytes per second.</param>
    public static void DisplayProgress(long current, long total, long bytesPerSecond)
    {
        var percentage = current * 100 / total;
        var mbps = bytesPerSecond / 1024.0 / 1024.0;
        Console.Write($"\rProgress: {current:N0}/{total:N0} bytes ({percentage}%) - {mbps:F2} MB/s");
    }
}
