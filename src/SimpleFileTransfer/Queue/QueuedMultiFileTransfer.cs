using SimpleFileTransfer.Helpers;
using SimpleFileTransfer.Transfer;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleFileTransfer.Queue;

/// <summary>
/// Represents a multi-file transfer that has been queued for execution.
/// </summary>
public class QueuedMultiFileTransfer : QueuedTransfer
{
    private readonly string _host;
    private readonly List<string> _filePaths;
    private readonly bool _useCompression;
    private readonly CompressionHelper.CompressionAlgorithm _compressionAlgorithm;
    private readonly bool _useEncryption;
    private readonly string? _password;
    private readonly bool _resumeEnabled;
    private readonly int _port;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedMultiFileTransfer"/> class.
    /// </summary>
    /// <param name="host">The hostname or IP address of the server to connect to.</param>
    /// <param name="filePaths">The paths to the files to send.</param>
    /// <param name="useCompression">Whether to use compression for the transfer.</param>
    /// <param name="compressionAlgorithm">The compression algorithm to use.</param>
    /// <param name="useEncryption">Whether to use encryption for the transfer.</param>
    /// <param name="password">The password to use for encryption.</param>
    /// <param name="resumeEnabled">Whether to enable resume capability for the transfer.</param>
    /// <param name="port">The port number to connect to. Defaults to <see cref="Program.Port"/>.</param>
    public QueuedMultiFileTransfer(
        string host,
        List<string> filePaths,
        bool useCompression = false,
        CompressionHelper.CompressionAlgorithm compressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip,
        bool useEncryption = false,
        string? password = null,
        bool resumeEnabled = false,
        int port = Program.Port)
    {
        _host = host;
        _filePaths = filePaths;
        _useCompression = useCompression;
        _compressionAlgorithm = compressionAlgorithm;
        _useEncryption = useEncryption;
        _password = password;
        _resumeEnabled = resumeEnabled;
        _port = port;
    }
    
    /// <inheritdoc/>
    public override string Description => $"Multiple files ({_filePaths.Count}) to {_host}";
    
    /// <inheritdoc/>
    public override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var client = new FileTransferClient(
                _host,
                _port,
                _useCompression,
                _compressionAlgorithm,
                _useEncryption,
                _password,
                _resumeEnabled);
            
            client.SendMultipleFiles(_filePaths);
        }, cancellationToken);
    }
}
