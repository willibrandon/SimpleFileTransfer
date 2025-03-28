using Microsoft.AspNetCore.Mvc;
using SimpleFileTransfer.Models;
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
            // Get the original filename
            var originalFileName = request.FileName ?? request.File.FileName;
            
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
                request.ResumeEnabled,
                request.SpeedLimit);
            
            // Add to history
            var historyItem = new TransferHistoryItem
            {
                Id = Guid.NewGuid().ToString(),
                FileName = originalFileName,
                Host = request.Host,
                Port = request.Port,
                Size = request.File.Length,
                StartTime = DateTime.Now,
                Status = TransferStatus.InProgress,
                UseCompression = request.UseCompression,
                UseEncryption = request.UseEncryption,
                SpeedLimit = request.SpeedLimit
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
                    // Create a temporary file with the correct extension
                    var tempFileWithExt = Path.Combine(
                        Path.GetDirectoryName(tempFile) ?? string.Empty,
                        originalFileName);
                    
                    // If a file with this name already exists, delete it
                    if (System.IO.File.Exists(tempFileWithExt))
                    {
                        System.IO.File.Delete(tempFileWithExt);
                    }
                    
                    // Rename the temp file to have the correct filename
                    System.IO.File.Move(tempFile, tempFileWithExt);
                    
                    // Send the file with the correct filename
                    client.SendFile(tempFileWithExt);
                    
                    // Update history
                    historyItem.EndTime = DateTime.Now;
                    historyItem.Status = TransferStatus.Completed;
                    
                    // Broadcast the updated history item
                    await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
                    {
                        Type = "transfer_completed",
                        Data = historyItem
                    });
                    
                    // Clean up the temporary file
                    try
                    {
                        if (System.IO.File.Exists(tempFileWithExt))
                        {
                            System.IO.File.Delete(tempFileWithExt);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error cleaning up temporary file: {ex.Message}");
                    }
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
                        // Ignore cleanup errors
                    }
                }
            });
            
            return Ok(new { message = "File transfer initiated", id = historyItem.Id });
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
            // Get the original filename
            var originalFileName = request.FileName ?? request.File.FileName;
            
            // Create a temporary file
            var tempFile = Path.GetTempFileName();
            
            // Save the uploaded file to the temporary location
            using (var stream = new FileStream(tempFile, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }
            
            // Create a temporary file with the correct extension
            var tempFileWithExt = Path.Combine(
                Path.GetDirectoryName(tempFile) ?? string.Empty,
                originalFileName);
            
            // Rename the temporary file
            System.IO.File.Move(tempFile, tempFileWithExt, true);
            
            // Add to history
            var historyItem = new TransferHistoryItem
            {
                Id = Guid.NewGuid().ToString(),
                FileName = originalFileName,
                Host = request.Host,
                Port = request.Port,
                Size = request.File.Length,
                StartTime = DateTime.Now,
                Status = TransferStatus.Queued,
                UseCompression = request.UseCompression,
                UseEncryption = request.UseEncryption,
                SpeedLimit = request.SpeedLimit
            };
            
            _history.Add(historyItem);
            
            // Broadcast the new history item
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = "transfer_queued",
                Data = historyItem
            });
            
            // Add to queue
            var transfer = new QueuedFileTransfer(
                request.Host,
                tempFileWithExt,
                request.UseCompression,
                request.CompressionAlgorithm,
                request.UseEncryption,
                request.Password,
                request.ResumeEnabled,
                request.SpeedLimit);
            
            _queue.Enqueue(transfer);
            
            return Ok(new { message = "File added to queue", id = historyItem.Id });
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
        // Create a copy of the history list to avoid modification during serialization
        var historyItems = _history.Select(item => new
        {
            Id = item.Id,
            FileName = item.FileName,
            Host = item.Host,
            Port = item.Port,
            Size = item.Size,
            StartTime = item.StartTime,
            EndTime = item.EndTime,
            Status = item.Status.ToString(), // Convert enum to string for consistent serialization
            Error = item.Error,
            UseCompression = item.UseCompression,
            UseEncryption = item.UseEncryption
        }).ToList();

        return Ok(new { items = historyItems });
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