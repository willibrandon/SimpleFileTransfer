using System.Threading.Tasks;

namespace SimpleFileTransfer.Services;

/// <summary>
/// Defines the contract for a service that handles file transfer operations.
/// </summary>
/// <remarks>
/// This interface abstracts the file transfer functionality, allowing for different
/// implementations and easier testing. It provides methods for transferring files
/// with various options like compression and encryption.
/// </remarks>
public interface IFileTransferService
{
    /// <summary>
    /// Transfers a file asynchronously according to the specified options.
    /// </summary>
    /// <param name="options">The options specifying the source, destination, and transfer settings.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TransferFileAsync(FileTransferOptions options);
}
