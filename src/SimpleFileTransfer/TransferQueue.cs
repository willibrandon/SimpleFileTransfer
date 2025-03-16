using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleFileTransfer;

/// <summary>
/// Manages a queue of file transfers to be executed sequentially.
/// </summary>
public class TransferQueue
{
    private readonly List<QueuedTransfer> _queue = [];
    private readonly object _queueLock = new();
    private bool _isProcessing;
    private CancellationTokenSource? _cancellationTokenSource;
    
    /// <summary>
    /// Gets a value indicating whether the queue is currently processing transfers.
    /// </summary>
    public bool IsProcessing => _isProcessing;
    
    /// <summary>
    /// Gets the number of transfers currently in the queue.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_queueLock)
            {
                return _queue.Count;
            }
        }
    }
    
    /// <summary>
    /// Event that is raised when a transfer is completed.
    /// </summary>
    public event EventHandler<TransferCompletedEventArgs>? TransferCompleted;
    
    /// <summary>
    /// Event that is raised when all transfers in the queue have been completed.
    /// </summary>
    public event EventHandler? AllTransfersCompleted;
    
    /// <summary>
    /// Adds a file transfer to the queue.
    /// </summary>
    /// <param name="transfer">The transfer to add to the queue.</param>
    public void Enqueue(QueuedTransfer transfer)
    {
        lock (_queueLock)
        {
            _queue.Add(transfer);
            Console.WriteLine($"Added to queue: {transfer.Description}");
        }
    }
    
    /// <summary>
    /// Starts processing the queue of transfers.
    /// </summary>
    public void Start()
    {
        if (_isProcessing)
        {
            Console.WriteLine("Queue is already processing.");
            return;
        }
        
        _isProcessing = true;
        _cancellationTokenSource = new CancellationTokenSource();
        
        Task.Run(() => ProcessQueue(_cancellationTokenSource.Token));
        
        Console.WriteLine("Queue processing started.");
    }
    
    /// <summary>
    /// Stops processing the queue of transfers.
    /// </summary>
    public void Stop()
    {
        if (!_isProcessing)
        {
            return;
        }
        
        _cancellationTokenSource?.Cancel();
        _isProcessing = false;
        
        Console.WriteLine("Queue processing stopped.");
    }
    
    /// <summary>
    /// Clears all transfers from the queue.
    /// </summary>
    public void Clear()
    {
        lock (_queueLock)
        {
            _queue.Clear();
            Console.WriteLine("Queue cleared.");
        }
    }
    
    /// <summary>
    /// Lists all transfers currently in the queue.
    /// </summary>
    public void ListTransfers()
    {
        lock (_queueLock)
        {
            if (_queue.Count == 0)
            {
                Console.WriteLine("Queue is empty.");
                return;
            }
            
            Console.WriteLine($"Queue contains {_queue.Count} transfers:");
            for (int i = 0; i < _queue.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {_queue[i].Description}");
            }
        }
    }
    
    private async Task ProcessQueue(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                QueuedTransfer? transfer = null;
                
                lock (_queueLock)
                {
                    if (_queue.Count == 0)
                    {
                        _isProcessing = false;
                        AllTransfersCompleted?.Invoke(this, EventArgs.Empty);
                        Console.WriteLine("All transfers completed.");
                        return;
                    }
                    
                    transfer = _queue[0];
                    _queue.RemoveAt(0);
                }
                
                if (cancellationToken.IsCancellationRequested)
                {
                    _isProcessing = false;
                    Console.WriteLine("Queue processing cancelled.");
                    return;
                }
                
                Console.WriteLine($"Processing transfer: {transfer.Description}");
                
                try
                {
                    await transfer.ExecuteAsync(cancellationToken);
                    TransferCompleted?.Invoke(this, new TransferCompletedEventArgs(transfer, true, null));
                    Console.WriteLine($"Transfer completed: {transfer.Description}");
                }
                catch (Exception ex)
                {
                    TransferCompleted?.Invoke(this, new TransferCompletedEventArgs(transfer, false, ex));
                    Console.WriteLine($"Transfer failed: {transfer.Description}");
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }
}

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
    /// Executes the transfer asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task ExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Represents a file transfer that has been queued for execution.
/// </summary>
public class QueuedFileTransfer : QueuedTransfer
{
    private readonly string _host;
    private readonly string _filePath;
    private readonly bool _useCompression;
    private readonly CompressionHelper.CompressionAlgorithm _compressionAlgorithm;
    private readonly bool _useEncryption;
    private readonly string? _password;
    private readonly bool _resumeEnabled;
    private readonly int _port;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedFileTransfer"/> class.
    /// </summary>
    /// <param name="host">The hostname or IP address of the server to connect to.</param>
    /// <param name="filePath">The path to the file to send.</param>
    /// <param name="useCompression">Whether to use compression for the transfer.</param>
    /// <param name="compressionAlgorithm">The compression algorithm to use.</param>
    /// <param name="useEncryption">Whether to use encryption for the transfer.</param>
    /// <param name="password">The password to use for encryption.</param>
    /// <param name="resumeEnabled">Whether to enable resume capability for the transfer.</param>
    /// <param name="port">The port number to connect to. Defaults to <see cref="Program.Port"/>.</param>
    public QueuedFileTransfer(
        string host,
        string filePath,
        bool useCompression = false,
        CompressionHelper.CompressionAlgorithm compressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip,
        bool useEncryption = false,
        string? password = null,
        bool resumeEnabled = false,
        int port = Program.Port)
    {
        _host = host;
        _filePath = filePath;
        _useCompression = useCompression;
        _compressionAlgorithm = compressionAlgorithm;
        _useEncryption = useEncryption;
        _password = password;
        _resumeEnabled = resumeEnabled;
        _port = port;
    }
    
    /// <inheritdoc/>
    public override string Description => $"File: {Path.GetFileName(_filePath)} to {_host}";
    
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
            
            client.SendFile(_filePath);
        }, cancellationToken);
    }
}

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
