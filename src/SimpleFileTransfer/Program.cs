using System;
using System.IO;
using System.Threading;

namespace SimpleFileTransfer;

public class Program
{
    public const int Port = 9876;
    private static string _downloadsDir = Path.Combine(Environment.CurrentDirectory, "downloads");

    public static string DownloadsDirectory
    {
        get => _downloadsDir;
        set => _downloadsDir = value ?? throw new ArgumentNullException(nameof(value));
    }
    
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

    public static string CalculateHash(string filepath)
    {
        return FileOperations.CalculateHash(filepath);
    }

    public static void DisplayProgress(long current, long total, long bytesPerSecond)
    {
        FileOperations.DisplayProgress(current, total, bytesPerSecond);
    }

    public static void SendDirectory(string host, string dirPath)
    {
        var client = new FileTransferClient(host);
        client.SendDirectory(dirPath);
    }

    public static void SendFile(string host, string filepath)
    {
        var client = new FileTransferClient(host);
        client.SendFile(filepath);
    }

    public static void RunServer(CancellationToken cancellationToken = default)
    {
        var server = new FileTransferServer(DownloadsDirectory);
        server.Start(cancellationToken);
    }
}
