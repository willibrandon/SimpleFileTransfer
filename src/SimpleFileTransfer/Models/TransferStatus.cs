using System;

namespace SimpleFileTransfer.Models;

/// <summary>
/// Represents the status of a file transfer.
/// </summary>
public enum TransferStatus
{
    /// <summary>
    /// The transfer is queued.
    /// </summary>
    Queued,
    
    /// <summary>
    /// The transfer is in progress.
    /// </summary>
    InProgress,
    
    /// <summary>
    /// The transfer completed successfully.
    /// </summary>
    Completed,
    
    /// <summary>
    /// The transfer failed.
    /// </summary>
    Failed,
    
    /// <summary>
    /// The transfer was cancelled.
    /// </summary>
    Cancelled
} 