using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SimpleFileTransfer.Helpers;
using SimpleFileTransfer.Queue;
using SimpleFileTransfer.Transfer;
using SimpleFileTransfer.WebSockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SimpleFileTransfer.Controllers;

/// <summary>
/// API controller for managing client-side file transfers.
/// </summary>
[ApiController]
[Route("api/client")]
public class ClientController : ControllerBase
{
    private static readonly TransferQueue _queue = new();
    private static readonly List<TransferHistoryItem> _history = new();
    
    static ClientController()
    {
        // Subscribe to queue events
        _queue.TransferCompleted += OnTransferCompleted;
        _queue.AllTransfersCompleted += OnAllTransfersCompleted;
    }
    
    /// <summary>
    /// Sends a file to a server.
    /// </summary>
    /// <param name="request">The transfer request.</param>
    /// <returns>The result of the operation.</returns>
    [HttpPost("send")]
    public async Task<IActionResult> SendFile([FromForm] ClientTransferRequest request)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { error = "No file was uploaded" });
        }
        
        try
        {
            // Create a temporary file
            var tempFile = Path.GetTempFileName();
            
            // Save the uploaded file to the temporary location
            using (var stream = new FileStream(tempFile, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }
            
            // Create a client
            var client = new FileTransferClient(
                request.Host,
                request.Port,
                request.UseCompression,
                request.CompressionAlgorithm,
                request.UseEncryption,
                request.Password,
                request.ResumeEnabled);
            
            // Send the file
            var fileName = request.FileName ?? request.File.FileName;
            
            // Add to history
            var historyItem = new TransferHistoryItem
            {
                Id = Guid.NewGuid().ToString(),
                FileName = fileName,
                Host = request.Host,
                Port = request.Port,
                Size = request.File.Length,
                StartTime = DateTime.Now,
                Status = TransferStatus.InProgress,
                UseCompression = request.UseCompression,
                UseEncryption = request.UseEncryption
            };
            
            _history.Add(historyItem);
            
            // Broadcast the new history item
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = "transfer_started",
                Data = historyItem
            });
            
            // Send the file in a background task
            _ = Task.Run(async () =>
            {
                try
                {
                    client.SendFile(tempFile);
                    
                    // Update history
                    historyItem.EndTime = DateTime.Now;
                    historyItem.Status = TransferStatus.Completed;
                    
                    // Broadcast the updated history item
                    await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
                    {
                        Type = "transfer_completed",
                        Data = historyItem
                    });
                }
                catch (Exception ex)
                {
                    // Update history
                    historyItem.EndTime = DateTime.Now;
                    historyItem.Status = TransferStatus.Failed;
                    historyItem.Error = ex.Message;
                    
                    // Broadcast the updated history item
                    await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
                    {
                        Type = "transfer_failed",
                        Data = historyItem
                    });
                }
                finally
                {
                    // Clean up the temporary file
                    try
                    {
                        if (System.IO.File.Exists(tempFile))
                        {
                            System.IO.File.Delete(tempFile);
                        }
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }
                }
            });
            
            return Ok(new
            {
                message = "File transfer started",
                transferId = historyItem.Id
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    /// <summary>
    /// Adds a transfer to the queue.
    /// </summary>
    /// <param name="request">The transfer request.</param>
    /// <returns>The result of the operation.</returns>
    [HttpPost("queue")]
    public async Task<IActionResult> QueueTransfer([FromForm] ClientTransferRequest request)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { error = "No file was uploaded" });
        }
        
        try
        {
            // Create a temporary file
            var tempFile = Path.GetTempFileName();
            
            // Save the uploaded file to the temporary location
            using (var stream = new FileStream(tempFile, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }
            
            // Create a queued transfer
            var fileName = request.FileName ?? request.File.FileName;
            var transfer = new QueuedFileTransfer(
                request.Host,
                tempFile,
                request.UseCompression,
                request.CompressionAlgorithm,
                request.UseEncryption,
                request.Password,
                request.ResumeEnabled);
            
            // Add to queue
            _queue.Enqueue(transfer);
            
            // Add to history
            var historyItem = new TransferHistoryItem
            {
                Id = Guid.NewGuid().ToString(),
                FileName = fileName,
                Host = request.Host,
                Port = request.Port,
                Size = request.File.Length,
                StartTime = DateTime.Now,
                Status = TransferStatus.Queued,
                UseCompression = request.UseCompression,
                UseEncryption = request.UseEncryption
            };
            
            _history.Add(historyItem);
            
            // Associate the history item with the transfer
            transfer.UserData = historyItem;
            
            // Broadcast the new queue item
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = "transfer_queued",
                Data = new
                {
                    Id = historyItem.Id,
                    FileName = fileName,
                    Host = request.Host,
                    Port = request.Port,
                    Size = request.File.Length,
                    QueuedAt = DateTime.Now
                }
            });
            
            return Ok(new
            {
                message = "File added to queue",
                queueId = historyItem.Id
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    /// <summary>
    /// Gets the current queue status.
    /// </summary>
    /// <returns>The queue status.</returns>
    [HttpGet("queue")]
    public IActionResult GetQueueStatus()
    {
        return Ok(new
        {
            isProcessing = _queue.IsProcessing,
            count = _queue.Count
        });
    }
    
    /// <summary>
    /// Starts processing the queue.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    [HttpPost("queue/start")]
    public async Task<IActionResult> StartQueue()
    {
        if (_queue.IsProcessing)
        {
            return BadRequest(new { error = "Queue is already processing" });
        }
        
        try
        {
            _queue.Start();
            
            // Broadcast queue started event
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = "queue_started",
                Data = new
                {
                    StartedAt = DateTime.Now
                }
            });
            
            return Ok(new { message = "Queue processing started" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    /// <summary>
    /// Stops processing the queue.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    [HttpPost("queue/stop")]
    public async Task<IActionResult> StopQueue()
    {
        if (!_queue.IsProcessing)
        {
            return BadRequest(new { error = "Queue is not processing" });
        }
        
        try
        {
            _queue.Stop();
            
            // Broadcast queue stopped event
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = "queue_stopped",
                Data = new
                {
                    StoppedAt = DateTime.Now
                }
            });
            
            return Ok(new { message = "Queue processing stopped" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    /// <summary>
    /// Clears the queue.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    [HttpPost("queue/clear")]
    public async Task<IActionResult> ClearQueue()
    {
        try
        {
            _queue.Clear();
            
            // Update history items
            foreach (var item in _history.Where(h => h.Status == TransferStatus.Queued))
            {
                item.Status = TransferStatus.Cancelled;
                item.EndTime = DateTime.Now;
            }
            
            // Broadcast queue cleared event
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = "queue_cleared",
                Data = new
                {
                    ClearedAt = DateTime.Now
                }
            });
            
            return Ok(new { message = "Queue cleared" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    /// <summary>
    /// Gets the transfer history.
    /// </summary>
    /// <returns>The transfer history.</returns>
    [HttpGet("history")]
    public IActionResult GetHistory()
    {
        return Ok(new { history = _history });
    }
    
    private static async void OnTransferCompleted(object? sender, TransferCompletedEventArgs e)
    {
        if (e.Transfer.UserData is TransferHistoryItem historyItem)
        {
            // Update history
            historyItem.EndTime = DateTime.Now;
            historyItem.Status = e.Success ? TransferStatus.Completed : TransferStatus.Failed;
            
            if (!e.Success && e.Exception != null)
            {
                historyItem.Error = e.Exception.Message;
            }
            
            // Broadcast the updated history item
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = e.Success ? "transfer_completed" : "transfer_failed",
                Data = historyItem
            });
        }
    }
    
    private static async void OnAllTransfersCompleted(object? sender, EventArgs e)
    {
        // Broadcast all transfers completed event
        await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
        {
            Type = "all_transfers_completed",
            Data = new
            {
                CompletedAt = DateTime.Now
            }
        });
    }
}

/// <summary>
/// Represents a client transfer request.
/// </summary>
public class ClientTransferRequest
{
    /// <summary>
    /// Gets or sets the file to transfer.
    /// </summary>
    public IFormFile? File { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the file.
    /// </summary>
    public string? FileName { get; set; }
    
    /// <summary>
    /// Gets or sets the hostname or IP address of the server.
    /// </summary>
    public string Host { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the port number of the server.
    /// </summary>
    public int Port { get; set; } = Program.Port;
    
    /// <summary>
    /// Gets or sets a value indicating whether to use compression.
    /// </summary>
    public bool UseCompression { get; set; }
    
    /// <summary>
    /// Gets or sets the compression algorithm to use.
    /// </summary>
    public CompressionHelper.CompressionAlgorithm CompressionAlgorithm { get; set; } = CompressionHelper.CompressionAlgorithm.GZip;
    
    /// <summary>
    /// Gets or sets a value indicating whether to use encryption.
    /// </summary>
    public bool UseEncryption { get; set; }
    
    /// <summary>
    /// Gets or sets the password for encryption.
    /// </summary>
    public string? Password { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether to enable resume capability.
    /// </summary>
    public bool ResumeEnabled { get; set; }
}

/// <summary>
/// Represents the status of a file transfer.
/// </summary>
public enum TransferStatus
{
    /// <summary>
    /// The transfer is queued.
    /// </summary>
    Queued,
    
    /// <summary>
    /// The transfer is in progress.
    /// </summary>
    InProgress,
    
    /// <summary>
    /// The transfer completed successfully.
    /// </summary>
    Completed,
    
    /// <summary>
    /// The transfer failed.
    /// </summary>
    Failed,
    
    /// <summary>
    /// The transfer was cancelled.
    /// </summary>
    Cancelled
}

/// <summary>
/// Represents a transfer history item.
/// </summary>
public class TransferHistoryItem
{
    /// <summary>
    /// Gets or sets the unique identifier for the transfer.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the name of the file.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the hostname or IP address of the server.
    /// </summary>
    public string Host { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the port number of the server.
    /// </summary>
    public int Port { get; set; }
    
    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long Size { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the transfer started.
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the transfer ended.
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Gets or sets the status of the transfer.
    /// </summary>
    public TransferStatus Status { get; set; }
    
    /// <summary>
    /// Gets or sets the error message if the transfer failed.
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether compression was used.
    /// </summary>
    public bool UseCompression { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether encryption was used.
    /// </summary>
    public bool UseEncryption { get; set; }
} 