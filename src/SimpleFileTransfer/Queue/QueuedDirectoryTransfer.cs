using SimpleFileTransfer.Helpers;
using SimpleFileTransfer.Transfer;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleFileTransfer.Queue;

/// <summary>
/// Represents a directory transfer that has been queued for execution.
/// </summary>
public class QueuedDirectoryTransfer : QueuedTransfer
{
    private readonly string _host;
    private readonly string _dirPath;
    private readonly bool _useCompression;
    private readonly CompressionHelper.CompressionAlgorithm _compressionAlgorithm;
    private readonly bool _useEncryption;
    private readonly string? _password;
    private readonly bool _resumeEnabled;
    private readonly int _port;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedDirectoryTransfer"/> class.
    /// </summary>
    /// <param name="host">The hostname or IP address of the server to connect to.</param>
    /// <param name="dirPath">The path to the directory to send.</param>
    /// <param name="useCompression">Whether to use compression for the transfer.</param>
    /// <param name="compressionAlgorithm">The compression algorithm to use.</param>
    /// <param name="useEncryption">Whether to use encryption for the transfer.</param>
    /// <param name="password">The password to use for encryption.</param>
    /// <param name="resumeEnabled">Whether to enable resume capability for the transfer.</param>
    /// <param name="port">The port number to connect to. Defaults to <see cref="Program.Port"/>.</param>
    public QueuedDirectoryTransfer(
        string host,
        string dirPath,
        bool useCompression = false,
        CompressionHelper.CompressionAlgorithm compressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip,
        bool useEncryption = false,
        string? password = null,
        bool resumeEnabled = false,
        int port = Program.Port)
    {
        _host = host;
        _dirPath = dirPath;
        _useCompression = useCompression;
        _compressionAlgorithm = compressionAlgorithm;
        _useEncryption = useEncryption;
        _password = password;
        _resumeEnabled = resumeEnabled;
        _port = port;
    }
    
    /// <inheritdoc/>
    public override string Description => $"Directory: {Path.GetFileName(_dirPath)} to {_host}";
    
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
            
            client.SendDirectory(_dirPath);
        }, cancellationToken);
    }
}
