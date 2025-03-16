using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SimpleFileTransfer;

/// <summary>
/// Handles server-side file transfer operations, including receiving files and directories.
/// </summary>
public class FileTransferServer
{
    private readonly int _port;
    private readonly string _downloadsDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransferServer"/> class.
    /// </summary>
    /// <param name="downloadsDirectory">The directory where received files will be saved.</param>
    /// <param name="port">The port number to listen on. Defaults to <see cref="Program.Port"/>.</param>
    public FileTransferServer(string downloadsDirectory, int port = Program.Port)
    {
        _port = port;
        _downloadsDirectory = downloadsDirectory;
        Directory.CreateDirectory(_downloadsDirectory);
    }

    /// <summary>
    /// Starts the file transfer server and listens for incoming connections.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public void Start(CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        
        var hostName = Dns.GetHostName();
        var addresses = Dns.GetHostAddresses(hostName)
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        
        Console.WriteLine($"Server started on port {_port}");
        Console.WriteLine("IP Addresses:");
        foreach (var ip in addresses)
        {
            var isTailscale = ip.ToString().StartsWith("100.");
            Console.WriteLine($"  {ip}{(isTailscale ? " (Tailscale)" : "")}");
        }
        Console.WriteLine("Ctrl+C to exit");
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!listener.Pending())
                {
                    Thread.Sleep(100);  // Don't busy-wait
                    continue;
                }

                var client = listener.AcceptTcpClient();
                Console.WriteLine($"\nClient connected from {client.Client.RemoteEndPoint}");
                
                using var stream = client.GetStream();
                using var reader = new BinaryReader(stream);
                
                // Read first string to determine if it's a directory
                var firstString = reader.ReadString();
                
                if (firstString.StartsWith("DIR:"))
                {
                    ReceiveDirectory(reader, stream);
                }
                else
                {
                    // It's a single file transfer, firstString is the filename
                    ReceiveFile(reader, stream, firstString);
                }
                
                client.Close();
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Receives a directory and all its contents from a client.
    /// </summary>
    /// <param name="reader">The binary reader for reading from the network stream.</param>
    /// <param name="stream">The network stream connected to the client.</param>
    private void ReceiveDirectory(BinaryReader reader, NetworkStream stream)
    {
        // Read base directory name
        var dirName = reader.ReadString();
        // Read number of files
        var fileCount = reader.ReadInt32();
        
        Console.WriteLine($"Receiving directory: {dirName}");
        Console.WriteLine($"Total files: {fileCount}");
        
        // Create downloads directory
        var downloadsDir = Path.Combine(_downloadsDirectory, dirName);
        Directory.CreateDirectory(downloadsDir);
        
        for (var i = 0; i < fileCount; i++)
        {
            // Read relative path
            var relativePath = reader.ReadString();
            // Read filesize
            var filesize = reader.ReadInt64();
            // Read hash
            var sourceHash = reader.ReadString();
            
            Console.WriteLine($"\nReceiving file {i + 1}/{fileCount}: {relativePath}");
            
            // Create full save path
            var savePath = Path.Combine(downloadsDir, relativePath);
            Console.WriteLine($"Saving to: {savePath}");
            
            // Create directory if needed
            var dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            // Receive and save file
            using var fileStream = File.Create(savePath);
            var buffer = new byte[8192];
            var bytesRead = 0L;
            var sw = Stopwatch.StartNew();
            var lastUpdate = sw.ElapsedMilliseconds;
            var lastBytes = 0L;
            
            while (bytesRead < filesize)
            {
                var chunkSize = (int)Math.Min(buffer.Length, filesize - bytesRead);
                var read = stream.Read(buffer, 0, chunkSize);
                fileStream.Write(buffer, 0, read);
                bytesRead += read;
                
                var now = sw.ElapsedMilliseconds;
                if (now - lastUpdate >= 100)
                {
                    var bytesPerSecond = (bytesRead - lastBytes) * 1000 / (now - lastUpdate);
                    FileOperations.DisplayProgress(bytesRead, filesize, bytesPerSecond);
                    lastUpdate = now;
                    lastBytes = bytesRead;
                }
            }
            
            Console.WriteLine();
            fileStream.Close();
            
            // Verify hash
            Console.Write("Verifying file hash... ");
            var calculatedHash = FileOperations.CalculateHash(savePath);
            if (sourceHash == calculatedHash)
            {
                Console.WriteLine("File received and verified: " + savePath);
            }
            else
            {
                Console.WriteLine("Warning: File hash mismatch!");
                Console.WriteLine("Expected: " + sourceHash);
                Console.WriteLine("Calculated: " + calculatedHash);
            }
        }
        
        Console.WriteLine("\nDirectory received successfully");
    }

    /// <summary>
    /// Receives a single file from a client.
    /// </summary>
    /// <param name="reader">The binary reader for reading from the network stream.</param>
    /// <param name="stream">The network stream connected to the client.</param>
    /// <param name="filename">The name of the file being received.</param>
    private void ReceiveFile(BinaryReader reader, NetworkStream stream, string filename)
    {
        // Read filesize
        var filesize = reader.ReadInt64();
        // Read hash
        var sourceHash = reader.ReadString();
        
        // Create downloads directory
        Directory.CreateDirectory(_downloadsDirectory);
        
        // Create full save path
        var savePath = Path.Combine(_downloadsDirectory, filename);
        Console.WriteLine($"Receiving file: {filename} ({filesize:N0} bytes)");
        Console.WriteLine($"Saving to: {savePath}");
        
        // Create directory if needed
        var dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        // Receive and save file
        using var fileStream = File.Create(savePath);
        var buffer = new byte[8192];
        var bytesRead = 0L;
        var sw = Stopwatch.StartNew();
        var lastUpdate = sw.ElapsedMilliseconds;
        var lastBytes = 0L;
        
        while (bytesRead < filesize)
        {
            var chunkSize = (int)Math.Min(buffer.Length, filesize - bytesRead);
            var read = stream.Read(buffer, 0, chunkSize);
            fileStream.Write(buffer, 0, read);
            bytesRead += read;
            
            var now = sw.ElapsedMilliseconds;
            if (now - lastUpdate >= 100)
            {
                var bytesPerSecond = (bytesRead - lastBytes) * 1000 / (now - lastUpdate);
                FileOperations.DisplayProgress(bytesRead, filesize, bytesPerSecond);
                lastUpdate = now;
                lastBytes = bytesRead;
            }
        }
        
        Console.WriteLine();
        fileStream.Close();
        
        // Verify hash
        Console.Write("Verifying file hash... ");
        var calculatedHash = FileOperations.CalculateHash(savePath);
        if (sourceHash == calculatedHash)
        {
            Console.WriteLine("File received and verified: " + savePath);
        }
        else
        {
            Console.WriteLine("Warning: File hash mismatch!");
            Console.WriteLine("Expected: " + sourceHash);
            Console.WriteLine("Calculated: " + calculatedHash);
        }
    }
}
