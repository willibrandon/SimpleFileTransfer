using Microsoft.AspNetCore.Http;
using SimpleFileTransfer.Controllers;
using SimpleFileTransfer.Transfer;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleFileTransfer.WebSockets;

/// <summary>
/// Manages WebSocket connections and broadcasts events to connected clients.
/// </summary>
public class WebSocketServer
{
    private static readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private static readonly CancellationTokenSource _serverCts = new();
    private static FileTransferServer? _fileTransferServer;
    
    /// <summary>
    /// Initializes the WebSocket server and subscribes to FileTransferServer events.
    /// </summary>
    /// <param name="fileTransferServer">The file transfer server to monitor.</param>
    public static void Initialize(FileTransferServer fileTransferServer)
    {
        _fileTransferServer = fileTransferServer;
        _fileTransferServer.FileReceived += OnFileReceived;
    }
    
    /// <summary>
    /// Handles a new WebSocket connection.
    /// </summary>
    /// <param name="context">The HTTP context containing the WebSocket.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync();
        var clientId = Guid.NewGuid().ToString();
        
        _clients.TryAdd(clientId, socket);
        Console.WriteLine($"WebSocket client connected: {clientId}");
        
        // Send initial state
        await SendInitialStateAsync(socket);
        
        try
        {
            await ReceiveMessagesAsync(socket, clientId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket error: {ex.Message}");
        }
        finally
        {
            await CloseConnectionAsync(clientId);
        }
    }
    
    /// <summary>
    /// Receives messages from a WebSocket client.
    /// </summary>
    /// <param name="socket">The WebSocket to receive messages from.</param>
    /// <param name="clientId">The ID of the client.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task ReceiveMessagesAsync(WebSocket socket, string clientId)
    {
        var buffer = new byte[4096];
        
        while (socket.State == WebSocketState.Open && !_serverCts.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _serverCts.Token);
            
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client requested close", CancellationToken.None);
                break;
            }
            
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"Received message from client {clientId}: {message}");
                
                // Process client messages if needed
                // This could be used for client commands like starting/stopping the server
            }
        }
    }
    
    /// <summary>
    /// Closes a WebSocket connection and removes it from the clients dictionary.
    /// </summary>
    /// <param name="clientId">The ID of the client to close.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task CloseConnectionAsync(string clientId)
    {
        if (_clients.TryRemove(clientId, out var socket))
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closing connection", CancellationToken.None);
                }
                
                socket.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing WebSocket: {ex.Message}");
            }
            
            Console.WriteLine($"WebSocket client disconnected: {clientId}");
        }
    }
    
    /// <summary>
    /// Broadcasts an event to all connected WebSocket clients.
    /// </summary>
    /// <param name="eventData">The event data to broadcast.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task BroadcastEventAsync(WebSocketEvent eventData)
    {
        if (_clients.Count == 0)
        {
            return; // No clients connected, nothing to do
        }
        
        var deadSockets = new ConcurrentBag<string>();
        
        foreach (var client in _clients)
        {
            try
            {
                if (client.Value.State != WebSocketState.Open)
                {
                    deadSockets.Add(client.Key);
                    continue;
                }
                
                await SendEventAsync(client.Value, eventData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to client {client.Key}: {ex.Message}");
                deadSockets.Add(client.Key);
            }
        }
        
        // Clean up dead connections
        foreach (var clientId in deadSockets)
        {
            await CloseConnectionAsync(clientId);
        }
    }
    
    /// <summary>
    /// Sends an event to a specific WebSocket client.
    /// </summary>
    /// <param name="socket">The WebSocket to send the event to.</param>
    /// <param name="eventData">The event data to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task SendEventAsync(WebSocket socket, WebSocketEvent eventData)
    {
        try
        {
            var json = JsonSerializer.Serialize(eventData);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error serializing or sending event: {ex.Message}");
            throw; // Rethrow to be handled by the caller
        }
    }
    
    /// <summary>
    /// Stops the WebSocket server and closes all connections.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task StopAsync()
    {
        if (_fileTransferServer != null)
        {
            _fileTransferServer.FileReceived -= OnFileReceived;
            _fileTransferServer = null;
        }
        
        _serverCts.Cancel();
        
        foreach (var client in _clients)
        {
            await CloseConnectionAsync(client.Key);
        }
        
        _clients.Clear();
    }
    
    /// <summary>
    /// Handles the FileReceived event from the FileTransferServer.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
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
            
            var receivedFile = new
            {
                Id = Guid.NewGuid().ToString(),
                FileName = Path.GetFileName(e.FilePath),
                FilePath = e.FilePath,
                Directory = Path.GetDirectoryName(e.FilePath) ?? string.Empty,
                Size = fileInfo.Length,
                ReceivedDate = DateTime.Now,
                Sender = e.SenderIp ?? "Unknown"
            };
            
            Console.WriteLine($"File received: {receivedFile.FileName}, Size: {receivedFile.Size} bytes, From: {receivedFile.Sender}");
            
            // Broadcast file received event
            await BroadcastEventAsync(new WebSocketEvent
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

    public static async Task RemoveClientAsync(string clientId)
    {
        if (_clients.TryRemove(clientId, out var webSocket))
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing WebSocket: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Sends initial state to a newly connected client
    /// </summary>
    /// <param name="socket">The WebSocket to send the initial state to</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private static async Task SendInitialStateAsync(WebSocket socket)
    {
        // Send server status
        await SendEventAsync(socket, new WebSocketEvent
        {
            Type = "server_status",
            Data = new
            {
                IsRunning = FileTransferServer.IsRunning,
                Port = FileTransferServer.CurrentPort
            }
        });
        
        // Get the received files from the ServerController
        var receivedFiles = ServerController.GetReceivedFiles();
        
        // Send received files list
        await SendEventAsync(socket, new WebSocketEvent
        {
            Type = "received_files",
            Data = receivedFiles
        });
    }
} 