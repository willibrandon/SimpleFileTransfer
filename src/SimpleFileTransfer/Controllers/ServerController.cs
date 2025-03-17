using Microsoft.AspNetCore.Mvc;
using SimpleFileTransfer.Transfer;
using SimpleFileTransfer.WebSockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleFileTransfer.Controllers;

/// <summary>
/// API controller for managing the file transfer server.
/// </summary>
[ApiController]
[Route("api/server")]
public class ServerController : ControllerBase
{
    private static FileTransferServer? _server;
    private static CancellationTokenSource? _serverCts;
    private static ServerConfig _config = new()
    {
        Port = Program.Port,
        DownloadsDirectory = Program.DownloadsDirectory,
        UseEncryption = false,
        Password = null
    };
    private static readonly List<ReceivedFileInfo> _receivedFiles = new();

    /// <summary>
    /// Gets the current status of the file transfer server.
    /// </summary>
    /// <returns>The server status.</returns>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            IsRunning = _server != null && _serverCts != null && !_serverCts.IsCancellationRequested
        });
    }

    /// <summary>
    /// Starts the file transfer server with the specified configuration.
    /// </summary>
    /// <param name="config">Optional server configuration. If not provided, the current configuration will be used.</param>
    /// <returns>The result of the operation.</returns>
    [HttpPost("start")]
    public async Task<IActionResult> StartServer([FromBody] ServerConfig? config = null)
    {
        if (_server != null && _serverCts != null && !_serverCts.IsCancellationRequested)
        {
            return BadRequest(new { error = "Server is already running" });
        }

        try
        {
            // Update config if provided
            if (config != null)
            {
                _config = config;
            }

            // Ensure downloads directory is not empty
            if (string.IsNullOrWhiteSpace(_config.DownloadsDirectory))
            {
                _config.DownloadsDirectory = Program.DownloadsDirectory;
            }

            // Create downloads directory
            Directory.CreateDirectory(_config.DownloadsDirectory);

            // Start the server
            _serverCts = new CancellationTokenSource();
            _server = new FileTransferServer(
                _config.DownloadsDirectory,
                _config.Port,
                _config.UseEncryption ? _config.Password : null,
                _serverCts.Token);

            // Subscribe to file received event
            _server.FileReceived += OnFileReceived;
            
            // Initialize WebSocket server
            WebSocketServer.Initialize(_server);

            // Start the server in a background task
            await Task.Run(() =>
            {
                try
                {
                    _server.Start();
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Server error: {ex.Message}");
                }
            });
            
            // Broadcast server started event
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = "server_started",
                Data = new
                {
                    Port = _config.Port,
                    DownloadsDirectory = _config.DownloadsDirectory,
                    UseEncryption = _config.UseEncryption,
                    StartedAt = DateTime.Now
                }
            });

            return Ok(new
            {
                message = $"Server started on port {_config.Port}",
                config = _config
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Stops the file transfer server.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    [HttpPost("stop")]
    public async Task<IActionResult> StopServer()
    {
        if (_server == null || _serverCts == null || _serverCts.IsCancellationRequested)
        {
            return BadRequest(new { error = "Server is not running" });
        }

        try
        {
            // Unsubscribe from events
            if (_server != null)
            {
                _server.FileReceived -= OnFileReceived;
            }

            // Cancel the server
            _serverCts.Cancel();
            _server = null;
            _serverCts = null;
            
            // Broadcast server stopped event
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = "server_stopped",
                Data = new
                {
                    StoppedAt = DateTime.Now
                }
            });

            return Ok(new { message = "Server stopped" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets the current server configuration.
    /// </summary>
    /// <returns>The server configuration.</returns>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return Ok(_config);
    }

    /// <summary>
    /// Updates the server configuration.
    /// </summary>
    /// <param name="config">The new server configuration.</param>
    /// <returns>The result of the operation.</returns>
    [HttpPost("config")]
    public async Task<IActionResult> UpdateConfig([FromBody] ServerConfig config)
    {
        if (_server != null && _serverCts != null && !_serverCts.IsCancellationRequested)
        {
            return BadRequest(new { error = "Cannot update configuration while server is running" });
        }

        try
        {
            _config = config;
            
            // Broadcast config updated event
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = "server_config_updated",
                Data = _config
            });
            
            return Ok(new { message = "Configuration updated", config });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets the list of received files.
    /// </summary>
    /// <returns>The list of received files.</returns>
    [HttpGet("files")]
    public IActionResult GetFiles()
    {
        return Ok(new { files = _receivedFiles });
    }

    /// <summary>
    /// Downloads a file by its ID.
    /// </summary>
    /// <param name="id">The ID of the file to download.</param>
    /// <returns>The file as a download.</returns>
    [HttpGet("files/{id}/download")]
    public IActionResult DownloadFile(string id)
    {
        var file = _receivedFiles.FirstOrDefault(f => f.Id == id);
        
        if (file == null)
        {
            return NotFound(new { error = $"File with ID {id} not found" });
        }
        
        if (!System.IO.File.Exists(file.FilePath))
        {
            return NotFound(new { error = $"File {file.FileName} no longer exists on disk" });
        }
        
        return PhysicalFile(file.FilePath, "application/octet-stream", file.FileName);
    }

    private static async void OnFileReceived(object? sender, FileReceivedEventArgs e)
    {
        try
        {
            var fileInfo = new FileInfo(e.FilePath);
            
            var receivedFile = new ReceivedFileInfo
            {
                Id = Guid.NewGuid().ToString(),
                FileName = Path.GetFileName(e.FilePath),
                FilePath = e.FilePath,
                Directory = Path.GetDirectoryName(e.FilePath) ?? string.Empty,
                Size = fileInfo.Length,
                ReceivedDate = DateTime.Now,
                Sender = e.SenderIp ?? "Unknown"
            };
            
            _receivedFiles.Add(receivedFile);
            
            // Broadcast file received event
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = "file_received",
                Data = receivedFile
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing received file: {ex.Message}");
        }
    }
}

/// <summary>
/// Represents the configuration for the file transfer server.
/// </summary>
public class ServerConfig
{
    /// <summary>
    /// Gets or sets the port number to listen on.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets the directory where received files will be saved.
    /// </summary>
    public string DownloadsDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether encryption is required.
    /// </summary>
    public bool UseEncryption { get; set; }

    /// <summary>
    /// Gets or sets the password for encryption.
    /// </summary>
    public string? Password { get; set; }
}

/// <summary>
/// Represents information about a received file.
/// </summary>
public class ReceivedFileInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for the file.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the file.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full path to the file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the directory containing the file.
    /// </summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the file was received.
    /// </summary>
    public DateTime ReceivedDate { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the sender.
    /// </summary>
    public string Sender { get; set; } = string.Empty;
} 