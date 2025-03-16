using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace SimpleFileTransfer;

/// <summary>
/// Main entry point for the SimpleFileTransfer application.
/// Provides command-line interface for sending and receiving files over TCP/IP.
/// </summary>
public class Program
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
    /// Main entry point for the application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            DisplayUsage();
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
            }
            
            try
            {
                var client = new FileTransferClient(
                    host, 
                    Port, 
                    useCompression, 
                    compressionAlgorithm, 
                    useEncryption, 
                    password,
                    resumeEnabled);
                
                if (paths.Count == 1)
                {
                    var path = paths[0];
                    if (Directory.Exists(path))
                    {
                        client.SendDirectory(path);
                    }
                    else if (File.Exists(path))
                    {
                        client.SendFile(path);
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
                        Console.WriteLine($"Sending {validFiles.Count} files to {host}");
                        client.SendMultipleFiles(validFiles);
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
                
                // Parse options
                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i] == "--password" && i + 1 < args.Length)
                    {
                        password = args[++i];
                    }
                }
                
                FileTransferClient.ResumeTransfer(index, password);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        else
        {
            DisplayUsage();
        }
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
        Console.WriteLine();
        Console.WriteLine("Options for resume:");
        Console.WriteLine("  --password <password>                  - Password for encryption (if the transfer is encrypted)");
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
        Console.WriteLine("  SimpleFileTransfer list-resume");
        Console.WriteLine("  SimpleFileTransfer resume 1");
        Console.WriteLine("  SimpleFileTransfer resume 1 --password mysecretpassword");
    }
}
