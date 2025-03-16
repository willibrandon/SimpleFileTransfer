using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Collections.Generic;
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
            RunServer(CancellationToken.None);
        }
        else if (args[0] == "send" && args.Length == 3)
        {
            var path = args[2];
            if (Directory.Exists(path))
            {
                SendDirectory(args[1], path);
            }
            else if (File.Exists(path))
            {
                SendFile(args[1], path);
            }
            else
            {
                Console.WriteLine($"Error: Path not found: {path}");
            }
        }
    }

    public static string CalculateHash(string filepath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filepath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static void DisplayProgress(long current, long total, long bytesPerSecond)
    {
        var percentage = current * 100 / total;
        var mbps = bytesPerSecond / 1024.0 / 1024.0;
        Console.Write($"\rProgress: {current:N0}/{total:N0} bytes ({percentage}%) - {mbps:F2} MB/s");
    }

    public static void SendDirectory(string host, string dirPath)
    {
        var files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
        var dirInfo = new DirectoryInfo(dirPath);
        var totalSize = files.Sum(f => new FileInfo(f).Length);
        
        Console.WriteLine($"Preparing to send directory: {dirPath}");
        Console.WriteLine($"Total files: {files.Length}");
        Console.WriteLine($"Total size: {totalSize:N0} bytes");
        
        try
        {
            using var client = new TcpClient();
            client.Connect(host, Port);
            using var stream = client.GetStream();
            using var writer = new BinaryWriter(stream);
            
            // Send marker indicating this is a directory
            writer.Write("DIR:");
            // Send base directory name
            writer.Write(dirInfo.Name);
            // Send number of files
            writer.Write(files.Length);
            
            foreach (var (file, index) in files.Select((f, i) => (f, i)))
            {
                var fileInfo = new FileInfo(file);
                var relativePath = Path.GetRelativePath(dirPath, file);
                
                Console.WriteLine($"\nSending file {index + 1}/{files.Length}: {relativePath}");
                
                // Calculate hash
                var hash = CalculateHash(file);
                
                // Send relative path
                writer.Write(relativePath);
                // Send filesize
                writer.Write(fileInfo.Length);
                // Send hash
                writer.Write(hash);
                
                // Send file data
                using var fileStream = File.OpenRead(file);
                var buffer = new byte[8192];
                var bytesRead = 0L;
                var sw = Stopwatch.StartNew();
                var lastUpdate = sw.ElapsedMilliseconds;
                var lastBytes = 0L;
                int read;
                
                while ((read = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    stream.Write(buffer, 0, read);
                    bytesRead += read;
                    
                    var now = sw.ElapsedMilliseconds;
                    if (now - lastUpdate >= 100)
                    {
                        var bytesPerSecond = (bytesRead - lastBytes) * 1000 / (now - lastUpdate);
                        DisplayProgress(bytesRead, fileInfo.Length, bytesPerSecond);
                        lastUpdate = now;
                        lastBytes = bytesRead;
                    }
                }
                Console.WriteLine();
            }
            
            Console.WriteLine("\nDirectory sent successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    public static void RunServer(CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        
        var hostName = Dns.GetHostName();
        var addresses = Dns.GetHostAddresses(hostName)
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        
        Console.WriteLine($"Server started on port {Port}");
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

    public static void ReceiveDirectory(BinaryReader reader, NetworkStream stream)
    {
        // Read base directory name
        var dirName = reader.ReadString();
        // Read number of files
        var fileCount = reader.ReadInt32();
        
        Console.WriteLine($"Receiving directory: {dirName}");
        Console.WriteLine($"Total files: {fileCount}");
        
        // Create downloads directory in current working directory
        var downloadsDir = Path.Combine(DownloadsDirectory, dirName);
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
                    DisplayProgress(bytesRead, filesize, bytesPerSecond);
                    lastUpdate = now;
                    lastBytes = bytesRead;
                }
            }
            
            Console.WriteLine();
            fileStream.Close();
            
            // Verify hash
            var receivedHash = CalculateHash(savePath);
            if (sourceHash == receivedHash)
            {
                Console.WriteLine($"File received and verified: {savePath}");
            }
            else
            {
                Console.WriteLine($"Warning: File hash mismatch!");
                Console.WriteLine($"Expected: {sourceHash}");
                Console.WriteLine($"Received: {receivedHash}");
            }
        }
        
        Console.WriteLine("\nDirectory received successfully");
    }

    public static void ReceiveFile(BinaryReader reader, NetworkStream stream, string filename)
    {
        // Read filesize
        var filesize = reader.ReadInt64();
        // Read hash
        var sourceHash = reader.ReadString();
        
        // Create downloads directory in current working directory
        Directory.CreateDirectory(DownloadsDirectory);
        
        // Create full save path
        var savePath = Path.Combine(DownloadsDirectory, filename);
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
                DisplayProgress(bytesRead, filesize, bytesPerSecond);
                lastUpdate = now;
                lastBytes = bytesRead;
            }
        }
        
        Console.WriteLine();
        fileStream.Close();
        
        // Verify hash
        var receivedHash = CalculateHash(savePath);
        if (sourceHash == receivedHash)
        {
            Console.WriteLine($"File received and verified: {savePath}");
        }
        else
        {
            Console.WriteLine($"Warning: File hash mismatch!");
            Console.WriteLine($"Expected: {sourceHash}");
            Console.WriteLine($"Received: {receivedHash}");
        }
    }

    public static void SendFile(string host, string filepath)
    {
        if (!File.Exists(filepath))
        {
            throw new FileNotFoundException($"File not found: {filepath}", filepath);
        }
        
        var fileInfo = new FileInfo(filepath);
        Console.WriteLine($"Sending {fileInfo.Name} ({fileInfo.Length:N0} bytes) to {host}");
        
        // Calculate hash before sending
        Console.Write("Calculating file hash... ");
        var hash = CalculateHash(filepath);
        Console.WriteLine("done");
        
        try
        {
            using var client = new TcpClient();
            client.Connect(host, Port);
            using var stream = client.GetStream();
            using var writer = new BinaryWriter(stream);
            
            // Send filename
            writer.Write(Path.GetFileName(filepath));
            // Send filesize
            writer.Write(fileInfo.Length);
            // Send hash
            writer.Write(hash);
            
            // Send file data
            using var fileStream = File.OpenRead(filepath);
            var buffer = new byte[8192];
            var bytesRead = 0L;
            var sw = Stopwatch.StartNew();
            var lastUpdate = sw.ElapsedMilliseconds;
            var lastBytes = 0L;
            int read;
            
            while ((read = fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                stream.Write(buffer, 0, read);
                bytesRead += read;
                
                var now = sw.ElapsedMilliseconds;
                if (now - lastUpdate >= 100)
                {
                    var bytesPerSecond = (bytesRead - lastBytes) * 1000 / (now - lastUpdate);
                    DisplayProgress(bytesRead, fileInfo.Length, bytesPerSecond);
                    lastUpdate = now;
                    lastBytes = bytesRead;
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("File sent successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
