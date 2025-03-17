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
    /// Gets the list of received files.
    /// </summary>
    /// <returns>The list of received files.</returns>
    public static List<ReceivedFileInfo> GetReceivedFiles()
    {
        // Filter out any invalid entries
        return _receivedFiles
            .Where(f => !string.IsNullOrEmpty(f.FileName) && f.Size > 0 && !string.IsNullOrEmpty(f.Id))
            .ToList();
    }

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
        // If the server is already running, return success
        if (_server != null && _serverCts != null && !_serverCts.IsCancellationRequested)
        {
            return Ok(new { isRunning = true, port = _config.Port });
        }
        
        // Update the configuration if provided
        if (config != null)
        {
            _config = config;
        }
        
        // Ensure downloads directory is not empty
        if (string.IsNullOrWhiteSpace(_config.DownloadsDirectory))
        {
            _config.DownloadsDirectory = Program.DownloadsDirectory;
        }
        
        Console.WriteLine($"Starting server with downloads directory: {_config.DownloadsDirectory}");
        
        // Create a new cancellation token source
        _serverCts = new CancellationTokenSource();
        
        try
        {
            // Create the downloads directory if it doesn't exist
            if (!Directory.Exists(_config.DownloadsDirectory))
            {
                try
                {
                    Directory.CreateDirectory(_config.DownloadsDirectory);
                    Console.WriteLine($"Created downloads directory: {_config.DownloadsDirectory}");
                }
                catch (Exception dirEx)
                {
                    Console.WriteLine($"Error creating downloads directory: {dirEx.Message}");
                    throw new Exception($"Could not create downloads directory: {dirEx.Message}", dirEx);
                }
            }
            
            // Scan the downloads directory for existing files
            ScanDownloadsDirectory();
            
            // Create and start the server
            try
            {
                _server = new FileTransferServer(
                    _config.DownloadsDirectory,
                    _config.Port,
                    _config.UseEncryption ? _config.Password : null,
                    _serverCts.Token);
                
                Console.WriteLine("FileTransferServer instance created successfully");
                
                // Subscribe to the FileReceived event
                _server.FileReceived += OnFileReceived;
                
                // Start the server
                _server.Start();
                Console.WriteLine($"Server started on port {_config.Port}");
                
                // Initialize the WebSocket server
                WebSocketServer.Initialize(_server);
                Console.WriteLine("WebSocketServer initialized");
                
                // Broadcast the server status
                await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
                {
                    Type = "server_started",
                    Data = new { port = _config.Port }
                });
                
                // Broadcast the current list of received files
                await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
                {
                    Type = "received_files",
                    Data = GetReceivedFiles()
                });
                
                return Ok(new { isRunning = true, port = _config.Port });
            }
            catch (Exception serverEx)
            {
                Console.WriteLine($"Error creating or starting server: {serverEx.Message}");
                Console.WriteLine($"Stack trace: {serverEx.StackTrace}");
                throw new Exception($"Error starting server: {serverEx.Message}", serverEx);
            }
        }
        catch (Exception ex)
        {
            // Clean up if an error occurs
            _server?.Stop();
            _server = null;
            _serverCts?.Cancel();
            _serverCts = null;
            
            Console.WriteLine($"Server start failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
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
        // Filter out any invalid entries
        var validFiles = _receivedFiles
            .Where(f => !string.IsNullOrEmpty(f.FileName) && f.Size > 0 && !string.IsNullOrEmpty(f.Id))
            .ToList();
            
        return Ok(new { files = validFiles });
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

    /// <summary>
    /// Opens a folder in the system's file explorer.
    /// </summary>
    /// <param name="request">The request containing the folder path.</param>
    /// <returns>The result of the operation.</returns>
    [HttpPost("open-folder")]
    public IActionResult OpenFolder([FromBody] OpenFolderRequest request)
    {
        if (string.IsNullOrEmpty(request.Path))
        {
            return BadRequest(new { error = "Path is required" });
        }
        
        if (!Directory.Exists(request.Path))
        {
            return NotFound(new { error = $"Directory not found: {request.Path}" });
        }
        
        try
        {
            // Open the folder using the system's default file explorer
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = request.Path,
                UseShellExecute = true
            };
            
            System.Diagnostics.Process.Start(startInfo);
            
            return Ok(new { message = $"Folder opened: {request.Path}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to open folder: {ex.Message}" });
        }
    }

    /// <summary>
    /// Scans the downloads directory for existing files and adds them to the received files list.
    /// </summary>
    private void ScanDownloadsDirectory()
    {
        try
        {
            // Clear existing files list to avoid duplicates
            _receivedFiles.Clear();
            
            if (!Directory.Exists(_config.DownloadsDirectory))
            {
                Console.WriteLine($"Downloads directory does not exist: {_config.DownloadsDirectory}");
                return;
            }
            
            Console.WriteLine($"Scanning downloads directory: {_config.DownloadsDirectory}");
            
            // Get all files in the downloads directory
            var files = Directory.GetFiles(_config.DownloadsDirectory, "*", SearchOption.AllDirectories);
            Console.WriteLine($"Found {files.Length} files in downloads directory");
            
            foreach (var filePath in files)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists || fileInfo.Length == 0)
                    {
                        Console.WriteLine($"Skipping file {filePath}: File does not exist or is empty");
                        continue;
                    }
                    
                    // Add the file to the received files list
                    var receivedFile = new ReceivedFileInfo
                    {
                        Id = Guid.NewGuid().ToString(),
                        FileName = Path.GetFileName(filePath),
                        FilePath = filePath,
                        Directory = Path.GetDirectoryName(filePath) ?? string.Empty,
                        Size = fileInfo.Length,
                        ReceivedDate = fileInfo.CreationTime,
                        Sender = "Unknown (Existing File)"
                    };
                    
                    _receivedFiles.Add(receivedFile);
                    Console.WriteLine($"Added existing file to list: {receivedFile.FileName}, Size: {receivedFile.Size} bytes");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing existing file {filePath}: {ex.Message}");
                }
            }
            
            Console.WriteLine($"Added {_receivedFiles.Count} existing files to the received files list");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning downloads directory: {ex.Message}");
        }
    }

    private static async void OnFileReceived(object? sender, FileReceivedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(e.FilePath) || !System.IO.File.Exists(e.FilePath))
            {
                Console.WriteLine($"Error processing received file: File path is invalid or file does not exist: {e.FilePath}");
                return;
            }
            
            var fileInfo = new FileInfo(e.FilePath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                Console.WriteLine($"Error processing received file: File is empty or does not exist: {e.FilePath}");
                return;
            }
            
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
            
            // Check if this is a duplicate
            var existingFile = _receivedFiles.FirstOrDefault(f => 
                f.FilePath == receivedFile.FilePath && 
                f.Size == receivedFile.Size);
                
            if (existingFile != null)
            {
                Console.WriteLine($"Duplicate file detected, not adding to received files list: {receivedFile.FileName}");
                return;
            }
            
            _receivedFiles.Add(receivedFile);
            
            Console.WriteLine($"File received: {receivedFile.FileName}, Size: {receivedFile.Size} bytes, From: {receivedFile.Sender}");
            
            // Broadcast file received event with complete file data
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = "file_received",
                Data = new
                {
                    id = receivedFile.Id,
                    fileName = receivedFile.FileName,
                    filePath = receivedFile.FilePath,
                    directory = receivedFile.Directory,
                    size = receivedFile.Size,
                    receivedDate = receivedFile.ReceivedDate,
                    sender = receivedFile.Sender
                }
            });
            
            // Also broadcast the updated list of files
            var validFiles = _receivedFiles
                .Where(f => !string.IsNullOrEmpty(f.FileName) && f.Size > 0 && !string.IsNullOrEmpty(f.Id))
                .ToList();
                
            await WebSocketServer.BroadcastEventAsync(new WebSocketEvent
            {
                Type = "received_files",
                Data = validFiles
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

/// <summary>
/// Represents a request to open a folder.
/// </summary>
public class OpenFolderRequest
{
    /// <summary>
    /// Gets or sets the path to the folder to open.
    /// </summary>
    public string Path { get; set; } = string.Empty;
} 