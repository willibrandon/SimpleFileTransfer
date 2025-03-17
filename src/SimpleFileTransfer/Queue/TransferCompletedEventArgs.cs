using System;

namespace SimpleFileTransfer.Queue;

/// <summary>
/// Provides data for the <see cref="TransferQueue.TransferCompleted"/> event.
/// </summary>
public class TransferCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the transfer that was completed.
    /// </summary>
    public QueuedTransfer Transfer { get; }
    
    /// <summary>
    /// Gets a value indicating whether the transfer was successful.
    /// </summary>
    public bool Success { get; }
    
    /// <summary>
    /// Gets the exception that occurred during the transfer, if any.
    /// </summary>
    public Exception? Exception { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="TransferCompletedEventArgs"/> class.
    /// </summary>
    /// <param name="transfer">The transfer that was completed.</param>
    /// <param name="success">Whether the transfer was successful.</param>
    /// <param name="exception">The exception that occurred during the transfer, if any.</param>
    public TransferCompletedEventArgs(QueuedTransfer transfer, bool success, Exception? exception)
    {
        Transfer = transfer;
        Success = success;
        Exception = exception;
    }
}
