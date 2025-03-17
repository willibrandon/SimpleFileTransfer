using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleFileTransfer.Queue;

/// <summary>
/// Manages a queue of file transfers to be executed sequentially.
/// </summary>
public class TransferQueue
{
    private readonly List<QueuedTransfer> _queue = [];
    private readonly Lock _queueLock = new();
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
