using SimpleFileTransfer.Helpers;
using SimpleFileTransfer.Transfer;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleFileTransfer.Queue;

/// <summary>
/// Represents a file transfer that has been queued for execution.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="QueuedFileTransfer"/> class.
/// </remarks>
/// <param name="host">The hostname or IP address of the server to connect to.</param>
/// <param name="filePath">The path to the file to send.</param>
/// <param name="useCompression">Whether to use compression for the transfer.</param>
/// <param name="compressionAlgorithm">The compression algorithm to use.</param>
/// <param name="useEncryption">Whether to use encryption for the transfer.</param>
/// <param name="password">The password to use for encryption.</param>
/// <param name="resumeEnabled">Whether to enable resume capability for the transfer.</param>
/// <param name="speedLimit">Optional speed limit in KB/s. Null means no limit.</param>
/// <param name="port">The port number to connect to. Defaults to <see cref="Program.Port"/>.</param>
public class QueuedFileTransfer(
    string host,
    string filePath,
    bool useCompression = false,
    CompressionHelper.CompressionAlgorithm compressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip,
    bool useEncryption = false,
    string? password = null,
    bool resumeEnabled = false,
    int? speedLimit = null,
    int port = Program.Port) : QueuedTransfer
{

    /// <inheritdoc/>
    public override string Description => $"File: {Path.GetFileName(filePath)} to {host}" + 
                                         (speedLimit.HasValue ? $" (Speed limit: {speedLimit} KB/s)" : "");
    
    /// <inheritdoc/>
    public override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var client = new FileTransferClient(
                host,
                port,
                useCompression,
                compressionAlgorithm,
                useEncryption,
                password,
                resumeEnabled,
                speedLimit);
            
            client.SendFile(filePath);
        }, cancellationToken);
    }
}
