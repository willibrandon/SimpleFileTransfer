using System;

namespace SimpleFileTransfer.Queue;

/// <summary>
/// Provides data for the <see cref="TransferQueue.TransferCompleted"/> event.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TransferCompletedEventArgs"/> class.
/// </remarks>
/// <param name="transfer">The transfer that was completed.</param>
/// <param name="success">Whether the transfer was successful.</param>
/// <param name="exception">The exception that occurred during the transfer, if any.</param>
public class TransferCompletedEventArgs(QueuedTransfer transfer, bool success, Exception? exception) : EventArgs
{
    /// <summary>
    /// Gets the transfer that was completed.
    /// </summary>
    public QueuedTransfer Transfer { get; } = transfer;

    /// <summary>
    /// Gets a value indicating whether the transfer was successful.
    /// </summary>
    public bool Success { get; } = success;

    /// <summary>
    /// Gets the exception that occurred during the transfer, if any.
    /// </summary>
    public Exception? Exception { get; } = exception;
}
