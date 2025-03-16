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
            Console.WriteLine("Usage:");
            Console.WriteLine("  receive - Start receiving files");
            Console.WriteLine("  send <host> <path> - Send a file or directory");
            return;
        }

        if (args[0] == "receive")
        {
            var server = new FileTransferServer(DownloadsDirectory);
            server.Start(CancellationToken.None);
        }
        else if (args[0] == "send" && args.Length == 3)
        {
            var client = new FileTransferClient(args[1]);
            var path = args[2];
            
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
    }

    /// <summary>
    /// Calculates the SHA256 hash of a file.
    /// </summary>
    /// <param name="filepath">The path to the file to hash.</param>
    /// <returns>A lowercase hexadecimal string representation of the file's SHA256 hash.</returns>
    public static string CalculateHash(string filepath)
    {
        return FileOperations.CalculateHash(filepath);
    }

    /// <summary>
    /// Displays the progress of a file transfer operation on the console.
    /// </summary>
    /// <param name="current">The number of bytes transferred so far.</param>
    /// <param name="total">The total number of bytes to transfer.</param>
    /// <param name="bytesPerSecond">The current transfer speed in bytes per second.</param>
    public static void DisplayProgress(long current, long total, long bytesPerSecond)
    {
        FileOperations.DisplayProgress(current, total, bytesPerSecond);
    }

    /// <summary>
    /// Sends a directory and all its contents to a remote server.
    /// </summary>
    /// <param name="host">The hostname or IP address of the server to connect to.</param>
    /// <param name="dirPath">The path to the directory to send.</param>
    public static void SendDirectory(string host, string dirPath)
    {
        var client = new FileTransferClient(host);
        client.SendDirectory(dirPath);
    }

    /// <summary>
    /// Sends a single file to a remote server.
    /// </summary>
    /// <param name="host">The hostname or IP address of the server to connect to.</param>
    /// <param name="filepath">The path to the file to send.</param>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    public static void SendFile(string host, string filepath)
    {
        var client = new FileTransferClient(host);
        client.SendFile(filepath);
    }

    /// <summary>
    /// Starts a file transfer server and listens for incoming connections.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static void RunServer(CancellationToken cancellationToken = default)
    {
        var server = new FileTransferServer(DownloadsDirectory);
        server.Start(cancellationToken);
    }
}
