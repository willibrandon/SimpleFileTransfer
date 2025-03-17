using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleFileTransfer.Queue;
using SimpleFileTransfer.WebSockets;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SimpleFileTransfer;

/// <summary>
/// Main entry point for the SimpleFileTransfer application.
/// Provides command-line interface for sending and receiving files over TCP/IP.
/// </summary>
public static class Program
{
    /// <summary>
    /// The default port used for file transfers.
    /// </summary>
    public const int Port = 9876;

    private static string _downloadsDir = Path.Combine(Environment.CurrentDirectory, "downloads");

    /// <summary>
    /// Gets or sets the directory where received files will be saved.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when attempting to set a null value.</exception>
    public static string DownloadsDirectory
    {
        get => _downloadsDir;
        set => _downloadsDir = value ?? throw new ArgumentNullException(nameof(value));
    }
    
    /// <summary>
    /// The transfer queue for managing sequential file transfers.
    /// </summary>
    public static readonly TransferQueue Queue = new();
    
    /// <summary>
    /// Main entry point for the application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        // Configure WebSockets
        var webSocketOptions = new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromMinutes(2)
        };
        app.UseWebSockets(webSocketOptions);

        // Map WebSocket endpoint
        app.Map("/ws", async context =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                try
                {
                    await WebSocketServer.HandleWebSocketAsync(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WebSocket error: {ex.Message}");
                }
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        });

        await app.RunAsync();
    }
    
    /// <summary>
    /// Displays usage information for the application.
    /// </summary>
    private static void DisplayUsage()
    {
        Console.WriteLine("SimpleFileTransfer - A simple file transfer utility");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  receive [options]                      - Start receiving files");
        Console.WriteLine("  send <host> <path> [options]           - Send a file or directory");
        Console.WriteLine("  send <host> <path1> <path2>... [options] - Send multiple files");
        Console.WriteLine("  list-resume                            - List all incomplete transfers that can be resumed");
        Console.WriteLine("  resume <index> [options]               - Resume an incomplete transfer");
        Console.WriteLine("  queue-list                             - List all transfers in the queue");
        Console.WriteLine("  queue-start                            - Start processing the queue");
        Console.WriteLine("  queue-stop                             - Stop processing the queue");
        Console.WriteLine("  queue-clear                            - Clear all transfers from the queue");
        Console.WriteLine();
        Console.WriteLine("Options for receive:");
        Console.WriteLine("  --password <password>                  - Password for decrypting files");
        Console.WriteLine();
        Console.WriteLine("Options for send:");
        Console.WriteLine("  --compress                             - Use GZip compression");
        Console.WriteLine("  --gzip                                 - Use GZip compression");
        Console.WriteLine("  --brotli                               - Use Brotli compression");
        Console.WriteLine("  --encrypt <password>                   - Encrypt data with the specified password");
        Console.WriteLine("  --resume                               - Enable resume capability for interrupted transfers");
        Console.WriteLine("  --queue                                - Add the transfer to the queue instead of executing immediately");
        Console.WriteLine();
        Console.WriteLine("Options for resume:");
        Console.WriteLine("  --password <password>                  - Password for encryption (if the transfer is encrypted)");
        Console.WriteLine("  --queue                                - Add the transfer to the queue instead of executing immediately");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  SimpleFileTransfer receive");
        Console.WriteLine("  SimpleFileTransfer receive --password mysecretpassword");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 myfile.txt");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 file1.txt file2.txt file3.txt");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 myfile.txt --compress");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 myfile.txt --resume");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 myfolder --brotli");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 myfile.txt --encrypt mysecretpassword");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 myfile.txt --brotli --encrypt mysecretpassword --resume");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 myfile.txt --queue");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 file1.txt file2.txt --queue");
        Console.WriteLine("  SimpleFileTransfer list-resume");
        Console.WriteLine("  SimpleFileTransfer resume 1");
        Console.WriteLine("  SimpleFileTransfer resume 1 --password mysecretpassword");
        Console.WriteLine("  SimpleFileTransfer resume 1 --queue");
        Console.WriteLine("  SimpleFileTransfer queue-list");
        Console.WriteLine("  SimpleFileTransfer queue-start");
        Console.WriteLine("  SimpleFileTransfer queue-stop");
        Console.WriteLine("  SimpleFileTransfer queue-clear");
    }
}
