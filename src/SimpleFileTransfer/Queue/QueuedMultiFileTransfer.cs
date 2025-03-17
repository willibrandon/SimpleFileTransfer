using SimpleFileTransfer.Helpers;
using SimpleFileTransfer.Transfer;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleFileTransfer.Queue;

/// <summary>
/// Represents a multi-file transfer that has been queued for execution.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="QueuedMultiFileTransfer"/> class.
/// </remarks>
/// <param name="host">The hostname or IP address of the server to connect to.</param>
/// <param name="filePaths">The paths to the files to send.</param>
/// <param name="useCompression">Whether to use compression for the transfer.</param>
/// <param name="compressionAlgorithm">The compression algorithm to use.</param>
/// <param name="useEncryption">Whether to use encryption for the transfer.</param>
/// <param name="password">The password to use for encryption.</param>
/// <param name="resumeEnabled">Whether to enable resume capability for the transfer.</param>
/// <param name="port">The port number to connect to. Defaults to <see cref="Program.Port"/>.</param>
public class QueuedMultiFileTransfer(
    string host,
    List<string> filePaths,
    bool useCompression = false,
    CompressionHelper.CompressionAlgorithm compressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip,
    bool useEncryption = false,
    string? password = null,
    bool resumeEnabled = false,
    int port = Program.Port) : QueuedTransfer
{

    /// <inheritdoc/>
    public override string Description => $"Multiple files ({filePaths.Count}) to {host}";
    
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
                resumeEnabled);
            
            client.SendMultipleFiles(filePaths);
        }, cancellationToken);
    }
}
