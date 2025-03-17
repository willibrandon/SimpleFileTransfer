using SimpleFileTransfer.Helpers;
using System;

namespace SimpleFileTransfer.Queue;

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
