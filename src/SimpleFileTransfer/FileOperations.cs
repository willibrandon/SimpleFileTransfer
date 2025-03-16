using System;
using System.IO;
using System.Security.Cryptography;

namespace SimpleFileTransfer;

public static class FileOperations
{
    public static string CalculateHash(string filepath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filepath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static void DisplayProgress(long current, long total, long bytesPerSecond)
    {
        var percentage = current * 100 / total;
        var mbps = bytesPerSecond / 1024.0 / 1024.0;
        Console.Write($"\rProgress: {current:N0}/{total:N0} bytes ({percentage}%) - {mbps:F2} MB/s");
    }
}
