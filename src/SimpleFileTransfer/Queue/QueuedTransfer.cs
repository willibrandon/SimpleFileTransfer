using System.Threading;
using System.Threading.Tasks;

namespace SimpleFileTransfer.Queue;

/// <summary>
/// Represents a transfer that has been queued for execution.
/// </summary>
public abstract class QueuedTransfer
{
    /// <summary>
    /// Gets a description of the transfer.
    /// </summary>
    public abstract string Description { get; }
    
    /// <summary>
    /// Gets or sets user-defined data associated with the transfer.
    /// </summary>
    public object? UserData { get; set; }
    
    /// <summary>
    /// Executes the transfer asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task ExecuteAsync(CancellationToken cancellationToken);
}
