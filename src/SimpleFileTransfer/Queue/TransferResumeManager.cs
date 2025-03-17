using SimpleFileTransfer.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace SimpleFileTransfer.Queue;

/// <summary>
/// Manages the state of file transfers that can be resumed.
/// </summary>
public static class TransferResumeManager
{
    private static readonly string ResumeDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleFileTransfer",
        "Resume");
        
    // Create a static JsonSerializerOptions instance to be reused
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Contains information about a file transfer that can be resumed.
    /// </summary>
    public class ResumeInfo
    {
        /// <summary>
        /// The full path to the file being transferred.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// The name of the file being transferred.
        /// </summary>
        public string FileName { get; set; } = string.Empty;
        
        /// <summary>
        /// The total size of the file in bytes.
        /// </summary>
        public long TotalSize { get; set; }
        
        /// <summary>
        /// The number of bytes that have been transferred so far.
        /// </summary>
        public long BytesTransferred { get; set; }
        
        /// <summary>
        /// The hash of the file being transferred.
        /// </summary>
        public string Hash { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether compression is being used for the transfer.
        /// </summary>
        public bool UseCompression { get; set; }
        
        /// <summary>
        /// The compression algorithm being used for the transfer.
        /// </summary>
        public CompressionHelper.CompressionAlgorithm CompressionAlgorithm { get; set; }
        
        /// <summary>
        /// Whether encryption is being used for the transfer.
        /// </summary>
        public bool UseEncryption { get; set; }
        
        /// <summary>
        /// The hostname or IP address of the server.
        /// </summary>
        public string Host { get; set; } = string.Empty;
        
        /// <summary>
        /// The port number of the server.
        /// </summary>
        public int Port { get; set; }
        
        /// <summary>
        /// For directory transfers, the relative path of the file within the directory.
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;
        
        /// <summary>
        /// For directory transfers, the name of the directory being transferred.
        /// </summary>
        public string DirectoryName { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether this file is part of a multi-file transfer.
        /// </summary>
        public bool IsMultiFile { get; set; }
        
        /// <summary>
        /// The timestamp when this resume info was last updated.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    static TransferResumeManager()
    {
        // Create the resume directory if it doesn't exist
        Directory.CreateDirectory(ResumeDirectory);
    }

    /// <summary>
    /// Creates a resume file for a file transfer.
    /// </summary>
    /// <param name="info">The resume information.</param>
    public static void CreateResumeFile(ResumeInfo info)
    {
        info.Timestamp = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(info, _jsonOptions);
        File.WriteAllText(GetResumeFilePath(info.FilePath), json);
    }

    /// <summary>
    /// Updates an existing resume file with new information.
    /// </summary>
    /// <param name="info">The updated resume information.</param>
    public static void UpdateResumeFile(ResumeInfo info)
    {
        info.Timestamp = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(info, _jsonOptions);
        File.WriteAllText(GetResumeFilePath(info.FilePath), json);
    }

    /// <summary>
    /// Loads resume information for a file transfer.
    /// </summary>
    /// <param name="filePath">The path to the file being transferred.</param>
    /// <returns>The resume information, or null if no resume file exists.</returns>
    public static ResumeInfo? LoadResumeInfo(string filePath)
    {
        var resumeFilePath = GetResumeFilePath(filePath);
        if (!File.Exists(resumeFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(resumeFilePath);
            return JsonSerializer.Deserialize<ResumeInfo>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading resume file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Deletes the resume file for a file transfer.
    /// </summary>
    /// <param name="filePath">The path to the file being transferred.</param>
    public static void DeleteResumeFile(string filePath)
    {
        var resumeFilePath = GetResumeFilePath(filePath);
        if (File.Exists(resumeFilePath))
        {
            File.Delete(resumeFilePath);
        }
    }

    /// <summary>
    /// Gets all resume files that exist.
    /// </summary>
    /// <returns>A list of resume information objects.</returns>
    public static List<ResumeInfo> GetAllResumeFiles()
    {
        var result = new List<ResumeInfo>();
        
        foreach (var file in Directory.GetFiles(ResumeDirectory, "*.resume"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var info = JsonSerializer.Deserialize<ResumeInfo>(json);
                if (info != null)
                {
                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading resume file {file}: {ex.Message}");
            }
        }
        
        // Sort by timestamp, newest first
        result.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
        
        return result;
    }

    /// <summary>
    /// Gets the path to the resume file for a file transfer.
    /// </summary>
    /// <param name="filePath">The path to the file being transferred.</param>
    /// <returns>The path to the resume file.</returns>
    private static string GetResumeFilePath(string filePath)
    {
        // Create a unique filename based on the file path
        var hash = Convert.ToBase64String(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(filePath)))
            .Replace("/", "_")
            .Replace("+", "-")
            .Replace("=", "");
            
        return Path.Combine(ResumeDirectory, $"{hash}.resume");
    }
}
