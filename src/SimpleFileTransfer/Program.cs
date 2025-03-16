using System;
using System.IO;
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
            
            var server = new FileTransferServer(DownloadsDirectory, Port, password);
            server.Start(CancellationToken.None);
        }
        else if (args[0] == "send" && args.Length >= 3)
        {
            var host = args[1];
            var path = args[2];
            var useCompression = false;
            var compressionAlgorithm = CompressionHelper.CompressionAlgorithm.GZip;
            var useEncryption = false;
            var password = string.Empty;
            
            // Parse options
            for (int i = 3; i < args.Length; i++)
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
            }
            
            try
            {
                var client = new FileTransferClient(
                    host, 
                    Port, 
                    useCompression, 
                    compressionAlgorithm, 
                    useEncryption, 
                    password);
                
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
        Console.WriteLine("  receive [options]                - Start receiving files");
        Console.WriteLine("  send <host> <path> [options]     - Send a file or directory");
        Console.WriteLine();
        Console.WriteLine("Options for receive:");
        Console.WriteLine("  --password <password>            - Password for decrypting files");
        Console.WriteLine();
        Console.WriteLine("Options for send:");
        Console.WriteLine("  --compress                       - Use GZip compression");
        Console.WriteLine("  --gzip                           - Use GZip compression");
        Console.WriteLine("  --brotli                         - Use Brotli compression");
        Console.WriteLine("  --encrypt <password>             - Encrypt data with the specified password");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  SimpleFileTransfer receive");
        Console.WriteLine("  SimpleFileTransfer receive --password mysecretpassword");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 myfile.txt");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 myfile.txt --compress");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 myfolder --brotli");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 myfile.txt --encrypt mysecretpassword");
        Console.WriteLine("  SimpleFileTransfer send 192.168.1.100 myfile.txt --brotli --encrypt mysecretpassword");
    }
}
