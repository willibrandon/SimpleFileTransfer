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
}
