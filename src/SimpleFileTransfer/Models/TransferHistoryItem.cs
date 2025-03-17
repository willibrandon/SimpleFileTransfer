using System;

namespace SimpleFileTransfer.Models;

/// <summary>
/// Represents a file transfer history item.
/// </summary>
public class TransferHistoryItem
{
    /// <summary>
    /// Gets or sets the unique identifier for the transfer.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the name of the file being transferred.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the hostname or IP address of the server.
    /// </summary>
    public string Host { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the port number of the server.
    /// </summary>
    public int Port { get; set; }
    
    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long Size { get; set; }
    
    /// <summary>
    /// Gets or sets the time when the transfer started.
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Gets or sets the time when the transfer ended.
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Gets or sets the status of the transfer.
    /// </summary>
    public TransferStatus Status { get; set; }
    
    /// <summary>
    /// Gets or sets the error message if the transfer failed.
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether compression was used.
    /// </summary>
    public bool UseCompression { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether encryption was used.
    /// </summary>
    public bool UseEncryption { get; set; }
    
    /// <summary>
    /// Gets or sets the speed limit for the transfer in KB/s.
    /// </summary>
    public int? SpeedLimit { get; set; }
} 