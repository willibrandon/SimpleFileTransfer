using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleFileTransfer.Helpers;
using SimpleFileTransfer.Queue;
using SimpleFileTransfer.Services;
using SimpleFileTransfer.Transfer;
using SimpleFileTransfer.WebSockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
        if (args.Length == 0 || args[0] == "--web")
        {
            await StartWebApplication(args);
            return;
        }

        if (args[0] == "receive")
        {
            string? password = null;
            
            // Parse options
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--password" && i + 1 < args.Length)
                {
                    password = args[++i];
                }
            }
            
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };
            
            var server = new FileTransferServer(DownloadsDirectory, Port, password, cts.Token);
            server.Start();
            
            // Wait for cancellation
            try
            {
                cts.Token.WaitHandle.WaitOne();
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            
            server.Stop();
        }
        else if (args[0] == "send" && args.Length >= 3)
        {
            var host = args[1];
            var paths = new List<string>();
            var useCompression = false;
            var compressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip;
            var useEncryption = false;
            var password = string.Empty;
            var resumeEnabled = false;
            var queueTransfer = false;
            
            // Collect all paths before options
            int i = 2;
            while (i < args.Length && !args[i].StartsWith("--"))
            {
                paths.Add(args[i]);
                i++;
            }
            
            // Parse options
            for (; i < args.Length; i++)
            {
                if (args[i] == "--compress" || args[i] == "--gzip")
                {
                    useCompression = true;
                    compressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip;
                }
                else if (args[i] == "--brotli")
                {
                    useCompression = true;
                    compressionAlgorithm = CompressionHelper.CompressionAlgorithm.Brotli;
                }
                else if (args[i] == "--encrypt" && i + 1 < args.Length)
                {
                    useEncryption = true;
                    password = args[++i];
                }
                else if (args[i] == "--resume")
                {
                    resumeEnabled = true;
                }
                else if (args[i] == "--queue")
                {
                    queueTransfer = true;
                }
            }
            
            try
            {
                if (paths.Count == 1)
                {
                    var path = paths[0];
                    if (Directory.Exists(path))
                    {
                        if (queueTransfer)
                        {
                            // Add to queue
                            var transfer = new QueuedDirectoryTransfer(
                                host,
                                path,
                                useCompression,
                                compressionAlgorithm,
                                useEncryption,
                                password,
                                resumeEnabled);
                            
                            Queue.Enqueue(transfer);
                            
                            // Start queue if not already processing
                            if (!Queue.IsProcessing)
                            {
                                Console.WriteLine("Starting queue processing...");
                                Queue.Start();
                            }
                        }
                        else
                        {
                            // Execute immediately
                            var client = new FileTransferClient(
                                host, 
                                Port, 
                                useCompression, 
                                compressionAlgorithm, 
                                useEncryption, 
                                password,
                                resumeEnabled);
                            
                            client.SendDirectory(path);
                        }
                    }
                    else if (File.Exists(path))
                    {
                        if (queueTransfer)
                        {
                            // Add to queue
                            var transfer = new QueuedFileTransfer(
                                host,
                                path,
                                useCompression,
                                compressionAlgorithm,
                                useEncryption,
                                password,
                                resumeEnabled);
                            
                            Queue.Enqueue(transfer);
                            
                            // Start queue if not already processing
                            if (!Queue.IsProcessing)
                            {
                                Console.WriteLine("Starting queue processing...");
                                Queue.Start();
                            }
                        }
                        else
                        {
                            // Execute immediately
                            var client = new FileTransferClient(
                                host, 
                                Port, 
                                useCompression, 
                                compressionAlgorithm, 
                                useEncryption, 
                                password,
                                resumeEnabled);
                            
                            client.SendFile(path);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Error: Path not found: {path}");
                    }
                }
                else
                {
                    // Multiple files mode
                    var validFiles = new List<string>();
                    var invalidPaths = new List<string>();
                    
                    foreach (var path in paths)
                    {
                        if (File.Exists(path))
                        {
                            validFiles.Add(path);
                        }
                        else if (Directory.Exists(path))
                        {
                            Console.WriteLine($"Warning: {path} is a directory. Use a single directory path to send directories.");
                            invalidPaths.Add(path);
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Path not found: {path}");
                            invalidPaths.Add(path);
                        }
                    }
                    
                    if (invalidPaths.Count != 0)
                    {
                        Console.WriteLine($"Found {invalidPaths.Count} invalid paths. Continuing with valid files only.");
                    }
                    
                    if (validFiles.Count != 0)
                    {
                        if (queueTransfer)
                        {
                            // Add to queue
                            var transfer = new QueuedMultiFileTransfer(
                                host,
                                validFiles,
                                useCompression,
                                compressionAlgorithm,
                                useEncryption,
                                password,
                                resumeEnabled);
                            
                            Queue.Enqueue(transfer);
                            
                            // Start queue if not already processing
                            if (!Queue.IsProcessing)
                            {
                                Console.WriteLine("Starting queue processing...");
                                Queue.Start();
                            }
                        }
                        else
                        {
                            // Execute immediately
                            var client = new FileTransferClient(
                                host, 
                                Port, 
                                useCompression, 
                                compressionAlgorithm, 
                                useEncryption, 
                                password,
                                resumeEnabled);
                            
                            Console.WriteLine($"Sending {validFiles.Count} files to {host}");
                            client.SendMultipleFiles(validFiles);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error: No valid files to send.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        else if (args[0] == "list-resume")
        {
            try
            {
                FileTransferClient.ListResumableTransfers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        else if (args[0] == "resume" && args.Length >= 2)
        {
            try
            {
                if (!int.TryParse(args[1], out int index))
                {
                    Console.WriteLine("Error: Invalid index. Please provide a number.");
                    return;
                }
                
                string? password = null;
                bool queueTransfer = false;
                
                // Parse options
                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i] == "--password" && i + 1 < args.Length)
                    {
                        password = args[++i];
                    }
                    else if (args[i] == "--queue")
                    {
                        queueTransfer = true;
                    }
                }
                
                if (queueTransfer)
                {
                    // Get resume info
                    var resumeFiles = TransferResumeManager.GetAllResumeFiles();
                    
                    if (resumeFiles.Count == 0)
                    {
                        Console.WriteLine("No incomplete transfers found.");
                        return;
                    }
                    
                    if (index < 1 || index > resumeFiles.Count)
                    {
                        Console.WriteLine($"Invalid index. Please specify a number between 1 and {resumeFiles.Count}.");
                        return;
                    }
                    
                    var info = resumeFiles[index - 1];
                    
                    if (info.UseEncryption && string.IsNullOrEmpty(password))
                    {
                        Console.WriteLine("This transfer is encrypted. Please provide a password using the --password option.");
                        return;
                    }
                    
                    if (!File.Exists(info.FilePath))
                    {
                        Console.WriteLine($"The file {info.FilePath} no longer exists. Cannot resume transfer.");
                        return;
                    }
                    
                    if (!string.IsNullOrEmpty(info.DirectoryName))
                    {
                        // Part of a directory transfer
                        var dirPath = Path.GetDirectoryName(info.FilePath);
                        if (dirPath != null && Directory.Exists(dirPath))
                        {
                            var transfer = new QueuedDirectoryTransfer(
                                info.Host,
                                dirPath,
                                info.UseCompression,
                                info.CompressionAlgorithm,
                                info.UseEncryption,
                                password,
                                true);
                            
                            Queue.Enqueue(transfer);
                            
                            // Start queue if not already processing
                            if (!Queue.IsProcessing)
                            {
                                Console.WriteLine("Starting queue processing...");
                                Queue.Start();
                            }
                        }
                        else
                        {
                            Console.WriteLine($"The directory containing {info.FilePath} no longer exists. Cannot resume transfer.");
                        }
                    }
                    else if (info.IsMultiFile)
                    {
                        // Part of a multi-file transfer
                        var multiFileTransfers = resumeFiles
                            .Where(r => r.IsMultiFile && r.Host == info.Host && r.Port == info.Port)
                            .ToList();
                        
                        // Filter out files that no longer exist
                        var validFiles = multiFileTransfers
                            .Where(r => File.Exists(r.FilePath))
                            .Select(r => r.FilePath)
                            .ToList();
                        
                        if (validFiles.Count == 0)
                        {
                            Console.WriteLine("No valid files found for this multi-file transfer. Cannot resume.");
                            return;
                        }
                        
                        var transfer = new QueuedMultiFileTransfer(
                            info.Host,
                            validFiles,
                            info.UseCompression,
                            info.CompressionAlgorithm,
                            info.UseEncryption,
                            password,
                            true);
                        
                        Queue.Enqueue(transfer);
                        
                        // Start queue if not already processing
                        if (!Queue.IsProcessing)
                        {
                            Console.WriteLine("Starting queue processing...");
                            Queue.Start();
                        }
                    }
                    else
                    {
                        // Single file transfer
                        var transfer = new QueuedFileTransfer(
                            info.Host,
                            info.FilePath,
                            info.UseCompression,
                            info.CompressionAlgorithm,
                            info.UseEncryption,
                            password,
                            true);
                        
                        Queue.Enqueue(transfer);
                        
                        // Start queue if not already processing
                        if (!Queue.IsProcessing)
                        {
                            Console.WriteLine("Starting queue processing...");
                            Queue.Start();
                        }
                    }
                }
                else
                {
                    // Execute immediately
                    FileTransferClient.ResumeTransfer(index, password);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        else if (args[0] == "queue-list")
        {
            Queue.ListTransfers();
        }
        else if (args[0] == "queue-start")
        {
            if (Queue.IsProcessing)
            {
                Console.WriteLine("Queue is already processing.");
            }
            else
            {
                Queue.Start();
            }
        }
        else if (args[0] == "queue-stop")
        {
            if (!Queue.IsProcessing)
            {
                Console.WriteLine("Queue is not currently processing.");
            }
            else
            {
                Queue.Stop();
            }
        }
        else if (args[0] == "queue-clear")
        {
            Queue.Clear();
        }
        else
        {
            DisplayUsage();
        }
    }
    
    /// <summary>
    /// Starts the web application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task StartWebApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();
        
        // Add health checks
        builder.Services.AddHealthChecks();
        
        // Add CORS policy
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Register the file transfer service
        builder.Services.AddScoped<IFileTransferService, FileTransferService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        
        // Use CORS middleware
        app.UseCors();
        
        app.MapControllers();
        
        // Map health check endpoint
        app.MapHealthChecks("/health");

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

        Console.WriteLine("SimpleFileTransfer API is starting...");
        await app.RunAsync();
        Console.WriteLine("SimpleFileTransfer API is running.");
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
        Console.WriteLine("  --web                                  - Start the web application");
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
        Console.WriteLine("  SimpleFileTransfer --web");
    }
}
